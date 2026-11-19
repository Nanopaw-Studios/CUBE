using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

/// <summary>
/// Placed on the MusicCirclePrefab. Tracks survivors inside the radius and applies
/// a temporary speed boost while they remain inside, removing it when they leave or
/// the circle expires.
/// </summary>
public class MusicCircleFunction : MonoBehaviourPunCallbacks
{
    public float boostDuration = 4f;
    public float boostMultiplier = 1.5f;
    public float radius = 5f;
    public float lifetime = 12f;

    // Tracks ViewIDs currently boosted so we can remove the boost on exit
    private readonly HashSet<int> boostedViewIds = new HashSet<int>();
    private readonly HashSet<int> rewardedViewIds = new HashSet<int>();


    private void Start()
    {
        if (!photonView.IsMine) return;
        Invoke(nameof(NetworkDestroy), lifetime);
    }

    private void Update()
    {
        if (!photonView.IsMine) return;

        var inRangeNow = new HashSet<int>();

        Collider[] hits = Physics.OverlapSphere(transform.position, radius);
        foreach (Collider col in hits)
        {
            BasePlayer player = col.GetComponentInParent<BasePlayer>();
            if (player == null) continue;
            if (player.GetPlayerType() != BasePlayer.PlayerType.Survivor) continue;

            PhotonView pv = player.gameObject.GetPhotonView();
            if (pv == null) continue;

            inRangeNow.Add(pv.ViewID);

            // ENTER: apply boost
            if (!boostedViewIds.Contains(pv.ViewID))
            {
                boostedViewIds.Add(pv.ViewID);
                photonView.RPC(nameof(RPC_ApplyBoost), pv.Owner, pv.ViewID, true, boostMultiplier, boostDuration);

                // Award XP only once per unique teammate boosted (or if killer is within 20m)
                if (!rewardedViewIds.Contains(pv.ViewID) && (!pv.IsMine || IsKillerNear(20f)))
                {
                    rewardedViewIds.Add(pv.ViewID);
                    CubeCoinRewardManager.NotifyLocalSuccessfulAbility("music_circle_buff", 30);
                }
            }

        }

        // EXIT: remove boost for players no longer in range
        var toRemove = new List<int>();
        foreach (int viewId in boostedViewIds)
        {
            if (!inRangeNow.Contains(viewId))
                toRemove.Add(viewId);
        }

        foreach (int viewId in toRemove)
        {
            boostedViewIds.Remove(viewId);
            PhotonView pv = PhotonView.Find(viewId);
            if (pv != null)
                photonView.RPC(nameof(RPC_ApplyBoost), pv.Owner, viewId, false, 1f, 0f);
        }
    }

    /// <summary>
    /// Sent to the target's owner so only they modify their own MovementSC.
    /// </summary>
    [PunRPC]
    private void RPC_ApplyBoost(int viewId, bool on, float multiplier, float duration)
    {
        PhotonView pv = PhotonView.Find(viewId);
        if (pv == null || !pv.IsMine) return;

        StatusEffectManager statusManager = pv.gameObject.GetComponent<StatusEffectManager>();
        if (statusManager == null)
            statusManager = pv.gameObject.AddComponent<StatusEffectManager>();

        if (on)
        {
            statusManager.ApplyEffect(StatusEffectType.Groove, 2, duration > 0f ? duration : 999f, multiplier);
        }
        else
        {
            statusManager.RemoveEffect(StatusEffectType.Groove);
        }
    }

    private bool IsKillerNear(float maxDist)
    {
        BasePlayer[] all = FindObjectsByType<BasePlayer>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].GetPlayerType() == BasePlayer.PlayerType.Killer)
            {
                RagdollNetwork rag = all[i].GetComponent<RagdollNetwork>();
                Vector3 kPos = rag != null && rag.hip != null ? rag.hip.position : all[i].transform.position;
                if (Vector3.Distance(transform.position, kPos) <= maxDist)
                    return true;
            }
        }
        return false;
    }

    private void NetworkDestroy()

    {
        if (photonView.IsMine)
            PhotonNetwork.Destroy(gameObject);
    }
}
