using System.Collections;
using UnityEngine;
using Photon.Pun;

[RequireComponent(typeof(PhotonView))]
public class PlayerTeleportHandler : MonoBehaviourPunCallbacks
{
    [Header("Setup")]
    [SerializeField] private Transform rigRoot;
    [SerializeField] private Transform hipBone;
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float groundOffset = 0.5f;
    [SerializeField] private float maxGroundSnapDistance = 6f;
    [SerializeField] private float raycastUpwardOffset = 50f;
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

    public void InitiateTeleport(Vector3 targetPosition, Quaternion? targetRotation = null, bool snapToGround = true)
    {
        bool hasRotation = targetRotation.HasValue;
        Quaternion rotation = targetRotation ?? Quaternion.identity;
        pv.RPC(nameof(NetworkTeleportPlayer), RpcTarget.All, targetPosition, hasRotation, rotation, snapToGround);
    }

    [PunRPC]
    public void NetworkTeleportPlayer(Vector3 targetPosition, bool hasRotation, Quaternion targetRotation, bool snapToGround)
    {
        if (!pv.IsMine) return;
        StartCoroutine(PerformTeleport(targetPosition, hasRotation ? targetRotation : (Quaternion?)null, snapToGround));
    }

    private IEnumerator PerformTeleport(Vector3 targetPosition, Quaternion? targetRotation, bool snapToGround)
    {
        if (isTeleporting) yield break;
        isTeleporting = true;

        if (rigRoot == null)
        {
            Debug.LogError("[Teleport] rigRoot is null.", this);
            isTeleporting = false;
            yield break;
        }

        var rbs = rigRoot.GetComponentsInChildren<Rigidbody>(true);

        Rigidbody hipRb = null;
        if (hipBone != null) hipRb = hipBone.GetComponent<Rigidbody>();
        if (hipRb == null && rbs.Length > 0) hipRb = rbs[0];

        if (hipRb == null)
        {
            Debug.LogError("[Teleport] No Rigidbody found under rigRoot.", this);
            isTeleporting = false;
            yield break;
        }

        // -----------------------------------------------------------------------
        // DIAGNOSTIC — read ALL candidate positions BEFORE freezing anything.
        // Check this log carefully. The position that matches "where you actually
        // are standing in the world" is the one we should use as rootCurrentPos.
        // -----------------------------------------------------------------------
        Debug.Log(
            $"[Teleport DIAG] targetPosition={targetPosition}\n" +
            $"  transform.position        = {transform.position}  (container — likely stale)\n" +
            $"  rigRoot.position          = {rigRoot.position}    (rig container — likely stale)\n" +
            $"  hipRb.position            = {hipRb.position}      (physics rb)\n" +
            $"  hipRb.transform.position  = {hipRb.transform.position} (bone Transform)\n" +
            $"  hipRb.isKinematic         = {hipRb.isKinematic}\n" +
            $"  rbs[0].name               = {rbs[0].name}  rbs[0].position = {rbs[0].position}"
        );

        // Also log every bone so we can see the full spread
        for (int i = 0; i < rbs.Length; i++)
            Debug.Log($"[Teleport DIAG]   bone[{i}] {rbs[i].name}: rb.position={rbs[i].position}  transform.position={rbs[i].transform.position}  isKinematic={rbs[i].isKinematic}");

        float feetOffset = EstimateFeetOffset(hipRb);
        Debug.Log($"[Teleport DIAG] feetOffset={feetOffset}");

        // Ground snap
        Vector3 finalTargetPos = new Vector3(targetPosition.x, targetPosition.y + feetOffset, targetPosition.z);
        if (snapToGround)
        {
            Vector3 rayStart = targetPosition + Vector3.up * 3f;
            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hitInfo, 80f, groundMask, QueryTriggerInteraction.Ignore))
            {
                float verticalDelta = Mathf.Abs(hitInfo.point.y - targetPosition.y);
                Debug.Log($"[Teleport DIAG] Ground hit at {hitInfo.point}  verticalDelta={verticalDelta}  collider={hitInfo.collider.name}");
                if (verticalDelta <= maxGroundSnapDistance)
                    finalTargetPos = new Vector3(targetPosition.x, hitInfo.point.y + groundOffset + feetOffset, targetPosition.z);
                else
                    Debug.LogWarning($"[Teleport] Ground snap skipped — too far ({verticalDelta:F2}m).");
            }
            else
            {
                Debug.LogWarning("[Teleport] Ground snap raycast found nothing.");
            }
        }

        Debug.Log($"[Teleport DIAG] finalTargetPos={finalTargetPos}");

        // Freeze
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

        // -----------------------------------------------------------------------
        // DIAGNOSTIC — log again after the fixed update so we can see if
        // setting isKinematic changed any positions.
        // -----------------------------------------------------------------------
        Debug.Log(
            $"[Teleport DIAG] After WaitForFixedUpdate:\n" +
            $"  hipRb.position            = {hipRb.position}\n" +
            $"  hipRb.transform.position  = {hipRb.transform.position}"
        );

        Vector3 hipCurrentPos  = hipRb.position;
        Vector3 positionDelta  = finalTargetPos - hipCurrentPos;

        Debug.Log($"[Teleport DIAG] hipCurrentPos={hipCurrentPos}  positionDelta={positionDelta}");

        Quaternion rotationDelta = Quaternion.identity;
        if (targetRotation.HasValue)
            rotationDelta = targetRotation.Value * Quaternion.Inverse(hipRb.rotation);

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

        if (targetRotation.HasValue)
        {
            Vector3 transformRelativeToHip = transform.position - hipCurrentPos;
            transform.position = finalTargetPos + rotationDelta * transformRelativeToHip;
            transform.rotation = targetRotation.Value;
        }
        else
        {
            transform.position += positionDelta;
        }

        Physics.SyncTransforms();

        Debug.Log($"[Teleport DIAG] After move: hipRb.position={hipRb.position}  transform.position={transform.position}");

        var net = GetComponent<RagdollNetwork>();
        if (net != null) net.SnapSendGate();

        var poseHandler = GetComponentInChildren<PoseHandler>(true);
        if (poseHandler != null)
        {
            poseHandler.StopAllCoroutines();
            poseHandler.SetPose(0);
            poseHandler.DoingAnimation = false;
        }

        StartCoroutine(UnfreezeRigidbodies(rbs, rootWasKinematic));
        isTeleporting = false;
    }

    private float EstimateFeetOffset(Rigidbody hipRb)
    {
        float hipY    = hipRb.position.y;
        float lowestY = hipY;
        bool found    = false;

        foreach (var c in GetComponentsInChildren<Collider>(true))
        {
            if (c == null || !c.enabled || c.isTrigger) continue;
            float minY = c.bounds.min.y;
            if (!found || minY < lowestY) { lowestY = minY; found = true; }
        }

        if (!found) return 1f;
        return Mathf.Max(0.1f, hipY - lowestY);
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