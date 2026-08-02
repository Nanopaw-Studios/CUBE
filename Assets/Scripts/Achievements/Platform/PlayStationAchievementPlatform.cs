using UnityEngine;

namespace CUBE.Achievements
{
    /// <summary>
    /// PlayStation achievement / trophy backend stub.
    ///
    /// <para>Replace the TODO implementations with PlayStation SDK calls
    /// (e.g. <c>NpTrophy</c>) when PlayStation support is integrated.</para>
    /// </summary>
    public class PlayStationAchievementPlatform : MonoBehaviour, IAchievementPlatform
    {
        public string PlatformName => "PlayStation";

        private void Start()
        {
            if (AchievementService.Instance != null)
                AchievementService.Instance.RegisterPlatform(this);
        }

        public void Initialize()
        {
            // TODO: Initialize PS trophy context via NpTrophy APIs
            Debug.Log("[PlayStationAchievementPlatform] Stub initialized.");
        }

        public void Unlock(string platformId)
        {
            if (string.IsNullOrEmpty(platformId)) return;

            // TODO: sceNpTrophyUnlockTrophy(trophyId)
            Debug.Log($"[PlayStationAchievementPlatform] STUB Unlock: {platformId}");
        }

        public void ReportProgress(string platformId, float current, float target)
        {
            if (string.IsNullOrEmpty(platformId)) return;

            // PlayStation trophies are unlock-only, no progress.
            // This is a no-op by design.
        }

        public bool IsUnlocked(string platformId)
        {
            // TODO: sceNpTrophyGetTrophyUnlockState
            return false;
        }

        public void Shutdown()
        {
            // TODO: Destroy PS trophy context
            Debug.Log("[PlayStationAchievementPlatform] Stub shutdown.");
        }
    }
}
