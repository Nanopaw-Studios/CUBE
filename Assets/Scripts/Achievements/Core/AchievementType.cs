namespace CUBE.Achievements
{
    /// <summary>
    /// Determines how an achievement tracks progress and unlocks.
    /// </summary>
    public enum AchievementType
    {
        /// <summary>
        /// A one-shot achievement that unlocks immediately when its trigger
        /// event fires and all conditions pass.  No progress bar.
        /// Example: "Kill first survivor", "Pet a spirit".
        /// </summary>
        OneShot = 0,

        /// <summary>
        /// A progressive achievement that accumulates progress toward a
        /// <see cref="AchievementDefinition.TargetValue"/> and unlocks when
        /// the target is reached.
        /// Example: "Walk 10,000 meters", "Kill 100 survivors".
        /// </summary>
        Progressive = 1,
    }
}
