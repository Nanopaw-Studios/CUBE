#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif

using UnityEngine;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

namespace CUBE.Achievements
{
    /// <summary>
    /// Steam achievement backend powered by Steamworks.NET.
    ///
    /// <para>Uses the same <c>DISABLESTEAMWORKS</c> preprocessor guard as the
    /// existing <see cref="CUBESteamManager"/> so builds that exclude
    /// Steamworks compile cleanly.</para>
    ///
    /// <para>Attach as a MonoBehaviour to a DontDestroyOnLoad object and call
    /// <c>AchievementService.Instance.RegisterPlatform(this)</c> in
    /// <see cref="Start"/>.</para>
    /// </summary>
    public class SteamAchievementPlatform : MonoBehaviour, IAchievementPlatform
    {
        public string PlatformName => "Steam";

        private bool _ready;

        private void Start()
        {
            // Self-register with the achievement service
            if (AchievementService.Instance != null)
                AchievementService.Instance.RegisterPlatform(this);
            else
                Debug.LogWarning("[SteamAchievementPlatform] AchievementService not found. " +
                                 "Ensure it initializes before this component.");
        }

        public void Initialize()
        {
#if !DISABLESTEAMWORKS
            _ready = CUBESteamManager.Initialized;
            if (_ready)
                Debug.Log("[SteamAchievementPlatform] Initialized.");
            else
                Debug.LogWarning("[SteamAchievementPlatform] Steam not initialized.");
#else
            _ready = false;
#endif
        }

        public void Unlock(string platformId)
        {
#if !DISABLESTEAMWORKS
            if (!_ready || string.IsNullOrEmpty(platformId))
                return;

            SteamUserStats.SetAchievement(platformId);
            SteamUserStats.StoreStats();

            Debug.Log($"[SteamAchievementPlatform] Unlocked: {platformId}");
#endif
        }

        public void ReportProgress(string platformId, float current, float target)
        {
#if !DISABLESTEAMWORKS
            if (!_ready || string.IsNullOrEmpty(platformId))
                return;

            // Steam supports stat-based progress.  If the achievement is tied
            // to a Steam stat with the same name, we can set it here.
            // This uses the convention: stat name == achievement API name + "_PROGRESS"
            // Adjust to match your Steamworks configuration.
            string statName = platformId + "_PROGRESS";
            SteamUserStats.SetStat(statName, (int)current);
            SteamUserStats.StoreStats();
#endif
        }

        public bool IsUnlocked(string platformId)
        {
#if !DISABLESTEAMWORKS
            if (!_ready || string.IsNullOrEmpty(platformId))
                return false;

            if (SteamUserStats.GetAchievement(platformId, out bool achieved))
                return achieved;
#endif
            return false;
        }

        public void Shutdown()
        {
#if !DISABLESTEAMWORKS
            if (_ready)
            {
                SteamUserStats.StoreStats();
                Debug.Log("[SteamAchievementPlatform] Final StoreStats called.");
            }
#endif
        }
    }
}
