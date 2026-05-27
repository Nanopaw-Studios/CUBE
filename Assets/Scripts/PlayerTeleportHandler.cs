using System.Collections;
using Photon.Pun;
using UnityEngine;

[RequireComponent(typeof(PhotonView))]
public class PlayerTeleportHandler : MonoBehaviourPun
{
    [Header("Setup")]
    [SerializeField]
    private Transform rigRoot;

    [SerializeField]
    private Transform hipBone;

    [SerializeField]
    private LayerMask groundMask = ~0;

    [SerializeField]
    private float groundOffset = 1.2f;

    [SerializeField]
    private float maxGroundSnapDistance = 6f;

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
            if (found != null)
                rigRoot = found;
        }
    }

    // 🚀 PUBLIC ENTRY (LOCAL ONLY)
    public void Teleport(
        Vector3 targetPosition,
        Quaternion? targetRotation = null,
        bool snapToGround = true,
        bool useOffset = false
    )
    {
        if (!pv.IsMine)
            return;

        StartCoroutine(PerformTeleport(targetPosition, targetRotation, snapToGround, useOffset));
    }

    private IEnumerator PerformTeleport(
        Vector3 targetPosition,
        Quaternion? targetRotation,
        bool snapToGround,
        bool useOffset
    )
    {
        if (isTeleporting)
            yield break;
        isTeleporting = true;

        if (rigRoot == null)
        {
            Debug.LogError("[Teleport] rigRoot is null.", this);
            isTeleporting = false;
            yield break;
        }

        var rbs = rigRoot.GetComponentsInChildren<Rigidbody>(true);

        Rigidbody hipRb = hipBone != null ? hipBone.GetComponent<Rigidbody>() : null;
        if (hipRb == null && rbs.Length > 0)
            hipRb = rbs[0];

        if (hipRb == null)
        {
            Debug.LogError("[Teleport] No Rigidbody found.", this);
            isTeleporting = false;
            yield break;
        }

        //float feetOffset = 0;
        Vector3 finalTargetPos = targetPosition;

        if (snapToGround)
        {
            Vector3 rayStart = targetPosition + Vector3.up * 10f;

            if (
                Physics.Raycast(
                    rayStart,
                    Vector3.down,
                    out RaycastHit hit,
                    200f,
                    groundMask,
                    QueryTriggerInteraction.Ignore
                )
            )
            {
                finalTargetPos.y = hit.point.y + groundOffset + 0.5f;
            }
            else
            {
                Debug.LogWarning("[Teleport] No ground hit! Using raw position.");
            }
        }

        // 🧊 Freeze
        foreach (var rb in rbs)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (rootRb != null)
        {
            rootRb.isKinematic = true;
            rootRb.linearVelocity = Vector3.zero;
            rootRb.angularVelocity = Vector3.zero;
        }

        yield return new WaitForFixedUpdate();

        // Calculate offset between root and hip BEFORE moving anything
        Vector3 offsetFromHip = hipRb.position - transform.position;
        Vector3 newRootPos = finalTargetPos - offsetFromHip;

        // Move ALL bodies relative to root (ONLY ONCE)
        foreach (var rb in rbs)
        {
            Vector3 relative = rb.position - transform.position;
            rb.position = newRootPos + relative;

            if (targetRotation.HasValue)
                rb.rotation = targetRotation.Value;
        }

        // Sync root ONCE
        transform.position = newRootPos;

        if (targetRotation.HasValue)
            transform.rotation = targetRotation.Value;

        Physics.SyncTransforms();

        yield return new WaitForFixedUpdate();

        // Re-enable physics
        foreach (var rb in rbs)
            rb.isKinematic = false;

        if (rootRb != null)
            rootRb.isKinematic = false;

        // ── Notify remotes to clear stale snapshots and re-snap ──
        RagdollNetwork ragdollNet = GetComponent<RagdollNetwork>();
        if (ragdollNet != null)
            ragdollNet.NotifyRemotesOfTeleport();

        isTeleporting = false;
    }

    private float EstimateFeetOffset(Rigidbody hipRb)
    {
        float hipY = hipRb.position.y;
        float lowestY = hipY;
        bool found = false;

        foreach (var c in GetComponentsInChildren<Collider>(true))
        {
            if (!c || !c.enabled || c.isTrigger)
                continue;

            float minY = c.bounds.min.y;
            if (!found || minY < lowestY)
            {
                lowestY = minY;
                found = true;
            }
        }

        if (!found)
            return 1f;
        return Mathf.Max(0.1f, hipY - lowestY);
    }
}
