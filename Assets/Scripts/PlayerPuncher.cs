using System.Collections.Generic;
using EZCameraShake;
using Photon.Pun;
using UnityEngine;

public class PlayerPuncher : MonoBehaviourPunCallbacks
{
    public BasePlayer player;
    private KillerType killerType;
    private RoundManager roundManager;

    [Header("Punch Settings")]
    [SerializeField]
    private float hitCooldownSeconds = 0.35f;
    private float lastHitTime = -999f;

    public PoseHandler armHandler;

    private HashSet<PhotonView> hitThisSwing = new HashSet<PhotonView>();

    private void Start()
    {
        if (player == null)
            player = GetComponentInParent<BasePlayer>() ?? GetComponent<BasePlayer>();

        if (armHandler == null && player != null)
            armHandler = player.GetComponentInChildren<PoseHandler>();

        if (armHandler != null)
        {
            armHandler.onArmHandle += Punch;
        }

        roundManager = FindFirstObjectByType<RoundManager>();
        if (roundManager != null)
        {
            roundManager.OnRoundStart += RoundStarted;
        }
    }

    private void OnDestroy()
    {
        if (armHandler != null)
        {
            armHandler.onArmHandle -= Punch;
        }

        if (roundManager != null)
        {
            roundManager.OnRoundStart -= RoundStarted;
        }
    }

    private void RoundStarted()
    {
        if (player != null)
            killerType = player.GetEquippedKiller();
    }

    private Transform GetCameraTransform()
    {
        if (player != null)
        {
            Camera playerCam = player.GetComponentInChildren<Camera>();
            if (playerCam != null)
                return playerCam.transform;

            Transform tr = player.transform.Find("CameraHead/Cam/ShakePivot/C");
            if (tr != null)
                return tr;

            tr = player.transform.Find("CameraHead/Cam/C");
            if (tr != null)
                return tr;
        }

        if (Camera.main != null)
            return Camera.main.transform;

        return null;
    }

    private void Punch()
    {
        if (!photonView.IsMine)
            return;

        if (player == null)
            player = GetComponentInParent<BasePlayer>() ?? GetComponent<BasePlayer>();

        if (player != null && player.GetPlayerType() != BasePlayer.PlayerType.Killer)
            return;

        if (killerType == null && player != null)
            killerType = player.GetEquippedKiller();

        if (killerType == null)
            return;

        if (Time.time - lastHitTime < hitCooldownSeconds)
            return;

        lastHitTime = Time.time;
        hitThisSwing.Clear(); // reset per punch

        Transform cam = GetCameraTransform();
        if (cam == null)
            return;

        Vector3 size = killerType.punchHitboxSize;
        Vector3 center = cam.position + cam.forward * (size.z / 2f + 0.6f);
        Quaternion rotation = Quaternion.Euler(0f, cam.eulerAngles.y, 0f);

        int playerMask = LayerMask.GetMask("Player");
        Collider[] hits = Physics.OverlapBox(
            center,
            size / 2f,
            rotation,
            playerMask
        );

        bool landedAnyHit = false;

        foreach (var hit in hits)
        {
            PlayerHitboxManager target = hit.GetComponentInParent<PlayerHitboxManager>();
            if (target == null)
                continue;

            PhotonView targetPV = target.photonView;
            if (targetPV == null || targetPV.IsMine)
                continue;

            // prevent multi-hit from multiple colliders on same player
            if (hitThisSwing.Contains(targetPV))
                continue;
            hitThisSwing.Add(targetPV);

            // only hit survivors
            if (
                target.basePlayer != null
                && target.basePlayer.GetPlayerType() != BasePlayer.PlayerType.Survivor
            )
                continue;

            landedAnyHit = true;
            int damage = killerType.PunchDamage;

            BasePlayerStats targetStats = target.GetComponent<BasePlayerStats>();
            bool isKillingBlow =
                targetStats != null && targetStats.health - damage <= 0 && targetStats.health > 0;

            targetPV.RPC(nameof(PlayerHitboxManager.RPC_OnHit), RpcTarget.All, damage);

            // Apply killer-specific status effect on punch impact
            StatusEffectManager targetFx = target.GetComponent<StatusEffectManager>();
            if (targetFx != null)
            {
                if (killerType != null && killerType._Name.Contains("Glitch"))
                {
                    targetPV.RPC(nameof(StatusEffectManager.RPC_ApplyStatusEffect), RpcTarget.All, (byte)StatusEffectType.GlitchDistortion, (byte)1, 3.5f);
                }
                else if (killerType != null && killerType._Name.Contains("Titan"))
                {
                    targetPV.RPC(nameof(StatusEffectManager.RPC_ApplyStatusEffect), RpcTarget.All, (byte)StatusEffectType.Tremor, (byte)1, 2.5f);
                }
                else
                {
                    targetPV.RPC(nameof(StatusEffectManager.RPC_ApplyStatusEffect), RpcTarget.All, (byte)StatusEffectType.HeavyLegs, (byte)1, 1.5f);
                }
            }

            Debug.Log($"[Punch] Hit {targetPV.Owner.NickName} for {damage}");

            // Award the local killer client when this hit eliminates a survivor
            if (isKillingBlow)
            {
                CubeCoinRewardManager rewardManager = player != null
                    ? player.GetComponent<CubeCoinRewardManager>()
                    : GetComponent<CubeCoinRewardManager>();
                rewardManager?.OnKilledSurvivor();

                CUBE.Achievements.GameplayEventBus.Publish(
                    new CUBE.Achievements.GameplayEvent(CUBE.Achievements.GameplayEventType.PlayerKilledEnemy)
                );
            }
        }

        if (landedAnyHit)
        {
            // Attacker impact feedback: shake killer's camera & play punch impact sound
            CameraShaker shaker = player != null
                ? player.GetComponentInChildren<CameraShaker>()
                : GetComponentInChildren<CameraShaker>();
            shaker?.ShakeOnce(1.2f, 5f, 0.05f, 0.2f);

            PlayHitSound(center);
        }
    }

    private void PlaySwingSound(Vector3 position)
    {
        AudioClip swing = Resources.Load<AudioClip>("SFX/punch_swing");
        if (swing != null)
        {
            AudioSource.PlayClipAtPoint(swing, position);
        }
    }

    private void PlayHitSound(Vector3 position)
    {
        AudioClip hit = Resources.Load<AudioClip>("SFX/punch_hit");
        if (hit != null)
        {
            AudioSource.PlayClipAtPoint(hit, position);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Transform cam = GetCameraTransform();
        if (cam == null)
            return;

        Vector3 size = killerType != null ? killerType.punchHitboxSize : Vector3.one;

        Vector3 center = cam.position + cam.forward * (size.z / 2f + 0.6f);
        Quaternion rotation = Quaternion.Euler(0f, cam.eulerAngles.y, 0f);

        Gizmos.color = Color.red;
        Gizmos.matrix = Matrix4x4.TRS(center, rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, size);
    }
}
