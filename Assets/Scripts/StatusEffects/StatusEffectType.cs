/// <summary>
/// Defines the types of status effects that can be applied to players.
/// Includes core stat modifiers and character-specific gameplay abilities.
/// </summary>
public enum StatusEffectType : byte
{
    // ── Core Stat Modifiers ──
    /// <summary>Increases movement speed (e.g. Speed I, Speed II, Speed III).</summary>
    Speed = 1,

    /// <summary>Decreases movement speed (e.g. Heavy Legs I, Heavy Legs II, Heavy Legs III).</summary>
    HeavyLegs = 2,

    /// <summary>Completely immobilizes player movement (e.g. Compile Boost prep, freeze traps).</summary>
    Freeze = 3,

    /// <summary>Reduces incoming damage taken (e.g. Resistance I, Resistance II).</summary>
    Resistance = 4,

    /// <summary>Increases incoming damage taken (e.g. Vulnerability I, Vulnerability II).</summary>
    Vulnerability = 5,

    /// <summary>Multiplies stamina regeneration rate.</summary>
    StaminaBoost = 6,

    // ── The Glitch ──
    /// <summary>The Glitch: Chromatic aberration, film grain, and sensory glitching on hit/proximity.</summary>
    GlitchDistortion = 7,

    // ── The DJ ──
    /// <summary>The DJ: Music Circle buff providing movement speed + rapid stamina recovery aura.</summary>
    Groove = 8,

    /// <summary>The DJ: Equalizer Wall blast debuff causing stagger, slow, and vulnerability.</summary>
    Equalized = 9,

    /// <summary>The DJ: Stacking speed and stamina momentum whenever abilities are used nearby.</summary>
    Hype = 10,

    // ── The Coder ──
    /// <summary>The Coder: Overclocked Mind accelerating ability cooldown ticks.</summary>
    Overclocked = 11,

    /// <summary>The Coder: Temporal lag trap that severely slows enemies with simulated network jitter.</summary>
    LagSpike = 12,

    // ── The Titan ──
    /// <summary>The Titan: Shockwave ground slam knocking players off-balance with unsteady legs.</summary>
    Tremor = 13,

    /// <summary>The Titan: WindSwipe sweeping pressure blade causing forward stagger and deceleration.</summary>
    WindStagger = 14,

    // ── The Healer ──
    /// <summary>The Healer: Heal Circle restoring health per second over time.</summary>
    Regeneration = 15,

    /// <summary>The Healer: Last Stand emergency buff granting heavy damage resistance and clutch speed.</summary>
    Sanctuary = 16
}
