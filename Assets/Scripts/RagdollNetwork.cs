using UnityEngine;
using Photon.Pun;
using System.Collections.Generic;

[RequireComponent(typeof(PhotonView))]
public class RagdollNetwork : MonoBehaviour, IPunObservable
{
    public Transform hip;                      // the main bone to send
    public Rigidbody hipRb;                    // hip's Rigidbody on owner
    public List<Transform> keyBones;           // chest, head, hands, feet etc. (configure in inspector)

    PhotonView pv;

    // smoothing
    Vector3 hipPosTarget;
    Quaternion hipRotTarget;
    Vector3 hipVelTarget;
    Vector3 hipAngVelTarget;

    // buffers for key bones
    Vector3[] keyBonePosTarget;
    Quaternion[] keyBoneRotTarget;

    // interpolation speeds
    public float positionLerp = 20f;
    public float rotationLerp = 20f;

    // Snap-to-target thresholds: below these values the lerp is skipped and target is applied directly,
    // preventing infinite micro-oscillation around a stable pose.
    private const float PositionSnapThreshold = 0.001f;
    private const float RotationSnapThreshold = 0.05f;

    // Send-gate thresholds: owner only broadcasts a moving=true flag when movement exceeds these values,
    // so remotes stop lerping entirely while the owner is idle.
    private const float PositionSendThreshold = 0.002f;
    private const float RotationSendThreshold = 0.1f;
    private const float VelocitySendThreshold = 0.01f;

    // Last values sent by the owner, used for change detection
    private Vector3 lastSentHipPos;
    private Quaternion lastSentHipRot;

    // Received from the owner each packet: true = owner is moving, false = owner is idle
    private bool remoteIsMoving;

    private float freezeUntilTime = 0f; // reserved for future use

    void Awake()
    {
        pv = GetComponent<PhotonView>();
        if (keyBones == null) keyBones = new List<Transform>();

        keyBonePosTarget = new Vector3[keyBones.Count];
        keyBoneRotTarget = new Quaternion[keyBones.Count];
    }

    void Start()
    {
        // initial targets
        if (hip)
        {
            hipPosTarget = hip.position;
            hipRotTarget = hip.rotation;
            lastSentHipPos = hip.position;
            lastSentHipRot = hip.rotation;
        }

        for (int i = 0; i < keyBones.Count; i++)
        {
            if (keyBones[i] != null)
            {
                keyBonePosTarget[i] = keyBones[i].localPosition;
                keyBoneRotTarget[i] = keyBones[i].localRotation;
            }
        }

        // Owner: physics active. Remotes: make physics kinematic and we will drive transforms.
        if (pv.IsMine)
            SetRagdollKinematic(false);
        else
            SetRagdollKinematic(true);
    }

    void Update()
    {
        if (pv.IsMine || !remoteIsMoving) return;

        if (hip != null)
        {
            float posDist = Vector3.Distance(hip.position, hipPosTarget);
            float rotAngle = Quaternion.Angle(hip.rotation, hipRotTarget);

            hip.position = posDist > PositionSnapThreshold
                ? Vector3.Lerp(hip.position, hipPosTarget, Time.deltaTime * positionLerp)
                : hipPosTarget;

            hip.rotation = rotAngle > RotationSnapThreshold
                ? Quaternion.Slerp(hip.rotation, hipRotTarget, Time.deltaTime * rotationLerp)
                : hipRotTarget;
        }

        for (int i = 0; i < keyBones.Count; i++)
        {
            var t = keyBones[i];
            if (t == null) continue;

            float posDist = Vector3.Distance(t.localPosition, keyBonePosTarget[i]);
            float rotAngle = Quaternion.Angle(t.localRotation, keyBoneRotTarget[i]);

            t.localPosition = posDist > PositionSnapThreshold
                ? Vector3.Lerp(t.localPosition, keyBonePosTarget[i], Time.deltaTime * positionLerp)
                : keyBonePosTarget[i];

            t.localRotation = rotAngle > RotationSnapThreshold
                ? Quaternion.Slerp(t.localRotation, keyBoneRotTarget[i], Time.deltaTime * rotationLerp)
                : keyBoneRotTarget[i];
        }
    }

    // This serializes only a few transforms/velocities.
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting && pv.IsMine)
        {
            Vector3 hipPos = hip != null ? hip.position : Vector3.zero;
            Quaternion hipRot = hip != null ? hip.rotation : Quaternion.identity;
            Vector3 linVel = hipRb != null ? hipRb.linearVelocity : Vector3.zero;
            Vector3 angVel = hipRb != null ? hipRb.angularVelocity : Vector3.zero;

            bool isMoving =
                Vector3.Distance(hipPos, lastSentHipPos) > PositionSendThreshold ||
                Quaternion.Angle(hipRot, lastSentHipRot) > RotationSendThreshold ||
                linVel.sqrMagnitude > VelocitySendThreshold * VelocitySendThreshold ||
                angVel.sqrMagnitude > VelocitySendThreshold * VelocitySendThreshold;

            stream.SendNext(isMoving);
            stream.SendNext(hipPos);
            stream.SendNext(hipRot);
            stream.SendNext(linVel);
            stream.SendNext(angVel);

            // Write key bones local transforms
            for (int i = 0; i < keyBones.Count; i++)
            {
                Transform t = keyBones[i];
                stream.SendNext(t != null ? t.localPosition : Vector3.zero);
                stream.SendNext(t != null ? t.localRotation : Quaternion.identity);
            }

            if (isMoving)
            {
                lastSentHipPos = hipPos;
                lastSentHipRot = hipRot;
            }
        }
        else if (stream.IsReading)
        {
            remoteIsMoving = (bool)stream.ReceiveNext();

            // Read hip
            hipPosTarget = (Vector3)stream.ReceiveNext();
            hipRotTarget = (Quaternion)stream.ReceiveNext();
            hipVelTarget = (Vector3)stream.ReceiveNext();
            hipAngVelTarget = (Vector3)stream.ReceiveNext();

            // Read key bones
            for (int i = 0; i < keyBones.Count; i++)
            {
                keyBonePosTarget[i] = (Vector3)stream.ReceiveNext();
                keyBoneRotTarget[i] = (Quaternion)stream.ReceiveNext();
            }
        }
    }

    // toggle all child rigidbodies kinematic (called on start depending on owner)
    void SetRagdollKinematic(bool kinematic)
    {
        Rigidbody[] rbs = GetComponentsInChildren<Rigidbody>();
        foreach (var r in rbs)
        {
            r.isKinematic = kinematic;
            r.interpolation = pv.IsMine
                ? RigidbodyInterpolation.Interpolate
                : RigidbodyInterpolation.None;
        }
    }

    /// <summary>Call on the owner to notify remotes of a physics impulse on a specific bone.</summary>
    [PunRPC]
    public void RPC_ApplyImpulse(int boneIndex, Vector3 worldForce, Vector3 worldPoint)
    {
        if (pv.IsMine) return;

        var bone = GetBoneByIndex(boneIndex);
        if (bone == null) return;

        var rb = bone.GetComponent<Rigidbody>();
        if (rb != null && !rb.isKinematic)
            rb.AddForceAtPosition(worldForce, worldPoint, ForceMode.Impulse);
    }

    Transform GetBoneByIndex(int idx)
    {
        if (idx < 0 || idx >= keyBones.Count) return null;
        return keyBones[idx];
    }


    /// <summary>
    /// Called on the owner immediately after a teleport. Resets the send-gate
    /// references so the very next serialized packet reflects the new position
    /// instead of being suppressed by the change-detection threshold.
    /// Must only be called on the owner — never touches remote bone transforms.
    /// </summary>
    public void SnapSendGate()
    {
        if (!pv.IsMine) return;

        if (hip != null)
        {
            lastSentHipPos = hip.position;
            lastSentHipRot = hip.rotation;
        }
    }

    /// <summary>
    /// Legacy entry point — kept so nothing outside breaks if still referenced.
    /// On the owner, delegates to SnapSendGate. On remotes, does nothing.
    /// </summary>
    public void ForceSnapToCurrentPose(float freezeSeconds = 0.10f)
    {
        SnapSendGate();
    }
}
