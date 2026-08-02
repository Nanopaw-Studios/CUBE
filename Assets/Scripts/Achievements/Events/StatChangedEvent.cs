namespace CUBE.Achievements
{
    /// <summary>
    /// Published to <see cref="GameplayEventBus"/> whenever a statistic (Lifetime or Session) changes value.
    /// Completely decouples the Statistics system from achievements — achievements simply listen to this event.
    /// </summary>
    public readonly struct StatChangedEvent
    {
        public readonly StatId Stat;
        public readonly StatScope Scope;
        public readonly float NewValue;
        public readonly float Delta;

        public StatChangedEvent(StatId stat, StatScope scope, float newValue, float delta)
        {
            Stat = stat;
            Scope = scope;
            NewValue = newValue;
            Delta = delta;
        }
    }
}
