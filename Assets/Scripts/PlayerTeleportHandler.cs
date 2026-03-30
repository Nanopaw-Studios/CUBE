using System.Collections;
using UnityEngine;
using Photon.Pun;

[RequireComponent(typeof(PhotonView))]
public class PlayerTeleportHandler : MonoBehaviourPunCallbacks
{
    [Header("Setup")]
    [Tooltip("Root of the ragdoll rig. If left null, will try to Find(\"RIG\").")]
    [SerializeField] private Transform rigRoot;

    [Tooltip("Layer mask used to find valid ground below a spawn point.")]
    [SerializeField] private LayerMask groundMask = ~0;

    [Tooltip("How far above the ground the player should be placed.")]
    [SerializeField] private float groundOffset = 0.5f;
    [Tooltip("Maximum allowed vertical correction when snapping to ground. Prevents snapping to far-below void colliders.")]
    [SerializeField] private float maxGroundSnapDistance = 6f;

    [Tooltip("How far above the spawn point the ground raycast starts. Increase if spawns are on elevated platforms.")]
    [SerializeField] private float raycastUpwardOffset = 50f;

    [Tooltip("Maximum downward distance the ground raycast will search.")]
    [SerializeField] private float raycastMaxDistance = 200f;

    private PhotonView pv;
    private Rigidbody rootRb;
    private bool isTeleporting;

    private void Awake()
    {
        pv = GetComponent<PhotonView>();
        rootRb = GetComponent<Rigidbody>();

        if (rigRoot == null)
        {
            var found = transform.Find("RIG");
            if (found != null) rigRoot = found;
        }
    }

    /// <summary>
    /// Teleport this player to a world position/rotation. Only callable on the owning client.
    /// Set snapToGround to false for spawn points that are already correctly placed (e.g. intermission platforms).
    /// </summary>
    public void InitiateTeleport(Vector3 targetPosition, Quaternion? targetRotation = null, bool snapToGround = true)
    {
        bool hasRotation = targetRotation.HasValue;
        Quaternion rotation = targetRotation ?? Quaternion.identity;

        // Do not buffer teleports. Spawn/round logic should decide position for late joiners,
        // and buffered teleports can replay at incorrect times after round transitions.
        pv.RPC(nameof(NetworkTeleportPlayer), RpcTarget.All, targetPosition, hasRotation, rotation, snapToGround);
    }

    [PunRPC]
    public void NetworkTeleportPlayer(Vector3 targetPosition, bool hasRotation, Quaternion targetRotation, bool snapToGround)
    {
        // Remotes do NOT run the physics teleport — RagdollNetwork will naturally
        // lerp to the new positions once the owner broadcasts them after moving.
        if (!pv.IsMine) return;

        StartCoroutine(PerformTeleport(targetPosition, hasRotation ? targetRotation : (Quaternion?)null, snapToGround));
    }

    private IEnumerator PerformTeleport(Vector3 targetPosition, Quaternion? targetRotation, bool snapToGround)
    {
        if (isTeleporting) yield break;
        isTeleporting = true;

        if (rigRoot == null)
        {
            Debug.LogError("[Teleport] rigRoot is null. Cannot teleport ragdoll safely.", this);
            isTeleporting = false;
            yield break;
        }

        // 1) Find a safe destination for the player's root transform.
        Vector3 finalTargetPos = targetPosition;
        float feetOffset = EstimateFeetOffset();
        if (snapToGround)
        {
            Vector3 rayStart = targetPosition + Vector3.up * 3f;
            RaycastHit hitInfo;
            if (Physics.Raycast(rayStart, Vector3.down, out hitInfo, 80f, groundMask, QueryTriggerInteraction.Ignore))
            {
                float verticalDelta = Mathf.Abs(hitInfo.point.y - targetPosition.y);
                if (verticalDelta <= maxGroundSnapDistance)
                {
                    finalTargetPos = new Vector3(targetPosition.x, hitInfo.point.y + groundOffset, targetPosition.z);
                }
                else
                {
                    Debug.LogWarning($"[Teleport] Ground hit too far from target ({verticalDelta:F2}m). Using target position without snap.");
                    finalTargetPos = targetPosition;
                }
            }
            else
            {
                // No usable ground hit; trust the requested position rather than forcing an old/current position.
                finalTargetPos = targetPosition;
            }
        }

        // 2) Freeze all ragdoll rigidbodies so physics doesn't fight the move
        var rbs = rigRoot.GetComponentsInChildren<Rigidbody>(true);
        foreach (var rb in rbs)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.Sleep();
        }

        bool rootWasKinematic = false;
        if (rootRb != null)
        {
            rootWasKinematic = rootRb.isKinematic;
            rootRb.isKinematic = true;
            rootRb.linearVelocity = Vector3.zero;
            rootRb.angularVelocity = Vector3.zero;
            rootRb.Sleep();
        }

        yield return new WaitForFixedUpdate();

        // 3) Compute delta from the root transform. This avoids rig-root pivot offsets.
        Vector3 rootCurrentPos = transform.position;
        Vector3 positionDelta = finalTargetPos - rootCurrentPos;

        Quaternion rotationDelta = Quaternion.identity;
        if (targetRotation.HasValue)
            rotationDelta = targetRotation.Value * Quaternion.Inverse(transform.rotation);

        foreach (var rb in rbs)
        {
            if (targetRotation.HasValue)
            {
                Vector3 relativeToRoot = rb.position - rootCurrentPos;
                rb.position = finalTargetPos + rotationDelta * relativeToRoot;
                rb.rotation = rotationDelta * rb.rotation;
            }
            else
            {
                rb.position += positionDelta;
            }
        }

        // 4) Move the prefab root transform to target
        if (targetRotation.HasValue)
        {
            transform.position = finalTargetPos;
            transform.rotation = targetRotation.Value;
        }
        else
        {
            transform.position = finalTargetPos;
        }

        Physics.SyncTransforms();

        // 5) Tell RagdollNetwork to snap its send-gate references so the next
        //    serialized packet immediately reflects the new position (no stale gate).
        var net = GetComponent<RagdollNetwork>();
        if (net != null) net.SnapSendGate();

        // 5.5) Force pose back to rest after teleport so arm animations don't get stuck in a "spider" pose.
        var poseHandler = GetComponentInChildren<PoseHandler>(true);
        if (poseHandler != null)
        {
            poseHandler.StopAllCoroutines();
            poseHandler.SetPose(0);
            poseHandler.DoingAnimation = false;
        }

        // 6) Unfreeze after two fixed frames so physics settles at the new position
        StartCoroutine(UnfreezeRigidbodies(rbs, rootWasKinematic));

        isTeleporting = false;
    }

    private float EstimateFeetOffset()
    {
        float rootY = transform.position.y;
        float lowestY = rootY;
        bool found = false;

        var cols = GetComponentsInChildren<Collider>(true);
        foreach (var c in cols)
        {
            if (c == null || !c.enabled || c.isTrigger) continue;
            float minY = c.bounds.min.y;
            if (!found || minY < lowestY)
            {
                lowestY = minY;
                found = true;
            }
        }

        if (!found) return 1f;
        return Mathf.Max(0.1f, rootY - lowestY);
    }

    private IEnumerator UnfreezeRigidbodies(Rigidbody[] rbs, bool rootWasKinematic)
    {
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        foreach (var rb in rbs)
        {
            rb.isKinematic = false;
            rb.WakeUp();
        }

        if (rootRb != null)
            rootRb.isKinematic = rootWasKinematic;
    }
}