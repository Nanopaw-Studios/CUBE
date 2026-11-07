using System;
using System.Collections;
using UnityEngine;
using Photon.Pun;

public class PoseHandler : MonoBehaviourPun, IPunObservable
{
    public PosePreset RestPose;
    public PosePreset CurrentTargetPose;

    public Transform RotationObject;
    public Transform CameraObject;

    public GameObject SpawnedObject;
    public bool WeaponSpawned = false;

    public PosePreset[] AllPoses;

    private int currentPoseIndex = 0;

    Rig data;

    public float drag = 0.05f;
    public float control = 1f;

    public bool DoingAnimation = false;
    public Action onArmHandle;

    public float rotP = 30f;

    [Header("Pose Interpolation")]
    [Tooltip("If true, smooth spherical interpolation (Slerp) is applied between poses. Keep disabled for crisp, responsive ragdoll arms.")]
    public bool usePoseInterpolation = false;

    [Tooltip("Speed of pose interpolation. Higher is faster/crisper, lower is softer/smoother.")]
    [Range(5f, 40f)]
    public float poseInterpolationSpeed = 16f;

    private Quaternion[] currentTargetRotations;
    private bool[] targetRotationsInitialized;

    // Reference to the relay on the root, used to send RPCs via the root PhotonView.
    private PoseHandlerNetworkRelay relay;

    private void Awake()
    {
        // Clone pose presets at runtime if they are asset references,
        // so rotating them in FixedUpdate never modifies or dirties project assets on disk.
        if (AllPoses != null && AllPoses.Length > 0)
        {
            var runtimePoses = new PosePreset[AllPoses.Length];
            for (int i = 0; i < AllPoses.Length; i++)
            {
                if (AllPoses[i] != null)
                {
                    if (!AllPoses[i].gameObject.scene.IsValid())
                    {
                        var copy = Instantiate(AllPoses[i].gameObject, transform, false);
                        copy.name = AllPoses[i].name + "_RuntimeInstance";
                        SanitizePoseInstance(copy);
                        runtimePoses[i] = copy.GetComponent<PosePreset>();
                    }
                    else
                    {
                        runtimePoses[i] = AllPoses[i];
                    }
                }
            }
            AllPoses = runtimePoses;
        }

        if (RestPose != null)
        {
            if (AllPoses != null && AllPoses.Length > 0 && AllPoses[0] != null)
            {
                RestPose = AllPoses[0];
            }
            else if (!RestPose.gameObject.scene.IsValid())
            {
                var copy = Instantiate(RestPose.gameObject, transform, false);
                copy.name = RestPose.name + "_RestInstance";
                SanitizePoseInstance(copy);
                RestPose = copy.GetComponent<PosePreset>();
            }
        }
    }

    /// <summary>
    /// Strips joints, rigidbodies, colliders, and renderers from an instantiated pose preset so it behaves purely
    /// as an invisible kinematic Transform reference hierarchy. This prevents spawning visible ghost ragdolls or
    /// dynamic physics bodies at (0,0,0) in world space!
    /// </summary>
    private void SanitizePoseInstance(GameObject instance)
    {
        if (instance == null) return;

        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;

        foreach (var rend in instance.GetComponentsInChildren<Renderer>(true))
        {
            DestroyImmediate(rend);
        }
        foreach (var filter in instance.GetComponentsInChildren<MeshFilter>(true))
        {
            DestroyImmediate(filter);
        }
        foreach (var joint in instance.GetComponentsInChildren<ConfigurableJoint>(true))
        {
            DestroyImmediate(joint);
        }
        foreach (var rb in instance.GetComponentsInChildren<Rigidbody>(true))
        {
            DestroyImmediate(rb);
        }
        foreach (var col in instance.GetComponentsInChildren<Collider>(true))
        {
            DestroyImmediate(col);
        }
    }

    private void Start()
    {
        data = transform.parent.parent.GetComponentInChildren<Rig>();

        if (data != null && data.allRigs != null)
        {
            currentTargetRotations = new Quaternion[data.allRigs.Count];
            targetRotationsInitialized = new bool[data.allRigs.Count];
        }

        // Locate the relay on the root (walks up to root automatically since we
        // GetComponentInParent from any depth).
        relay = GetComponentInParent<PoseHandlerNetworkRelay>(true);
        if (relay == null)
        {
            Debug.LogWarning("[PoseHandler] PoseHandlerNetworkRelay not found on root. RPCs will not be sent.");
        }

        // Ensure we always have a pose
        if (RestPose != null)
        {
            CurrentTargetPose = RestPose;
        }
        else if (AllPoses != null && AllPoses.Length > 0)
        {
            CurrentTargetPose = AllPoses[0];
        }

        currentPoseIndex = 0;

        Debug.Log("PoseHandler initialized with pose: " + CurrentTargetPose?.name);
    }

    private void FixedUpdate()
    {
        if (!photonView.IsMine) return;

        if (CurrentTargetPose == null)
        {
            Debug.LogWarning("CurrentTargetPose is NULL");
            return;
        }

        HandleWeapon();

        if (RotationObject != null)
        {
            CurrentTargetPose.transform.rotation = RotationObject.rotation;
        }

        if (CurrentTargetPose.LetHeadMovement && CameraObject != null)
        {
            CurrentTargetPose.transform.rotation =
                Quaternion.Euler(CameraObject.rotation.eulerAngles.x,
                                 RotationObject.rotation.eulerAngles.y,
                                 RotationObject.rotation.eulerAngles.z);
        }

        DoPose(CurrentTargetPose);
    }

    void HandleWeapon()
    {
        if (CurrentTargetPose == null) return;

        CurrentTargetPose.gameObject.TryGetComponent(out WeaponPosePreset preset);

        if (preset != null)
        {
            if (!WeaponSpawned)
            {
                if (relay != null)
                    relay.photonView.RPC(nameof(PoseHandlerNetworkRelay.Relay_SetPose), RpcTarget.All, currentPoseIndex);
            }
        }
        else
        {
            if (SpawnedObject)
            {
                photonView.RPC(nameof(RPC_DestroyWeapon), RpcTarget.All);
            }
        }
    }

    [PunRPC]
    void RPC_SpawnWeapon(int poseIndex)
    {
        if (AllPoses == null || poseIndex >= AllPoses.Length) return;

        var preset = AllPoses[poseIndex].GetComponent<WeaponPosePreset>();
        if (preset == null) return;

        if (SpawnedObject == null && data.allRigs.Count > 8)
        {
            SpawnedObject = Instantiate(preset.Weapon.gameObject, data.allRigs[8].transform);

            SpawnedObject.transform.localPosition = preset.PositionOffset;
            SpawnedObject.transform.localRotation = Quaternion.Euler(preset.RotationOffset);

            WeaponSpawned = true;
        }
    }

    [PunRPC]
    void RPC_DestroyWeapon()
    {
        if (SpawnedObject)
        {
            Destroy(SpawnedObject);
            WeaponSpawned = false;
        }
    }

    void DoPose(PosePreset p)
    {
        int num = -1;

        foreach (Rigidbody i in data.allRigs)
        {
            num++;

            // Skip if not enabled
            if (!p.IsEnabled(num)) continue;

            Transform pose = p.GetPose(num);

            if (pose == null)
            {
                Debug.LogWarning($"Enabled bone {num} is NULL in {p.name}");
                continue;
            }

            Follow(i, pose, num);
        }
    }

    public void ResetInterpolation()
    {
        if (targetRotationsInitialized != null)
        {
            for (int i = 0; i < targetRotationsInitialized.Length; i++)
                targetRotationsInitialized[i] = false;
        }
    }

    public void SetPose(int index)
    {
        if (AllPoses == null || AllPoses.Length == 0)
        {
            Debug.LogError("AllPoses is EMPTY!");
            return;
        }

        if (index < 0 || index >= AllPoses.Length)
        {
            Debug.LogError($"Invalid pose index: {index}");
            return;
        }

        ApplyPoseLocally(index);

        if (photonView.IsMine && relay != null)
        {
            relay.photonView.RPC(nameof(PoseHandlerNetworkRelay.Relay_SetPose), RpcTarget.Others, index);
        }
    }

    /// <summary>
    /// Applies a pose locally without sending any RPC. Called by both SetPose and the relay.
    /// </summary>
    public void ApplyPoseLocally(int index)
    {
        if (AllPoses == null || index < 0 || index >= AllPoses.Length) return;
        currentPoseIndex = index;
        CurrentTargetPose = AllPoses[index];
    }

    public void Animation(int[] poseIndexes, float timeBetween)
    {
        if (!photonView.IsMine) return;

        if (poseIndexes == null || poseIndexes.Length == 0)
        {
            Debug.LogError("Animation called with EMPTY poseIndexes!");
            return;
        }

        StopAllCoroutines();
        StartCoroutine(AnimationSequence(poseIndexes, timeBetween));

        if (relay != null)
        {
            relay.photonView.RPC(nameof(PoseHandlerNetworkRelay.Relay_PlayAnimation), RpcTarget.Others, poseIndexes, timeBetween);
        }
    }

    /// <summary>
    /// Plays an animation locally without sending any RPC. Called by the relay.
    /// </summary>
    public void PlayAnimationLocally(int[] poseIndexes, float timeBetween)
    {
        StopAllCoroutines();
        StartCoroutine(AnimationSequence(poseIndexes, timeBetween));
    }

    IEnumerator AnimationSequence(int[] poses, float timeBetween)
    {
        DoingAnimation = true;

        foreach (int index in poses)
        {
            SetPose(index);
            yield return new WaitForSeconds(timeBetween);
        }

        SetPose(0); // RestPose assumed index 0
        onArmHandle?.Invoke();
        DoingAnimation = false;
    }

    private void Follow(Rigidbody rig, Transform target, int boneIndex)
    {
        Quaternion targetRot = target.rotation;

        if (usePoseInterpolation)
        {
            // Pose interpolation operates in local space relative to the pose root.
            // This ensures turning/looking/dashing rotates the arm targets INSTANTLY with the body,
            // while pose-to-pose transitions (e.g. idle -> punch -> idle) remain buttery smooth without
            // lagging behind in world space or pulling arms behind the back!
            Transform rootT = (CurrentTargetPose != null) ? CurrentTargetPose.transform : (RotationObject != null ? RotationObject : transform);
            Quaternion rootRot = rootT.rotation;
            Quaternion targetLocalRot = Quaternion.Inverse(rootRot) * targetRot;

            if (currentTargetRotations == null || boneIndex >= currentTargetRotations.Length)
            {
                int count = data != null && data.allRigs != null ? data.allRigs.Count : boneIndex + 1;
                Array.Resize(ref currentTargetRotations, count);
                Array.Resize(ref targetRotationsInitialized, count);
            }

            if (!targetRotationsInitialized[boneIndex])
            {
                currentTargetRotations[boneIndex] = targetLocalRot;
                targetRotationsInitialized[boneIndex] = true;
            }
            else
            {
                currentTargetRotations[boneIndex] = Quaternion.Slerp(
                    currentTargetRotations[boneIndex],
                    targetLocalRot,
                    1f - Mathf.Exp(-poseInterpolationSpeed * Time.fixedDeltaTime)
                );
            }

            targetRot = rootRot * currentTargetRotations[boneIndex];
        }

        Vector3 forward = targetRot * Vector3.forward;
        Vector3 up = targetRot * Vector3.up;

        Vector3 crossForward = Vector3.Cross(forward, rig.transform.forward);
        Vector3 crossUp = Vector3.Cross(up, rig.transform.up);

        Vector3 torque = Vector3.zero;
        if (crossForward.sqrMagnitude > 1e-6f)
        {
            torque += (0f - Vector3.Angle(forward, rig.transform.forward)) * crossForward.normalized;
        }
        if (crossUp.sqrMagnitude > 1e-6f)
        {
            torque += (0f - Vector3.Angle(up, rig.transform.up)) * crossUp.normalized;
        }

        rig.AddTorque(Time.fixedDeltaTime * data.control * 200f * rotP * torque, ForceMode.Acceleration);

        // Damp angular and linear velocity relative to the torso so moving, sprinting, or turning
        // does not drag the arms backward or float away from the body.
        Rigidbody torsoRb = (data != null && data.torso != null) ? data.torso.GetComponent<Rigidbody>() : null;
        if (torsoRb != null)
        {
            Vector3 relAngVel = rig.angularVelocity - torsoRb.angularVelocity;
            rig.angularVelocity -= relAngVel * drag * control;

            Vector3 relLinearVel = rig.linearVelocity - torsoRb.linearVelocity;
            rig.linearVelocity -= relLinearVel * drag * control;
        }
        else
        {
            rig.angularVelocity -= rig.angularVelocity * drag * control;
            rig.linearVelocity -= rig.linearVelocity * drag * control;
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(currentPoseIndex);
        }
        else
        {
            int index = (int)stream.ReceiveNext();

            if (!photonView.IsMine && AllPoses != null && index < AllPoses.Length)
            {
                CurrentTargetPose = AllPoses[index];
            }
        }
    }
}