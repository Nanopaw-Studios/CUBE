namespace CUBE.Achievements
{
    /// <summary>
    /// Dictates whether a statistic persists forever (Lifetime) or resets automatically on game loop changes (Session).
    /// </summary>
    public enum StatScope
    {
        /// <summary>Persists to disk/cloud across sessions.</summary>
        Lifetime = 0,

        /// <summary>Resets per round, match, or lobby session.</summary>
        Session = 1
    }
}
