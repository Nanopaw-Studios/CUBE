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

    private void Start()
    {
        data = transform.parent.parent.GetComponentInChildren<Rig>();

        // 🔥 CRITICAL: ensure we always have a pose
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

        // Debug to confirm it's running
        // Debug.Log("Running pose: " + CurrentTargetPose.name);

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
                photonView.RPC(nameof(RPC_SpawnWeapon), RpcTarget.All, currentPoseIndex);
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

            Follow(i, pose);
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

        currentPoseIndex = index;
        CurrentTargetPose = AllPoses[index];

        //Debug.Log("SetPose → " + CurrentTargetPose.name);

        if (photonView.IsMine)
        {
            photonView.RPC(nameof(RPC_SetPose), RpcTarget.Others, index);
        }
    }

    [PunRPC]
    void RPC_SetPose(int index)
    {
        if (AllPoses == null || index >= AllPoses.Length) return;

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

        photonView.RPC(nameof(RPC_PlayAnimation), RpcTarget.Others, poseIndexes, timeBetween);
    }

    [PunRPC]
    void RPC_PlayAnimation(int[] poseIndexes, float timeBetween)
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

    private void Follow(Rigidbody rig, Transform target)
    {
        Vector3 forward = target.forward;
        Vector3 up = target.up;

        Vector3 torque =
            (0f - Vector3.Angle(forward, rig.transform.forward)) *
            Vector3.Cross(forward, rig.transform.forward).normalized +
            (0f - Vector3.Angle(up, rig.transform.up)) *
            Vector3.Cross(up, rig.transform.up).normalized;

        rig.AddTorque(Time.fixedDeltaTime * data.control * 200f * rotP * torque, ForceMode.Acceleration);

        rig.angularVelocity -= rig.angularVelocity * drag * control;
        rig.linearVelocity -= rig.linearVelocity * drag * control;
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