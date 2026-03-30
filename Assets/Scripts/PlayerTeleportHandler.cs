using System.Collections;
using UnityEngine;
using Photon.Pun;

[RequireComponent(typeof(PhotonView))]
public class PlayerTeleportHandler : MonoBehaviourPunCallbacks
{
    [Header("Setup")]
    [Tooltip("Root of the ragdoll rig. If left null, will try to Find(\"RIG\").")]
    [SerializeField] private Transform rigRoot;

    [SerializeField] private Rigidbody[] teleportBodies;

    [Tooltip("Layer mask used to find valid ground below a spawn point.")]
    [SerializeField] private LayerMask groundMask = ~0;

    [Tooltip("How far above the ground the player should be placed.")]
    [SerializeField] private float groundOffset = 0.5f;

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
    /// </summary>
    public void InitiateTeleport(Vector3 targetPosition, Quaternion? targetRotation = null, bool snapToGround = true)
    {
        if (!pv.IsMine)
        {
            Debug.LogWarning("[Teleport] Cannot teleport a player you don't own!");
            return;
        }

        bool hasRotation = targetRotation.HasValue;
        Quaternion rotation = targetRotation ?? Quaternion.identity;

        // Keep only the most recent teleport RPC for this player to avoid stale buffered
        // teleports firing after round/intermission transitions.
        PhotonNetwork.RemoveRPCs(pv);
        // Buffered so late joiners also get the last teleport
        pv.RPC(nameof(NetworkTeleportPlayer), RpcTarget.AllBuffered, targetPosition, hasRotation, rotation, snapToGround);
    }

    [PunRPC]
    public void NetworkTeleportPlayer(Vector3 targetPosition, bool hasRotation, Quaternion targetRotation, bool snapToGround)
    {
        // Remotes do NOT run the physics teleport — RagdollNetwork will naturally
        // lerp to the new positions once the owner broadcasts them after moving.
        // Running PerformTeleport on remotes causes their kinematic bone positions
        // to be overwritten by the teleport, then immediately fought by RagdollNetwork.Update().
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

        // 1) Find a safe ground position
        Vector3 finalTargetPos = targetPosition;
        if (snapToGround)
        {
            Vector3 rayStart = targetPosition + Vector3.up * 5f;
            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 80f, groundMask, QueryTriggerInteraction.Ignore))
                finalTargetPos = hit.point + Vector3.up * groundOffset;
            else
            {
                Debug.LogWarning($"[Teleport] No ground found below {targetPosition}. Keeping current position to avoid void teleport.");
                finalTargetPos = rigRoot.position;
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

        // 3) Compute the delta from the rig root's current world position to the target.
        //    Apply this same delta to every bone so the entire pose shifts by the same offset.
        Vector3 hipCurrentPos = rigRoot.position;
        Vector3 positionDelta = finalTargetPos - hipCurrentPos;

        Quaternion rotationDelta = Quaternion.identity;
        if (targetRotation.HasValue)
            rotationDelta = targetRotation.Value * Quaternion.Inverse(rigRoot.rotation);

        foreach (var rb in rbs)
        {
            if (targetRotation.HasValue)
            {
                Vector3 relativeToHip = rb.position - hipCurrentPos;
                rb.position = finalTargetPos + rotationDelta * relativeToHip;
                rb.rotation = rotationDelta * rb.rotation;
            }
            else
            {
                rb.position += positionDelta;
            }
        }

        // 4) Move the prefab root transform to stay aligned with the rig
        Vector3 rootMinusHip = transform.position - hipCurrentPos;
        if (targetRotation.HasValue)
        {
            transform.position = finalTargetPos + rotationDelta * rootMinusHip;
            transform.rotation = rotationDelta * transform.rotation;
        }
        else
        {
            transform.position += positionDelta;
        }

        Physics.SyncTransforms();

        // 5) Tell RagdollNetwork to snap its send-gate references so the next
        //    serialized packet immediately reflects the new position (no stale gate).
        //    Only update owner-side targets — never touch remote bones.
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
