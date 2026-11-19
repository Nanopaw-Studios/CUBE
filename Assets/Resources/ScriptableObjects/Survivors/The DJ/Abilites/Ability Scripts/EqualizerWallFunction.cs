using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

/// <summary>
/// Placed on the EqualizerWallPrefab. The wall has no physical collision (IsTrigger).
/// Any killer that enters the trigger zone is slowed; the slow is removed when they exit.
/// </summary>
public class EqualizerWallFunction : MonoBehaviourPunCallbacks
{
    public float killerSlowMultiplier = 0.4f;
    public float lifetime = 10f;

    // Track which killers are currently slowed by this wall (ViewID)
    private readonly HashSet<int> slowedKillers = new HashSet<int>();

    private void Start()
    {
        if (photonView.IsMine)
            Invoke(nameof(NetworkDestroy), lifetime);
    }

    private void Update()
    {
        if (!photonView.IsMine) return;

        // Continuously scan so we catch players who walked in while we were mid-frame
        Collider[] hits = Physics.OverlapBox(
            transform.position,
            transform.localScale * 0.5f,
            transform.rotation
        );

        var inRangeNow = new HashSet<int>();

        foreach (Collider col in hits)
        {
            BasePlayer player = col.GetComponentInParent<BasePlayer>();
            if (player == null) continue;
            if (player.GetPlayerType() != BasePlayer.PlayerType.Killer) continue;

            PhotonView pv = player.gameObject.GetPhotonView();
            if (pv == null) continue;

            inRangeNow.Add(pv.ViewID);

            if (!slowedKillers.Contains(pv.ViewID))
            {
                slowedKillers.Add(pv.ViewID);
                photonView.RPC(nameof(RPC_ApplySlow), pv.Owner, pv.ViewID, true, killerSlowMultiplier);
                CubeCoinRewardManager.NotifyLocalSuccessfulAbility("equalizer_slowed_killer", 45);
            }

        }

        // Remove slow when killer leaves the zone
        var toRemove = new List<int>();
        foreach (int viewId in slowedKillers)
        {
            if (!inRangeNow.Contains(viewId))
                toRemove.Add(viewId);
        }

        foreach (int viewId in toRemove)
        {
            slowedKillers.Remove(viewId);
            PhotonView pv = PhotonView.Find(viewId);
            if (pv != null)
                photonView.RPC(nameof(RPC_ApplySlow), pv.Owner, viewId, false, 1f);
        }
    }

    /// <summary>
    /// Sent to the killer's owner to modify only their own MovementSC.
    /// </summary>
    [PunRPC]
    private void RPC_ApplySlow(int viewId, bool on, float multiplier)
    {
        PhotonView pv = PhotonView.Find(viewId);
        if (pv == null || !pv.IsMine) return;

        StatusEffectManager sem = pv.gameObject.GetComponent<StatusEffectManager>();
        if (sem == null)
            sem = pv.gameObject.AddComponent<StatusEffectManager>();

        if (on)
        {
            sem.ApplyEffect(StatusEffectType.Equalized, 2, 999f, multiplier);
        }
        else
        {
            sem.RemoveEffect(StatusEffectType.Equalized);
        }
    }

    private void NetworkDestroy()
    {
        // Lift all active slows before destroying
        foreach (int viewId in slowedKillers)
        {
            PhotonView pv = PhotonView.Find(viewId);
            if (pv != null)
                photonView.RPC(nameof(RPC_ApplySlow), pv.Owner, viewId, false, 1f);
        }
        slowedKillers.Clear();

        if (photonView.IsMine)
            PhotonNetwork.Destroy(gameObject);
    }
}
