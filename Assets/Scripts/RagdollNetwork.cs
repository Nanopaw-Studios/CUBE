using UnityEngine;
using Photon.Pun;
using System.Collections.Generic;

[RequireComponent(typeof(PhotonView))]
public class RagdollNetwork : MonoBehaviour, IPunObservable
{
    // ─── Setup ───────────────────────────────────────────────────
    [Header("Setup")]
    public Transform       hip;
    public List<Transform> keyBones;

    PhotonView pv;

    // ─── Snapshot ────────────────────────────────────────────────
    struct Snapshot
    {
        public Vector3      hipPos;
        public Quaternion   hipRot;
        public Vector3      hipVel;      // estimated hip velocity (world-space)
        public Vector3[]    bonePos;     // local-space
        public Quaternion[] boneRot;
        public Vector3[]    boneVel;     // local-space velocity per bone
        public double       time;
    }

    readonly List<Snapshot> buffer    = new List<Snapshot>();
    const    int            MAX_BUFFER = 32;

    // ─── Smoothing ───────────────────────────────────────────────
    [Header("Smoothing")]
    [Tooltip("Base interpolation delay (seconds). Adapts automatically to jitter.")]
    public float interpolationBackTime = 0.10f;

    [Tooltip("How quickly the adaptive delay chases actual jitter. Lower = faster.")]
    public float jitterAdaptSpeed = 4f;

    [Tooltip("Above this distance the ragdoll fast-blends instead of hard-snapping.")]
    public float teleportThreshold = 3f;

    [Tooltip("Speed used to blend out a teleport mismatch (units/s). 0 = instant snap.")]
    public float teleportBlendSpeed = 12f;

    [Tooltip("Extra positional smoothing applied to the root each frame (0 = none).")]
    [Range(0f, 20f)]
    public float rootDamping = 8f;

    // ─── Runtime state ───────────────────────────────────────────
    // Adaptive back-time tracks observed packet spacing + jitter.
    float   adaptedBackTime;
    double  lastPacketTime;
    float   observedJitter;   // one-sided EWMA of arrival jitter

    // Smoothed root position used to damp the final transform move.
    Vector3 smoothedHipTarget;
    bool    smoothedHipTargetSet;

    // True once the first snapshot has been applied on remote clients.
    bool remoteInitialized;

    // ─────────────────────────────────────────────────────────────
    void Awake()
    {
        pv = GetComponent<PhotonView>();
        if (keyBones == null) keyBones = new List<Transform>();
        adaptedBackTime = interpolationBackTime;
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

        double interpTime = PhotonNetwork.Time - adaptedBackTime;

        // Discard snapshots that are fully behind the playhead
        while (buffer.Count >= 2 && buffer[1].time <= interpTime)
            buffer.RemoveAt(0);

        Vector3    targetHipPos;
        Quaternion targetHipRot;
        Vector3[]  targetBonePos;
        Quaternion[] targetBoneRot;

        int boneCount = keyBones.Count;

        if (buffer.Count >= 2)
        {
            Snapshot a = buffer[0];
            Snapshot b = buffer[1];
            double range = b.time - a.time;
            float  t     = range > 0.0001 ? Mathf.Clamp01((float)((interpTime - a.time) / range)) : 1f;

            targetHipPos = Vector3.Lerp(a.hipPos, b.hipPos, t);
            targetHipRot = Quaternion.Slerp(a.hipRot, b.hipRot, t);

            int safeCount = Mathf.Min(boneCount, Mathf.Min(a.bonePos.Length, b.bonePos.Length));
            targetBonePos = new Vector3[safeCount];
            targetBoneRot = new Quaternion[safeCount];
            for (int i = 0; i < safeCount; i++)
            {
                targetBonePos[i] = Vector3.Lerp(a.bonePos[i], b.bonePos[i], t);
                targetBoneRot[i] = Quaternion.Slerp(a.boneRot[i], b.boneRot[i], t);
            }
        }
        else
        {
            // Only one sample — extrapolate forward using the last estimated velocity.
            Snapshot s    = buffer[0];
            float    age  = Mathf.Clamp((float)(interpTime - s.time), 0f, 0.25f); // cap at 250 ms

            targetHipPos = s.hipPos + s.hipVel * age;
            targetHipRot = s.hipRot; // angular extrapolation rarely helps ragdolls

            int safeCount = Mathf.Min(boneCount, s.bonePos.Length);
            targetBonePos = new Vector3[safeCount];
            targetBoneRot = new Quaternion[safeCount];
            for (int i = 0; i < safeCount; i++)
            {
                targetBonePos[i] = s.bonePos[i] + (s.boneVel != null && i < s.boneVel.Length
                                                    ? s.boneVel[i] * age
                                                    : Vector3.zero);
                targetBoneRot[i] = s.boneRot[i];
            }
        }

        // ── Root position: fast-blend or smooth-damp ──────────────
        float hipError = Vector3.Distance(hip.position, targetHipPos);

        if (hipError > teleportThreshold)
        {
            // Large gap — blend at high speed rather than hard-snapping.
            float blendT = teleportBlendSpeed > 0f
                         ? Mathf.Clamp01(teleportBlendSpeed * Time.deltaTime)
                         : 1f;
            smoothedHipTarget    = Vector3.Lerp(hip.position, targetHipPos, blendT);
            smoothedHipTargetSet = true;
        }
        else if (rootDamping > 0f)
        {
            // Gentle exponential smoothing keeps things buttery without lag.
            if (!smoothedHipTargetSet) { smoothedHipTarget = targetHipPos; smoothedHipTargetSet = true; }
            smoothedHipTarget = Vector3.Lerp(smoothedHipTarget, targetHipPos,
                                             1f - Mathf.Exp(-rootDamping * Time.deltaTime));
        }
        else
        {
            smoothedHipTarget    = targetHipPos;
            smoothedHipTargetSet = true;
        }

        // Shift root so hip lands at smoothedHipTarget
        transform.position += smoothedHipTarget - hip.position;
        hip.rotation        = targetHipRot;

        // ── Bones ────────────────────────────────────────────────
        int applyCount = Mathf.Min(boneCount, targetBonePos.Length);
        for (int i = 0; i < applyCount; i++)
        {
            if (keyBones[i] == null) continue;
            keyBones[i].localPosition = targetBonePos[i];
            keyBones[i].localRotation = targetBoneRot[i];
        }
    }

    // ─────────────────────────────────────────────────────────────
    void ApplySnapshot(Snapshot s)
    {
        Vector3 delta = s.hipPos - hip.position;
        transform.position += delta;
        hip.rotation        = s.hipRot;

        int count = Mathf.Min(keyBones.Count, s.bonePos.Length);
        for (int i = 0; i < count; i++)
        {
            if (keyBones[i] == null) continue;
            keyBones[i].localPosition = s.bonePos[i];
            keyBones[i].localRotation = s.boneRot[i];
        }

        smoothedHipTarget    = s.hipPos;
        smoothedHipTargetSet = true;
    }

    // ─────────────────────────────────────────────────────────────
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // ── Owner → Network ──────────────────────────────────
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
            // ── Network → Remote ─────────────────────────────────
            Snapshot s = new Snapshot();
            s.time   = info.SentServerTime;
            s.hipPos = (Vector3)stream.ReceiveNext();
            s.hipRot = (Quaternion)stream.ReceiveNext();

            int count = (int)stream.ReceiveNext();
            s.bonePos = new Vector3[count];
            s.boneRot = new Quaternion[count];
            s.boneVel = new Vector3[count];

            for (int i = 0; i < count; i++)
            {
                s.bonePos[i] = (Vector3)stream.ReceiveNext();
                s.boneRot[i] = (Quaternion)stream.ReceiveNext();
            }

            // ── Estimate velocities from previous snapshot ───────
            if (buffer.Count > 0)
            {
                Snapshot prev = buffer[buffer.Count - 1];
                double   dt   = s.time - prev.time;

                if (dt > 0.0001)
                {
                    s.hipVel = (s.hipPos - prev.hipPos) / (float)dt;

                    int velCount = Mathf.Min(count, prev.bonePos.Length);
                    for (int i = 0; i < velCount; i++)
                        s.boneVel[i] = (s.bonePos[i] - prev.bonePos[i]) / (float)dt;
                }

                // ── Adaptive back-time: track jitter ─────────────
                // jitter = how much the arrival spacing deviates from the send rate
                double arrivalGap = PhotonNetwork.Time - lastPacketTime;
                double sendGap    = dt; // should match Photon's serialization rate
                float  jitter     = Mathf.Abs((float)(arrivalGap - sendGap));
                observedJitter    = Mathf.Lerp(observedJitter, jitter,
                                               1f - Mathf.Exp(-jitterAdaptSpeed * (float)dt));

                // Back-time = base + a safety margin over observed jitter
                adaptedBackTime = Mathf.Max(interpolationBackTime,
                                            interpolationBackTime + observedJitter * 1.5f);
            }

            lastPacketTime = PhotonNetwork.Time;

            // ── Cap buffer ───────────────────────────────────────
            if (buffer.Count >= MAX_BUFFER)
                buffer.RemoveAt(0);

            buffer.Add(s);

            // ── First-time snap ──────────────────────────────────
            if (!remoteInitialized)
            {
                ApplySnapshot(s);
                remoteInitialized = true;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────
    public void TeleportTo(Vector3 worldPosition)
    {
        if (!pv.IsMine) return;

        Vector3 delta = worldPosition - hip.position;
        foreach (Rigidbody rb in GetComponentsInChildren<Rigidbody>())
        {
            rb.position        += delta;
            rb.linearVelocity   = Vector3.zero;
            rb.angularVelocity  = Vector3.zero;
        }

        pv.RPC(nameof(RPC_OnTeleport), RpcTarget.Others);
        PhotonNetwork.SendAllOutgoingCommands();
    }

    [PunRPC]
    void RPC_OnTeleport()
    {
        buffer.Clear();
        remoteInitialized    = false;
        smoothedHipTargetSet = false;
        observedJitter       = 0f;
        adaptedBackTime      = interpolationBackTime;
    }

    public void NotifyRemotesOfTeleport()
    {
        if (!pv.IsMine) return;
        PhotonNetwork.SendAllOutgoingCommands();
        pv.RPC(nameof(RPC_OnTeleport), RpcTarget.Others);
    }

    // ─────────────────────────────────────────────────────────────
    void SetKinematic(bool state)
    {
        foreach (Rigidbody rb in GetComponentsInChildren<Rigidbody>())
        {
            rb.isKinematic   = state;
            rb.interpolation = state
                ? RigidbodyInterpolation.None
                : RigidbodyInterpolation.Interpolate;
        }
    }
}