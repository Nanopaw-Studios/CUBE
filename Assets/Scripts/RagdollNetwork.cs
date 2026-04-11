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
        public Vector3 hipPos;
        public Quaternion hipRot;
        public Vector3[] bonePos;
        public Quaternion[] boneRot;
        public double time;
    }

    readonly List<Snapshot> buffer = new List<Snapshot>();
    const int MAX_BUFFER = 32;

    [Header("Smoothing")]
    public float interpolationBackTime = 0.15f;
    public float teleportThreshold = 3f;
    public float maxExtrapolationTime = 0.15f;

    bool remoteInitialized;

    Vector3 extrapolatedHipVelocity;
    Quaternion lastExtrapolatedHipRot;

    // World-space offset from the root transform to the hip bone at rest pose.
    // Computed in Awake from the prefab's default pose before Photon moves the object.
    // Must remain valid after teleports — TeleportTo keeps this invariant by also
    // moving transform.position so hip.localPosition never drifts.
    Vector3 cachedHipOffset;

    void Awake()
    {
        pv = GetComponent<PhotonView>();
        if (keyBones == null) keyBones = new List<Transform>();

        // Awake runs before Photon repositions the object, so this captures
        // the clean skeletal offset from the prefab's rest pose. Both local and
        // remote clients compute the same value since they use the same prefab.
        cachedHipOffset = hip.position - transform.position;
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

    void Update()
    {
        if (pv.IsMine || !remoteInitialized || buffer.Count == 0) return;

        double renderTime = PhotonNetwork.Time - interpolationBackTime;

        while (buffer.Count >= 2 && buffer[1].time <= renderTime)
            buffer.RemoveAt(0);

        if (buffer.Count >= 2)
        {
            Snapshot a = buffer[0];
            Snapshot b = buffer[1];

            double length = b.time - a.time;
            float t = length > 0.0001
                ? Mathf.Clamp01((float)((renderTime - a.time) / length))
                : 1f;

            if ((hip.position - b.hipPos).sqrMagnitude > teleportThreshold * teleportThreshold)
            {
                ApplySnapshot(b);
                buffer.Clear();
                return;
            }

            if (length > 0.0001)
                extrapolatedHipVelocity = (b.hipPos - a.hipPos) / (float)length;

            Vector3 hipPos = Vector3.Lerp(a.hipPos, b.hipPos, t);
            Quaternion hipRot = Quaternion.Slerp(a.hipRot, b.hipRot, t);

            ApplyInterpolatedPose(hipPos, hipRot, a, b, t);
        }
        else
        {
            Snapshot s = buffer[0];

            float dt = Mathf.Clamp(
                (float)(PhotonNetwork.Time - s.time),
                0f,
                maxExtrapolationTime
            );

            Vector3 hipPos = s.hipPos + extrapolatedHipVelocity * dt;

            if ((hip.position - hipPos).sqrMagnitude > teleportThreshold * teleportThreshold)
            {
                ApplySnapshot(s);
                return;
            }

            ApplyInterpolatedPose(hipPos, s.hipRot, s, s, 0f);
        }
    }

    void ApplySnapshot(Snapshot s)
    {
        transform.position = s.hipPos - cachedHipOffset;
        hip.rotation = s.hipRot;

        int boneCount = Mathf.Min(keyBones.Count, s.bonePos.Length);
        for (int i = 0; i < boneCount; i++)
        {
            if (keyBones[i] == null) continue;
            keyBones[i].localPosition = s.bonePos[i];
            keyBones[i].localRotation = s.boneRot[i];
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(hip.position);
            stream.SendNext(hip.rotation);
            stream.SendNext(keyBones.Count);

            foreach (Transform bone in keyBones)
            {
                stream.SendNext(bone != null ? bone.localPosition : Vector3.zero);
                stream.SendNext(bone != null ? bone.localRotation : Quaternion.identity);
            }
        }
        else
        {
            Snapshot s = new Snapshot();
            s.time = info.SentServerTime;
            s.hipPos = (Vector3)stream.ReceiveNext();
            s.hipRot = (Quaternion)stream.ReceiveNext();

            int count = (int)stream.ReceiveNext();
            s.bonePos = new Vector3[count];
            s.boneRot = new Quaternion[count];

            for (int i = 0; i < count; i++)
            {
                s.bonePos[i] = (Vector3)stream.ReceiveNext();
                s.boneRot[i] = (Quaternion)stream.ReceiveNext();
            }

            if (buffer.Count >= MAX_BUFFER)
                buffer.RemoveAt(0);

            buffer.Add(s);

            if (!remoteInitialized)
            {
                ApplySnapshot(s);
                lastExtrapolatedHipRot = s.hipRot;
                remoteInitialized = true;
            }
        }
    }

    void ApplyInterpolatedPose(
        Vector3 targetHipPos,
        Quaternion targetHipRot,
        Snapshot a,
        Snapshot b,
        float t)
    {
        transform.position = targetHipPos - cachedHipOffset;

        hip.rotation = targetHipRot;
        lastExtrapolatedHipRot = hip.rotation;

        int boneCount = Mathf.Min(
            keyBones.Count,
            Mathf.Min(a.bonePos.Length, b.bonePos.Length)
        );

        for (int i = 0; i < boneCount; i++)
        {
            if (keyBones[i] == null) continue;

            keyBones[i].localPosition =
                Vector3.Lerp(a.bonePos[i], b.bonePos[i], t);

            keyBones[i].localRotation =
                Quaternion.Slerp(a.boneRot[i], b.boneRot[i], t);
        }
    }

    /// <summary>
    /// Teleports the ragdoll to worldPosition (targeting the hip). Also moves
    /// transform.position by the same delta so hip.localPosition stays constant —
    /// this prevents a double-offset on remote clients when bone localPositions
    /// are serialized after the teleport.
    /// </summary>
    public void TeleportTo(Vector3 worldPosition)
    {
        if (!pv.IsMine) return;

        Vector3 delta = worldPosition - hip.position;

        // Keep root anchor in sync with the rigidbodies so cachedHipOffset
        // stays valid and bone localPositions don't drift.
        transform.position += delta;

        foreach (Rigidbody rb in GetComponentsInChildren<Rigidbody>())
        {
            rb.position += delta;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        pv.RPC(nameof(RPC_OnTeleport), RpcTarget.Others);
        PhotonNetwork.SendAllOutgoingCommands();
    }

    [PunRPC]
    void RPC_OnTeleport()
    {
        buffer.Clear();
        remoteInitialized = false;
    }

    public void NotifyRemotesOfTeleport()
    {
        if (!pv.IsMine) return;
        PhotonNetwork.SendAllOutgoingCommands();
        pv.RPC(nameof(RPC_OnTeleport), RpcTarget.Others);
    }

    void SetKinematic(bool state)
    {
        foreach (Rigidbody rb in GetComponentsInChildren<Rigidbody>())
        {
            rb.isKinematic = state;
            rb.interpolation = state
                ? RigidbodyInterpolation.None
                : RigidbodyInterpolation.Interpolate;
        }
    }
}
