using UnityEngine;

namespace CUBE.Achievements
{
    /// <summary>
    /// Xbox achievement backend stub.
    ///
    /// <para>Replace the TODO implementations with Microsoft GDK calls when
    /// Xbox support is integrated.  The architecture is ready — only this
    /// file needs platform-specific code.</para>
    /// </summary>
    public class XboxAchievementPlatform : MonoBehaviour, IAchievementPlatform
    {
        public string PlatformName => "Xbox";

        private void Start()
        {
            if (AchievementService.Instance != null)
                AchievementService.Instance.RegisterPlatform(this);
        }

        public void Initialize()
        {
            // TODO: Initialize Xbox GDK / XGameRuntimeInitialize
            Debug.Log("[XboxAchievementPlatform] Stub initialized.");
        }

        public void Unlock(string platformId)
        {
            if (string.IsNullOrEmpty(platformId)) return;

            // TODO: XblAchievementsUpdateAchievementAsync(platformId, 100)
            Debug.Log($"[XboxAchievementPlatform] STUB Unlock: {platformId}");
        }

        public void ReportProgress(string platformId, float current, float target)
        {
            if (string.IsNullOrEmpty(platformId)) return;

            // TODO: XblAchievementsUpdateAchievementAsync(platformId, percentComplete)
            float percent = target > 0f ? (current / target) * 100f : 0f;
            Debug.Log($"[XboxAchievementPlatform] STUB Progress: {platformId} = {percent:F0}%");
        }

        public bool IsUnlocked(string platformId)
        {
            // TODO: Query Xbox achievement state
            return false;
        }

        public void Shutdown()
        {
            // TODO: Flush Xbox state
            Debug.Log("[XboxAchievementPlatform] Stub shutdown.");
        }
    }
}
