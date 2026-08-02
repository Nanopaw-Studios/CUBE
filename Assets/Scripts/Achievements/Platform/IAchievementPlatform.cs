namespace CUBE.Achievements
{
    /// <summary>
    /// Platform-agnostic interface for achievement backends.
    ///
    /// <para>Gameplay code never calls platform SDKs directly — it calls
    /// <see cref="AchievementService"/>, which delegates to all registered
    /// <see cref="IAchievementPlatform"/> instances.</para>
    ///
    /// <para>Multiple platforms can be active simultaneously (e.g. Steam +
    /// Custom in-game tracking).</para>
    /// </summary>
    public interface IAchievementPlatform
    {
        /// <summary>
        /// Human-readable platform name used for ID look-up via
        /// <see cref="AchievementDefinition.GetPlatformId"/>.
        /// Must match one of: "Steam", "Xbox", "PlayStation", "Epic", "Custom".
        /// </summary>
        string PlatformName { get; }

        /// <summary>
        /// Called once when the platform is registered with
        /// <see cref="AchievementService"/>.
        /// </summary>
        void Initialize();

        /// <summary>
        /// Unlock / complete an achievement on this platform.
        /// </summary>
        /// <param name="platformId">
        /// The platform-specific achievement ID (e.g. Steam API name,
        /// Xbox achievement ID, etc.).
        /// </param>
        void Unlock(string platformId);

        /// <summary>
        /// Report incremental progress for a progressive achievement.
        /// Not all platforms support this — implementations should no-op
        /// gracefully if unsupported.
        /// </summary>
        void ReportProgress(string platformId, float current, float target);

        /// <summary>
        /// Query whether an achievement is already unlocked on this platform.
        /// </summary>
        bool IsUnlocked(string platformId);

        /// <summary>
        /// Called when the <see cref="AchievementService"/> shuts down.
        /// Flush any pending state (e.g. <c>SteamUserStats.StoreStats()</c>).
        /// </summary>
        void Shutdown();
    }
}
