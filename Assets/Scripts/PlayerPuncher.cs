using Photon.Pun;
using System;
using UnityEngine;

public class PlayerPuncher : MonoBehaviourPunCallbacks
{
    public BasePlayer player;
    private KillerType killerType;
    private RoundManager roundManager;
    [SerializeField] private float hitCooldownSeconds = 0.35f;
    private float lastHitTime = -999f;

    public PoseHandler armHandler;

    private void Start()
    {
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

    private void RoundStarted()
    {
        killerType = player.GetEquippedKiller();
    }

    private void Punch()
    {
        if (!photonView.IsMine || killerType == null) return;
        if (Time.time - lastHitTime < hitCooldownSeconds) return;
        lastHitTime = Time.time;

        Vector3 size = killerType.punchHitboxSize;

        Transform hips = player.transform.Find("CameraHead").Find("Cam").Find("C");
        if (hips == null) return;

        // base center is slightly in front of hips
        Vector3 center = hips.position + hips.forward * (size.z / 2f + 0.6f);
        Quaternion yRotation = Quaternion.Euler(0f, hips.eulerAngles.y, 0f);
       
        // new hitbox system
        Collider[] hits = Physics.OverlapBox(center, size / 2f, yRotation, LayerMask.GetMask("Player"));
        foreach (var hit in hits)
        {
            PlayerHitboxManager targetHitbox = hit.GetComponentInParent<PlayerHitboxManager>();
            if (targetHitbox == null || targetHitbox.photonView.IsMine) continue;
            BasePlayerStats targetStats = targetHitbox.GetComponent<BasePlayerStats>();
            if (targetStats == null || targetStats.photonView == null) continue;

            // Damage is applied through the target's own PhotonView so ownership and state stay consistent.
            targetStats.photonView.RPC(nameof(BasePlayerStats.TakeDamage), RpcTarget.All, killerType.PunchDamage);
        }
    }
}
