using UnityEngine;

/// <summary>
/// Represents an active, timed instance of a status effect with a specific tier/strength.
/// Supports both generic core stat modifiers and character-specific abilities.
/// </summary>
[System.Serializable]
public class ActiveStatusEffect
{
    public StatusEffectType type;
    public int tier;
    public float durationRemaining;
    public float totalDuration;
    public float customMultiplier;

    public ActiveStatusEffect(StatusEffectType type, int tier, float duration, float customMultiplier = 0f)
    {
        this.type = type;
        this.tier = Mathf.Max(1, tier);
        this.totalDuration = Mathf.Max(0.05f, duration);
        this.durationRemaining = this.totalDuration;
        this.customMultiplier = customMultiplier;
    }

    /// <summary>Is this status effect a negative debuff?</summary>
    public bool IsDebuff => type == StatusEffectType.HeavyLegs
                         || type == StatusEffectType.Freeze
                         || type == StatusEffectType.Vulnerability
                         || type == StatusEffectType.GlitchDistortion
                         || type == StatusEffectType.Equalized
                         || type == StatusEffectType.LagSpike
                         || type == StatusEffectType.Tremor
                         || type == StatusEffectType.WindStagger;

    /// <summary>Hex color code for UI display based on effect type and buff/debuff status.</summary>
    public string ColorHex
    {
        get
        {
            return type switch
            {
                StatusEffectType.Speed => "#00FF88",           // Mint Green
                StatusEffectType.HeavyLegs => "#FF5533",       // Fiery Orange/Red
                StatusEffectType.Freeze => "#33CCFF",          // Ice Blue
                StatusEffectType.Resistance => "#FFD700",      // Gold
                StatusEffectType.Vulnerability => "#FF3366",    // Crimson
                StatusEffectType.StaminaBoost => "#00D4FF",    // Cyan Surge
                StatusEffectType.GlitchDistortion => "#D500F9",// Electric Magenta
                StatusEffectType.Groove => "#00E5FF",          // DJ Electric Cyan
                StatusEffectType.Equalized => "#FF9100",       // DJ Amber Blast
                StatusEffectType.Hype => "#FF007F",            // DJ Hot Pink
                StatusEffectType.Overclocked => "#76FF03",     // Coder Matrix Green
                StatusEffectType.LagSpike => "#FF1744",        // Coder Ping Red
                StatusEffectType.Tremor => "#FF6D00",          // Titan Tremor Orange
                StatusEffectType.WindStagger => "#AEEA00",     // Titan Wind Lime
                StatusEffectType.Regeneration => "#00E676",    // Healer Emerald
                StatusEffectType.Sanctuary => "#FFD600",       // Healer Divine Gold
                _ => "#FFFFFF"
            };
        }
    }

    /// <summary>Icon symbol for fast HUD readability.</summary>
    public string Icon
    {
        get
        {
            return type switch
            {
                StatusEffectType.Speed => "⚡",
                StatusEffectType.HeavyLegs => "⚓",
                StatusEffectType.Freeze => "❄️",
                StatusEffectType.Resistance => "🛡️",
                StatusEffectType.Vulnerability => "💔",
                StatusEffectType.StaminaBoost => "🔋",
                StatusEffectType.GlitchDistortion => "👾",
                StatusEffectType.Groove => "🎵",
                StatusEffectType.Equalized => "🔊",
                StatusEffectType.Hype => "🔥",
                StatusEffectType.Overclocked => "💻",
                StatusEffectType.LagSpike => "📡",
                StatusEffectType.Tremor => "💥",
                StatusEffectType.WindStagger => "🌪️",
                StatusEffectType.Regeneration => "💖",
                StatusEffectType.Sanctuary => "✨",
                _ => "✨"
            };
        }
    }

    /// <summary>Roman numeral representation of the tier (e.g. I, II, III).</summary>
    public string RomanTier => ToRomanNumeral(tier);

    /// <summary>Display name combining effect title and Roman numeral tier (e.g. "Heavy Legs II").</summary>
    public string DisplayName
    {
        get
        {
            string baseName = type switch
            {
                StatusEffectType.Speed => "Speed",
                StatusEffectType.HeavyLegs => "Heavy Legs",
                StatusEffectType.Freeze => "Frozen",
                StatusEffectType.Resistance => "Resistance",
                StatusEffectType.Vulnerability => "Vulnerability",
                StatusEffectType.StaminaBoost => "Stamina Surge",
                StatusEffectType.GlitchDistortion => "Distortion",
                StatusEffectType.Groove => "Groove",
                StatusEffectType.Equalized => "Equalized",
                StatusEffectType.Hype => "Hype",
                StatusEffectType.Overclocked => "Overclocked",
                StatusEffectType.LagSpike => "Lag Spike",
                StatusEffectType.Tremor => "Tremor",
                StatusEffectType.WindStagger => "Wind Stagger",
                StatusEffectType.Regeneration => "Regen",
                StatusEffectType.Sanctuary => "Sanctuary",
                _ => type.ToString()
            };

            if (type == StatusEffectType.Freeze && tier == 1)
                return baseName;

            return $"{baseName} {RomanTier}";
        }
    }

    /// <summary>
    /// Calculates net speed multiplier for this effect.
    /// </summary>
    public float GetSpeedMultiplier()
    {
        if (customMultiplier > 0f)
            return customMultiplier;

        return type switch
        {
            StatusEffectType.Speed => 1f + (tier * 0.25f),
            StatusEffectType.Groove => 1f + (tier * 0.20f),
            StatusEffectType.Hype => 1f + (tier * 0.15f),
            StatusEffectType.Sanctuary => 1f + (tier * 0.25f),
            StatusEffectType.HeavyLegs => 1f / (1f + (tier * 0.25f)),
            StatusEffectType.Equalized => 1f / (1f + (tier * 0.25f)),
            StatusEffectType.WindStagger => 1f / (1f + (tier * 0.25f)),
            StatusEffectType.LagSpike => 1f / (1f + (tier * 0.35f)),
            StatusEffectType.Tremor => 0.75f,
            StatusEffectType.Freeze => 0f,
            _ => 1f
        };
    }

    /// <summary>
    /// Calculates fractional damage reduction (0.0 to 1.0).
    /// </summary>
    public float GetDamageResistance()
    {
        if (customMultiplier > 0f)
            return Mathf.Clamp01(customMultiplier);

        return type switch
        {
            StatusEffectType.Resistance => Mathf.Clamp01(tier * 0.15f),
            StatusEffectType.Sanctuary => Mathf.Clamp01(0.20f + tier * 0.15f),
            _ => 0f
        };
    }

    /// <summary>
    /// Calculates incoming damage multiplier for vulnerability effects.
    /// </summary>
    public float GetVulnerabilityMultiplier()
    {
        if (customMultiplier > 0f)
            return customMultiplier;

        return type switch
        {
            StatusEffectType.Vulnerability => 1f + (tier * 0.15f),
            StatusEffectType.Equalized => 1f + (tier * 0.15f),
            _ => 1f
        };
    }

    /// <summary>
    /// Calculates stamina regeneration multiplier.
    /// </summary>
    public float GetStaminaRegenMultiplier()
    {
        if (customMultiplier > 0f)
            return customMultiplier;

        return type switch
        {
            StatusEffectType.StaminaBoost => 1f + (tier * 0.25f),
            StatusEffectType.Groove => 1.50f,
            StatusEffectType.Hype => 1f + (tier * 0.25f),
            _ => 1f
        };
    }

    /// <summary>
    /// Calculates health restored per second for Regeneration.
    /// </summary>
    public float GetHealPerSecond()
    {
        if (type != StatusEffectType.Regeneration)
            return 0f;

        if (customMultiplier > 0f)
            return customMultiplier;

        return tier * 5f; // Tier 1 = +5 HP/s, Tier 2 = +10 HP/s
    }

    public static string ToRomanNumeral(int number)
    {
        return number switch
        {
            1 => "I",
            2 => "II",
            3 => "III",
            4 => "IV",
            5 => "V",
            6 => "VI",
            7 => "VII",
            8 => "VIII",
            9 => "IX",
            10 => "X",
            _ => number.ToString()
        };
    }
}
