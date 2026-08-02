using System.Collections.Generic;
using UnityEngine;

namespace CUBE.Achievements
{
    /// <summary>
    /// In-game achievement tracking that works regardless of storefront.
    ///
    /// <para>This platform is always active.  It tracks unlock state using
    /// the <see cref="AchievementSaveManager"/> (which the
    /// <see cref="AchievementService"/> already manages), so it doesn't need
    /// its own persistence — it just reads from the shared runtime state.</para>
    ///
    /// <para>Use this for in-game achievement UIs, profile badges, and
    /// cross-platform parity checks.</para>
    /// </summary>
    public class CustomAchievementPlatform : MonoBehaviour, IAchievementPlatform
    {
        public string PlatformName => "Custom";

        // Local cache of unlocked IDs (mirrors AchievementService state)
        private readonly HashSet<string> _unlocked = new();

        private void Start()
        {
            if (AchievementService.Instance != null)
                AchievementService.Instance.RegisterPlatform(this);
        }

        public void Initialize()
        {
            Debug.Log("[CustomAchievementPlatform] Initialized.");
        }

        public void Unlock(string platformId)
        {
            if (string.IsNullOrEmpty(platformId)) return;

            _unlocked.Add(platformId);
            Debug.Log($"[CustomAchievementPlatform] Unlocked: {platformId}");
        }

        public void ReportProgress(string platformId, float current, float target)
        {
            // The custom platform doesn't need to do anything special for
            // progress — it's all managed by AchievementService.
        }

        public bool IsUnlocked(string platformId)
        {
            if (string.IsNullOrEmpty(platformId)) return false;
            return _unlocked.Contains(platformId);
        }

        public void Shutdown()
        {
            Debug.Log("[CustomAchievementPlatform] Shutdown.");
        }
    }
}
