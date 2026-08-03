namespace CUBE.Achievements
{
    /// <summary>
    /// Enum of all tracked statistics (Lifetime and Session).
    /// Decoupled from achievements — used by statistics system, profiles, UI, and requirement query context.
    /// </summary>
    public enum StatId
    {
        None                  = 0,

        // ── Lifetime Stats ─────────────────────────────────────────────
        LifetimeWins          = 100,
        LifetimeLosses        = 101,
        MatchesPlayed         = 102,
        RoundsSurvived        = 103,
        RoundsAsKiller        = 200,
        RoundsAsSurvivor      = 201,
        DamageDealt           = 300,
        DamageTaken           = 301,
        HealingDone           = 302,
        PlayersEliminated     = 303,
        SurvivorsSaved        = 304,
        Deaths                = 305,
        DistanceWalked        = 400,
        DistanceSprinted      = 401,
        TimeAirborne          = 402,
        JumpCount             = 403,
        CoinsEarned           = 500,
        MasteryLevelsGained   = 501,
        AbilitiesUsed         = 600,
        TimesEmoted           = 601,

        // ── Session Stats (Reset per round / match) ────────────────────
        DamageTakenThisRound  = 700,
        DamageDealtThisRound  = 701,
        AirtimeThisRound      = 702,
        DistanceThisRound     = 703,
        EliminationsThisRound = 704,
        AbilitiesUsedThisRound= 705,
        TimeWithoutSprinting  = 706,
        HealingThisRound      = 707,
        DeathsThisSession     = 708,
        JumpCountThisSession  = 709,

        // ── Time Tracking ──────────────────────────────────────────────
        TimePlayedSeconds     = 800,
        HoursPlayed           = 801,
        ConsecutiveLoginDays  = 900,
        CubeCoins             = 901
    }
}
