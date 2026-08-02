namespace CUBE.Achievements
{
    /// <summary>
    /// Immutable value-type event structure published via <see cref="GameplayEventBus"/>.
    /// Carries a reference to an immutable <see cref="GameplayContext"/>.
    /// Zero allocation on publish stack.
    /// </summary>
    public readonly struct GameplayEvent
    {
        public readonly GameplayEventType Type;
        public readonly GameplayContext Context;

        public GameplayEvent(GameplayEventType type, GameplayContext context = null)
        {
            Type = type;
            Context = context ?? GameplayContext.Empty;
        }

        public override string ToString() => $"[GameplayEvent] {Type} | SrcActor={Context?.SourceActorNumber}";
    }
}
