using UnityEngine;

namespace CUBE.Achievements
{
    /// <summary>
    /// Epic Online Services (EOS) achievement backend stub.
    ///
    /// <para>Replace the TODO implementations with EOS SDK calls when
    /// Epic Games Store support is integrated.</para>
    /// </summary>
    public class EpicAchievementPlatform : MonoBehaviour, IAchievementPlatform
    {
        public string PlatformName => "Epic";

        private void Start()
        {
            if (AchievementService.Instance != null)
                AchievementService.Instance.RegisterPlatform(this);
        }

        public void Initialize()
        {
            // TODO: EOS_Achievements_QueryPlayerAchievements
            Debug.Log("[EpicAchievementPlatform] Stub initialized.");
        }

        public void Unlock(string platformId)
        {
            if (string.IsNullOrEmpty(platformId)) return;

            // TODO: EOS_Achievements_UnlockAchievements
            Debug.Log($"[EpicAchievementPlatform] STUB Unlock: {platformId}");
        }

        public void ReportProgress(string platformId, float current, float target)
        {
            if (string.IsNullOrEmpty(platformId)) return;

            // TODO: EOS stats for progress-based achievements
            float percent = target > 0f ? (current / target) * 100f : 0f;
            Debug.Log($"[EpicAchievementPlatform] STUB Progress: {platformId} = {percent:F0}%");
        }

        public bool IsUnlocked(string platformId)
        {
            // TODO: Query EOS achievement state
            return false;
        }

        public void Shutdown()
        {
            // TODO: Flush EOS state
            Debug.Log("[EpicAchievementPlatform] Stub shutdown.");
        }
    }
}
