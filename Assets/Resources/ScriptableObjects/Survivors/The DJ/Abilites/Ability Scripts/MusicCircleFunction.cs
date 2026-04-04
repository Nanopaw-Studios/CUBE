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

        MovementSC movement = pv.gameObject.GetComponentInChildren<MovementSC>();
        if (movement == null) return;

        BasePlayerStats stats = pv.gameObject.GetComponent<BasePlayerStats>();
        float baseSpeed = stats != null ? stats.speed : 5f;

        if (on)
        {
            movement.Force = baseSpeed * multiplier;

            // Schedule a reset in case the circle expires while they're still inside
            MusicBoostResetter resetter = pv.gameObject.GetComponent<MusicBoostResetter>();
            if (resetter == null)
                resetter = pv.gameObject.AddComponent<MusicBoostResetter>();

            resetter.ScheduleReset(baseSpeed, duration);
        }
        else
        {
            // Immediate exit — restore base speed
            movement.Force = baseSpeed;
            MusicBoostResetter resetter = pv.gameObject.GetComponent<MusicBoostResetter>();
            resetter?.CancelReset();
        }
    }

    private void NetworkDestroy()
    {
        if (photonView.IsMine)
            PhotonNetwork.Destroy(gameObject);
    }
}
