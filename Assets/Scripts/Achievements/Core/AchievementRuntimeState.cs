using System;

namespace CUBE.Achievements
{
    /// <summary>
    /// Versioned mutable runtime state for an achievement.
    /// Serialisable to JSON for local and cloud persistence.
    /// </summary>
    [Serializable]
    public class AchievementRuntimeState
    {
        public string AchievementId;
        public string GuidString;
        public int SavedVersion = 1;

        public bool IsUnlocked;
        public float CurrentProgress;
        public string UnlockedTimestamp;
        public int UnlockCount;

        public SerializableGuid InternalGuid => SerializableGuid.Parse(GuidString);

        public void Increment(float amount, float max)
        {
            CurrentProgress += amount;
            if (CurrentProgress > max) CurrentProgress = max;
            if (CurrentProgress < 0f) CurrentProgress = 0f;
        }

        public void SetValue(float value, float max)
        {
            CurrentProgress = value;
            if (CurrentProgress > max) CurrentProgress = max;
            if (CurrentProgress < 0f) CurrentProgress = 0f;
        }

        public bool HasReachedTarget(float target) => CurrentProgress >= target;
    }
}
