using Photon.Pun;
using UnityEngine;

namespace CUBE.Achievements
{
    /// <summary>
    /// Shared utility methods used by conditions, the achievement service, and
    /// the statistics tracker.  Centralises "find the local player" logic so
    /// every condition doesn't duplicate it.
    /// </summary>
    public static class AchievementHelpers
    {
        // Cached reference cleared on domain reload.
        private static BasePlayer _cachedLocalPlayer;

        /// <summary>
        /// Returns the <see cref="BasePlayer"/> component owned by the local
        /// Photon client, or <c>null</c> if no local player exists yet.
        /// Result is cached per frame-cluster to avoid repeated FindGameObjects.
        /// </summary>
        public static BasePlayer GetLocalBasePlayer()
        {
            // Fast path: cached reference still valid
            if (_cachedLocalPlayer != null)
            {
                var pv = _cachedLocalPlayer.GetComponent<PhotonView>();
                if (pv != null && pv.IsMine)
                    return _cachedLocalPlayer;

                // Cache invalidated — fall through to search
                _cachedLocalPlayer = null;
            }

            // Search
            foreach (GameObject go in GameObject.FindGameObjectsWithTag("Player"))
            {
                PhotonView pv = go.GetComponent<PhotonView>();
                if (pv == null || !pv.IsMine)
                    continue;

                BasePlayer bp = go.GetComponent<BasePlayer>();
                if (bp != null)
                {
                    _cachedLocalPlayer = bp;
                    return bp;
                }
            }

            return null;
        }

        /// <summary>
        /// Returns the <see cref="BasePlayerStats"/> for the local player,
        /// or <c>null</c> if unavailable.
        /// </summary>
        public static BasePlayerStats GetLocalBasePlayerStats()
        {
            BasePlayer bp = GetLocalBasePlayer();
            return bp != null ? bp.GetComponent<BasePlayerStats>() : null;
        }

        /// <summary>
        /// Returns the Photon ActorNumber of the local player, or -1 if not in a room.
        /// </summary>
        public static int GetLocalActorNumber()
        {
            if (PhotonNetwork.LocalPlayer != null)
                return PhotonNetwork.LocalPlayer.ActorNumber;
            return -1;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _cachedLocalPlayer = null;
        }
    }
}
