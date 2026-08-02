namespace CUBE.Achievements
{
    /// <summary>
    /// Abstraction query layer passed to requirements during evaluation.
    /// Requirements ask this context for data rather than directly coupling to game singletons.
    /// </summary>
    public interface IRequirementContext
    {
        /// <summary>The gameplay event currently being evaluated.</summary>
        GameplayEvent CurrentEvent { get; }

        /// <summary>The rich immutable context associated with the current event.</summary>
        GameplayContext Context { get; }

        /// <summary>The runtime state of the achievement being evaluated.</summary>
        AchievementRuntimeState RuntimeState { get; }

        /// <summary>Queries a statistic (Lifetime or Session scope) without knowing how statistics are calculated.</summary>
        float GetStat(StatId statId, StatScope scope);

        /// <summary>Returns the Photon ActorNumber of the local player.</summary>
        int LocalActorNumber { get; }

        /// <summary>Queries custom metadata extensions by key.</summary>
        bool TryGetCustomValue<T>(string key, out T value);
    }
}
