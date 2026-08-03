using System.Collections.Generic;
using UnityEngine;

namespace CUBE.Achievements
{
    /// <summary>
    /// PlayFab backend implementation of <see cref="IAchievementPlatform"/>.
    ///
    /// <para>Reports achievement unlocks and progress updates to PlayFab cloud.
    /// Works alongside local persistence and other platform backends (e.g., Steam, Xbox).</para>
    /// </summary>
    public class PlayFabAchievementPlatform : MonoBehaviour, IAchievementPlatform
    {
        public string PlatformName => "PlayFab";

        private readonly HashSet<string> _unlocked = new();

        private void Start()
        {
            if (AchievementService.Instance != null)
            {
                AchievementService.Instance.RegisterPlatform(this);
            }
        }

        public void Initialize()
        {
            Debug.Log("[PlayFabAchievementPlatform] Initialized PlayFab achievement platform backend.");
        }

        public void Unlock(string platformId)
        {
            if (string.IsNullOrEmpty(platformId)) return;

            _unlocked.Add(platformId);
            Debug.Log($"[PlayFabAchievementPlatform] Unlocked achievement on PlayFab: '{platformId}'");

            if (AchievementService.Instance != null)
            {
                AchievementService.Instance.TriggerCloudSync();
            }
        }

        public void ReportProgress(string platformId, float current, float target)
        {
            if (string.IsNullOrEmpty(platformId)) return;

            if (AchievementService.Instance != null)
            {
                AchievementService.Instance.TriggerCloudSync();
            }
        }

        public bool IsUnlocked(string platformId)
        {
            if (string.IsNullOrEmpty(platformId)) return false;
            return _unlocked.Contains(platformId);
        }

        public void Shutdown()
        {
            Debug.Log("[PlayFabAchievementPlatform] Shutdown PlayFab achievement platform backend.");
        }
    }
}
