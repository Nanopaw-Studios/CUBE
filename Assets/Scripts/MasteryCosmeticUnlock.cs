using System;
using UnityEngine;

/// <summary>
/// One cosmetic step in a character's mastery progression.
/// The built-in tiers unlock at mastery levels 25, 50 and 100.
/// Character assets may override any tier through CubeCharacterData.masteryCosmetics.
/// </summary>
[Serializable]
public class MasteryCosmeticUnlock
{
    public const int TierCount = 3;

    [Header("Unlock")]
    [Tooltip("Mastery level required for this cosmetic. The built-in tiers use 25, 50 and 100.")]
    public int RequiredLevel = 25;

    public string DisplayName = "Bronze Circuit";

    [Header("Material Override")]
    [Tooltip("Optional material whose full set of shader properties is copied onto this player's character material. Assign a material built with the character's shader (Player) to reuse every setting it supports, including textures, colours, emission and keywords. When assigned, the built-in tint values below are not applied.")]
    public Material SourceMaterial;

    [Header("Built-in Tint (ignored when Source Material is assigned)")]
    public Color Tint = new Color(1f, 0.58f, 0.16f, 1f);

    [Range(0f, 1f)]
    public float TintStrength = 0.35f;

    [Range(0f, 1f)]
    public float Metallic = 0.1f;

    [Range(0f, 1f)]
    public float Smoothness = 0.08f;

    [Tooltip("Continuously cycles the primary colour. Tier 100 animates automatically unless a Source Material is assigned.")]
    public bool AnimateHue;

    [Header("Emission (shaders that support it)")]
    public Color EmissionColor = new Color(1f, 0.4f, 0.05f, 1f);

    [Range(0f, 4f)]
    public float EmissionIntensity = 0.12f;

    [Header("Optional Extra Cosmetic")]
    [Tooltip("Optional prefab attached at tier 100 or any configured tier.")]
    public Accessory ExtraAccessory;

    private static readonly MasteryCosmeticUnlock[] Defaults =
    {
        new MasteryCosmeticUnlock
        {
            RequiredLevel = 25,
            DisplayName = "Bronze Circuit",
            Tint = new Color(1f, 0.58f, 0.16f, 1f),
            TintStrength = 0.35f,
            Metallic = 0.1f,
            Smoothness = 0.08f,
            EmissionColor = new Color(1f, 0.4f, 0.05f, 1f),
            EmissionIntensity = 0.12f,
        },
        new MasteryCosmeticUnlock
        {
            RequiredLevel = 50,
            DisplayName = "Emerald Circuit",
            Tint = new Color(0.12f, 1f, 0.5f, 1f),
            TintStrength = 0.45f,
            Metallic = 0.18f,
            Smoothness = 0.12f,
            EmissionColor = new Color(0.05f, 1f, 0.35f, 1f),
            EmissionIntensity = 0.2f,
        },
        new MasteryCosmeticUnlock
        {
            RequiredLevel = 100,
            DisplayName = "Prismatic Aura",
            Tint = new Color(1f, 0.3f, 1f, 1f),
            TintStrength = 0.65f,
            Metallic = 0.3f,
            Smoothness = 0.25f,
            AnimateHue = true,
            EmissionColor = new Color(1f, 0.4f, 1f, 1f),
            EmissionIntensity = 0.45f,
        },
    };

    private static readonly int[] TierLevels = { 25, 50, 100 };

    public static int GetRequiredLevel(int tier)
    {
        return TierLevels[Mathf.Clamp(tier, 1, TierCount) - 1];
    }

    public static MasteryCosmeticUnlock GetDefault(int tier)
    {
        return Defaults[Mathf.Clamp(tier, 1, TierCount) - 1];
    }

    /// <summary>
    /// Returns the override configured for this exact tier, or the built-in
    /// prestige tier when the character has no override for it. This keeps
    /// unconfigured tiers visually distinct instead of inheriting a lower tier.
    /// </summary>
    public static MasteryCosmeticUnlock GetFor(CubeCharacterData data, int tier)
    {
        if (tier <= 0)
            return null;

        int requiredLevel = GetRequiredLevel(tier);

        if (data != null && data.masteryCosmetics != null)
        {
            for (int i = 0; i < data.masteryCosmetics.Length; i++)
            {
                MasteryCosmeticUnlock entry = data.masteryCosmetics[i];
                if (entry != null && entry.RequiredLevel == requiredLevel)
                    return entry;
            }
        }

        return GetDefault(tier);
    }
}
