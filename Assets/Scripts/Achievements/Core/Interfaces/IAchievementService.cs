using System;

namespace CUBE.Achievements
{
    public struct AchievementUnlockedEventArgs
    {
        public AchievementDefinition Definition;
        public AchievementRuntimeState State;
        public string UnlockTimestamp;
    }

    public struct AchievementProgressEventArgs
    {
        public AchievementDefinition Definition;
        public float CurrentProgress;
        public float TargetValue;
    }

    public struct RequirementFailedEventArgs
    {
        public AchievementDefinition Definition;
        public object FailedRequirement;
        public string Reason;
    }

    /// <summary>
    /// Interface for the core achievement engine.
    /// Exposes public lifecycle events for UI, telemetry, and debugging.
    /// </summary>
    public interface IAchievementService
    {
        void Unlock(string achievementId);
        void Unlock(SerializableGuid guid);
        void AddProgress(string achievementId, float amount);
        void SetProgress(string achievementId, float value);

        AchievementRuntimeState GetState(string achievementId);
        AchievementRuntimeState GetState(SerializableGuid guid);
        bool IsUnlocked(string achievementId);

        void EvaluatePending(EvaluationMode mode);

        event Action<AchievementUnlockedEventArgs> OnAchievementUnlocked;
        event Action<AchievementProgressEventArgs> OnProgressUpdated;
        event Action<RequirementFailedEventArgs> OnRequirementFailed;
        event Action<AchievementDefinition> OnAchievementEvaluated;
    }
}
