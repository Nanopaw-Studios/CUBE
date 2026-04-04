using Photon.Pun;
using System.Collections.Generic;
using UnityEngine;

public class PlayerPuncher : MonoBehaviourPunCallbacks
{
    public BasePlayer player;
    private KillerType killerType;
    private RoundManager roundManager;

    [Header("Punch Settings")]
    [SerializeField] private float hitCooldownSeconds = 0.35f;
    private float lastHitTime = -999f;

    public PoseHandler armHandler;

    private HashSet<PhotonView> hitThisSwing = new HashSet<PhotonView>();

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
        hitThisSwing.Clear(); // reset per punch

        Transform cam = player.transform.Find("CameraHead")?.Find("Cam")?.Find("C");
        if (cam == null) return;

        Vector3 size = killerType.punchHitboxSize;

        Vector3 center = cam.position + cam.forward * (size.z / 2f + 0.6f);
        Quaternion rotation = Quaternion.Euler(0f, cam.eulerAngles.y, 0f);

        Collider[] hits = Physics.OverlapBox(
            center,
            size / 2f,
            rotation,
            LayerMask.GetMask("Player")
        );

        foreach (var hit in hits)
        {
            PlayerHitboxManager target = hit.GetComponentInParent<PlayerHitboxManager>();
            if (target == null) continue;

            PhotonView targetPV = target.photonView;
            if (targetPV == null || targetPV.IsMine) continue;

            // prevent multi-hit from multiple colliders
            if (hitThisSwing.Contains(targetPV)) continue;
            hitThisSwing.Add(targetPV);

            // only hit survivors
            if (target.basePlayer != null &&
                target.basePlayer.GetPlayerType() != BasePlayer.PlayerType.Survivor)
                continue;

            int damage = killerType.PunchDamage;

            targetPV.RPC(nameof(PlayerHitboxManager.RPC_OnHit), RpcTarget.All, damage);

            Debug.Log($"[Punch] Hit {targetPV.Owner.NickName} for {damage}");
        }
    }

    // OPTIONAL: visualize hitbox
    private void OnDrawGizmosSelected()
    {
        if (player == null) return;

        Transform cam = player.transform.Find("CameraHead")?.Find("Cam")?.Find("C");
        if (cam == null) return;

        Vector3 size = killerType != null ? killerType.punchHitboxSize : Vector3.one;

        Vector3 center = cam.position + cam.forward * (size.z / 2f + 0.6f);
        Quaternion rotation = Quaternion.Euler(0f, cam.eulerAngles.y, 0f);

        Gizmos.color = Color.red;
        Gizmos.matrix = Matrix4x4.TRS(center, rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, size);
    }
}