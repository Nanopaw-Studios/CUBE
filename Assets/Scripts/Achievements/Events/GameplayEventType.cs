namespace CUBE.Achievements
{
    /// <summary>
    /// Every distinct gameplay occurrence that the achievement and statistics systems
    /// can react to.  Gameplay code publishes one of these; it never references
    /// achievement definitions or platform SDKs.
    ///
    /// Add new entries freely — existing subscribers simply ignore unknown types.
    /// Do NOT reorder or remove entries once shipped; serialised ScriptableObjects
    /// store the integer value.
    /// </summary>
    public enum GameplayEventType
    {
        None = 0,

        // ── Combat ────────────────────────────────────────────────────
        PlayerKilledEnemy        = 100,
        PlayerDamaged            = 101,
        PlayerTookDamage         = 102,
        PlayerHealed             = 103,
        PlayerDied               = 104,
        PlayerReachedHealth      = 105,

        // ── Round lifecycle ───────────────────────────────────────────
        RoundStarted             = 200,
        RoundEnded               = 201,
        PlayerWonRound           = 202,
        PlayerLostRound          = 203,
        PlayerSurvivedRound      = 204,
        PlayerRoleAssigned       = 205,
        PlayerSpawned            = 206,

        // ── Movement / traversal ──────────────────────────────────────
        PlayerJumped             = 300,
        PlayerDistanceMoved      = 301,
        PlayerSprinted           = 302,
        PlayerAirborneTimeUpdated = 303,
        PlayerEscaped            = 304,
        PlayerLaunchedIntoVoid   = 305,
        PlayerReachedHighestCube = 306,

        // ── Abilities / actions ───────────────────────────────────────
        PlayerUsedAbility        = 400,
        PlayerEmoted             = 401,

        // ── Economy / progression ─────────────────────────────────────
        PlayerCollectedCoins     = 500,
        PlayerMasteryLevelChanged = 501,

        // ── Social / multiplayer ──────────────────────────────────────
        PlayerJoinedRoom         = 600,
        PlayerLeftRoom           = 601,
        PlayerPlayedWithDeveloper   = 602,
        PlayerPlayedWithVerifiedPlayer = 603,
        PlayerCausedTeammateDeath = 604,
        PlayerSavedTeammate      = 605,
        LobbyEmotedSimultaneously = 606,
        KillerNeverHarmedAnyone  = 607,

        // ── Catch-all for future / modded events ──────────────────────
        Custom                   = 9999,
    }
}
