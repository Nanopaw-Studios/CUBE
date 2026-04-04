using UnityEngine;
using Photon.Pun;
using System.Collections.Generic;

[RequireComponent(typeof(PhotonView))]
public class RagdollNetwork : MonoBehaviour, IPunObservable
{
    [Header("Setup")]
    public Transform hip;
    public List<Transform> keyBones;

    PhotonView pv;

    struct Snapshot
    {
        public Vector3    hipPos;    // world-space
        public Quaternion hipRot;    // world-space
        public Vector3[]  bonePos;   // local-space (relative to parent bone)
        public Quaternion[] boneRot;
        public double     time;
    }

    readonly List<Snapshot> buffer = new List<Snapshot>();
    const int MAX_BUFFER = 32;

    [Header("Smoothing")]
    public float interpolationBackTime = 0.1f;
    public float teleportThreshold    = 3f;

    // True once the first snapshot has been applied on remote clients.
    // Prevents the T-pose flash while waiting for the second packet.
    bool remoteInitialized;

    // ─────────────────────────────────────────────────────────────
    void Awake()
    {
        pv = GetComponent<PhotonView>();
        if (keyBones == null) keyBones = new List<Transform>();
    }

    void Start()
    {
        if (!pv.IsMine)
        {
            SetKinematic(true);
            foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>())
                smr.updateWhenOffscreen = true;
        }
    }
    // ─────────────────────────────────────────────────────────────
    void Update()
    {
        if (pv.IsMine || !remoteInitialized || buffer.Count == 0) return;

        double interpTime = PhotonNetwork.Time - interpolationBackTime;

        // Discard snapshots that are now too old
        while (buffer.Count >= 2 && buffer[1].time <= interpTime)
            buffer.RemoveAt(0);

        if (buffer.Count >= 2)
        {
            Snapshot a = buffer[0];
            Snapshot b = buffer[1];
            double range = b.time - a.time;
            float t = range > 0.0001 ? Mathf.Clamp01((float)((interpTime - a.time) / range)) : 1f;

            if (Vector3.Distance(hip.position, b.hipPos) > teleportThreshold)
            {
                ApplySnapshot(b);
                buffer.Clear();
                return;
            }

            // Move root by delta so hip lands at the interpolated world position
            Vector3 targetHipPos = Vector3.Lerp(a.hipPos, b.hipPos, t);
            transform.position += targetHipPos - hip.position;

            hip.rotation = Quaternion.Slerp(a.hipRot, b.hipRot, t);

            int boneCount = Mathf.Min(keyBones.Count, Mathf.Min(a.bonePos.Length, b.bonePos.Length));
            for (int i = 0; i < boneCount; i++)
            {
                if (keyBones[i] == null) continue;
                keyBones[i].localPosition = Vector3.Lerp(a.bonePos[i], b.bonePos[i], t);
                keyBones[i].localRotation = Quaternion.Slerp(a.boneRot[i], b.boneRot[i], t);
            }
        }
        else
        {
            // Only one snapshot — extrapolate by holding the last known pose
            ApplySnapshot(buffer[0]);
        }
    }

    // ─────────────────────────────────────────────────────────────
    void ApplySnapshot(Snapshot s)
    {
        // Shift root by however much is needed so hip.position lands on s.hipPos.
        // Moving root shifts ALL children equally — hip arrives at s.hipPos without
        // being set a second time (that's what caused the T-pose before).
        Vector3 delta = s.hipPos - hip.position;
        transform.position += delta;

        // Rotation only — position is already correct from above
        hip.rotation = s.hipRot;

        int boneCount = Mathf.Min(keyBones.Count, s.bonePos.Length);
        for (int i = 0; i < boneCount; i++)
        {
            if (keyBones[i] == null) continue;
            keyBones[i].localPosition = s.bonePos[i];
            keyBones[i].localRotation = s.boneRot[i];
        }
    }

    // ─────────────────────────────────────────────────────────────
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // ── Owner → Network ──────────────────────────────────
            stream.SendNext(hip.position);   // world
            stream.SendNext(hip.rotation);   // world
            stream.SendNext(keyBones.Count); // always send count so reader never desyncs

            foreach (Transform bone in keyBones)
            {
                // Send identity for null bones so the stream length stays constant
                stream.SendNext(bone != null ? bone.localPosition : Vector3.zero);
                stream.SendNext(bone != null ? bone.localRotation : Quaternion.identity);
            }
        }
        else
        {
            // ── Network → Remote ─────────────────────────────────
            Snapshot s   = new Snapshot();
            s.time       = info.SentServerTime;
            s.hipPos     = (Vector3)stream.ReceiveNext();
            s.hipRot     = (Quaternion)stream.ReceiveNext();

            int count    = (int)stream.ReceiveNext();
            s.bonePos    = new Vector3[count];
            s.boneRot    = new Quaternion[count];

            for (int i = 0; i < count; i++)
            {
                s.bonePos[i] = (Vector3)stream.ReceiveNext();
                s.boneRot[i] = (Quaternion)stream.ReceiveNext();
            }

            // ── Cap buffer so it never grows unbounded ────────────
            if (buffer.Count >= MAX_BUFFER)
                buffer.RemoveAt(0);

            buffer.Add(s);

            // ── FIX: snap to correct world position immediately ───
            // This eliminates the float-in-air on spawn and ensures
            // the first received pose is correct before interpolation
            // has two samples to work with.
            if (!remoteInitialized)
            {
                ApplySnapshot(s);
                remoteInitialized = true;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Call on the OWNER to teleport the ragdoll and have remotes snap cleanly.
    public void TeleportTo(Vector3 worldPosition)
    {
        if (!pv.IsMine) return;

        // Shift every rigidbody by the delta so relative bone poses are preserved
        Vector3 delta = worldPosition - hip.position;
        foreach (Rigidbody rb in GetComponentsInChildren<Rigidbody>())
        {
            rb.position           += delta;
            rb.linearVelocity            = Vector3.zero;
            rb.angularVelocity     = Vector3.zero;
        }

        // Tell all remote clients to clear their stale buffer so they don't
        // interpolate from the old location to the new one.
        pv.RPC(nameof(RPC_OnTeleport), RpcTarget.Others);

        // Flush immediately so remotes get the new position ASAP
        PhotonNetwork.SendAllOutgoingCommands();
    }

    [PunRPC]
    void RPC_OnTeleport()
    {
        // Wipe stale snapshots — next received packet will snap to the
        // correct post-teleport position via the !remoteInitialized path.
        buffer.Clear();
        remoteInitialized = false;
    }

    // Add this alongside TeleportTo()
    public void NotifyRemotesOfTeleport()
    {
        if (!pv.IsMine) return;
        PhotonNetwork.SendAllOutgoingCommands(); // flush new bone positions first
        pv.RPC(nameof(RPC_OnTeleport), RpcTarget.Others);
    }

    // ─────────────────────────────────────────────────────────────
    void SetKinematic(bool state)
    {
        foreach (Rigidbody rb in GetComponentsInChildren<Rigidbody>())
        {
            rb.isKinematic  = state;
            rb.interpolation = state
                ? RigidbodyInterpolation.None
                : RigidbodyInterpolation.Interpolate;
        }
    }
}