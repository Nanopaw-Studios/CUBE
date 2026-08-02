using System;

namespace CUBE.Achievements
{
    /// <summary>
    /// Decoupled statistics service interface.
    /// Tracks lifetime and session statistics independently of achievements.
    /// Publishes StatChangedEvent to GameplayEventBus whenever a stat updates.
    /// </summary>
    public interface IStatisticsService
    {
        float GetStat(StatId id, StatScope scope);
        void SetStat(StatId id, StatScope scope, float value);
        void IncrementStat(StatId id, StatScope scope, float delta);

        void ResetSessionStats(ResetTiming timing);
        void SaveLifetimeStats();

        event Action<StatId, StatScope, float> OnStatChanged;
    }

    public enum ResetTiming
    {
        RoundStart,
        RoundEnd,
        MatchStart,
        MatchEnd,
        LobbyLeave
    }
}
