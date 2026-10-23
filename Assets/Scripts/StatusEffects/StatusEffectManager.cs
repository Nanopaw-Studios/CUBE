using System;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Central manager for timed, tiered status effects on a player.
/// Handles physics speed modifiers, damage modifiers, screen distortions, and networked RPCs.
/// </summary>
public class StatusEffectManager : MonoBehaviourPun
{
    private readonly Dictionary<StatusEffectType, ActiveStatusEffect> activeEffects = new();
    private readonly List<StatusEffectType> removalBuffer = new();

    // Volume overrides for sensory character effects
    private Volume cachedVolume;
    private ChromaticAberration chromatic;
    private FilmGrain filmGrain;
    private LensDistortion lensDistortion;
    private Vignette vignette;
    private float initialChromatic;
    private float initialGrain;
    private float initialDistortion;
    private float initialVignette;
    private bool volumeCached;
    private EZCameraShake.CameraShaker cachedCameraShaker;

    /// <summary>Fired whenever a status effect is added or upgraded.</summary>
    public event Action<ActiveStatusEffect> OnEffectApplied;

    /// <summary>Fired whenever a status effect expires or is removed.</summary>
    public event Action<ActiveStatusEffect> OnEffectRemoved;

    /// <summary>Fired whenever any status effect is added, removed, or refreshed.</summary>
    public event Action OnEffectsChanged;

    private MovementSC cachedMovement;

    private void Awake()
    {
        CacheVolume();
        cachedMovement = GetComponentInChildren<MovementSC>();
        cachedCameraShaker = GetComponentInChildren<EZCameraShake.CameraShaker>();
        OnEffectsChanged += SyncMovementSpeedModifier;
    }

    private void SyncMovementSpeedModifier()
    {
        if (cachedMovement == null)
            cachedMovement = GetComponentInChildren<MovementSC>();

        if (cachedMovement != null)
        {
            cachedMovement.SpeedModifier = GetSpeedMultiplier();
        }
    }

    private float regenTimer = 0f;

    private void Update()
    {
        // Only the local player simulates and counts down active effects on their client
        if (photonView != null && !photonView.IsMine)
            return;

        if (activeEffects.Count == 0)
            return;

        float dt = Time.deltaTime;
        removalBuffer.Clear();

        // Process Regeneration heal ticks
        if (activeEffects.TryGetValue(StatusEffectType.Regeneration, out var regen))
        {
            regenTimer += dt;
            if (regenTimer >= 1.0f)
            {
                regenTimer = 0f;
                BasePlayerStats stats = GetComponent<BasePlayerStats>();
                if (stats != null && stats.health < stats.maxHealth)
                {
                    stats.Heal(Mathf.RoundToInt(regen.GetHealPerSecond()));
                }
            }
        }
        else
        {
            regenTimer = 0f;
        }

        foreach (var kvp in activeEffects)
        {
            ActiveStatusEffect effect = kvp.Value;
            effect.durationRemaining -= dt;

            if (effect.durationRemaining <= 0f)
            {
                removalBuffer.Add(kvp.Key);
            }
        }

        if (removalBuffer.Count > 0)
        {
            for (int i = 0; i < removalBuffer.Count; i++)
            {
                StatusEffectType type = removalBuffer[i];
                if (activeEffects.TryGetValue(type, out var removedEffect))
                {
                    activeEffects.Remove(type);
                    OnEffectExpired(removedEffect);
                }
            }

            OnEffectsChanged?.Invoke();
        }

        UpdateSensoryEffects();
    }

    /// <summary>
    /// Applies a status effect locally. If photonView is not owned by local client, does nothing.
    /// Use RPC_ApplyStatusEffect to apply to remote players.
    /// </summary>
    public void ApplyEffect(StatusEffectType type, int tier, float duration, float customMultiplier = 0f, bool announce = true)
    {
        if (photonView != null && !photonView.IsMine)
            return;

        tier = Mathf.Max(1, tier);
        duration = Mathf.Max(0.05f, duration);

        bool isUpgrade = false;
        bool isNew = false;
        ActiveStatusEffect activeEffect;

        if (activeEffects.TryGetValue(type, out activeEffect))
        {
            if (tier > activeEffect.tier)
            {
                // Upgrade to higher tier
                activeEffect.tier = tier;
                activeEffect.totalDuration = duration;
                activeEffect.durationRemaining = duration;
                activeEffect.customMultiplier = customMultiplier;
                isUpgrade = true;
            }
            else if (tier == activeEffect.tier)
            {
                // Refresh duration to maximum
                activeEffect.durationRemaining = Mathf.Max(activeEffect.durationRemaining, duration);
                activeEffect.totalDuration = Mathf.Max(activeEffect.totalDuration, duration);
                if (customMultiplier > 0f)
                    activeEffect.customMultiplier = customMultiplier;
            }
            else
            {
                // Lower tier: keep higher tier, but extend duration if new duration exceeds remaining
                if (duration > activeEffect.durationRemaining)
                {
                    activeEffect.durationRemaining = duration;
                }
            }
        }
        else
        {
            activeEffect = new ActiveStatusEffect(type, tier, duration, customMultiplier);
            activeEffects[type] = activeEffect;
            isNew = true;
        }

        // Handle sensory/volume effects
        if (type == StatusEffectType.GlitchDistortion)
        {
            ApplyGlitchDistortion(activeEffect.tier);
        }
        else if (type == StatusEffectType.Tremor)
        {
            if (cachedCameraShaker == null)
                cachedCameraShaker = GetComponentInChildren<EZCameraShake.CameraShaker>();
            cachedCameraShaker?.ShakeOnce(1.8f * activeEffect.tier, 4f, 0.08f, 0.7f);
        }

        // Announce via status text
        if (announce && (isNew || isUpgrade))
        {
            string sign = activeEffect.IsDebuff ? "-" : "+";
            string color = activeEffect.ColorHex;
            CUBEStatusText.CreateText($"<color={color}>{sign} {activeEffect.DisplayName} ({duration:F1}s)</color>");
        }

        OnEffectApplied?.Invoke(activeEffect);
        OnEffectsChanged?.Invoke();
    }

    /// <summary>
    /// Immediately removes an active status effect of the specified type.
    /// </summary>
    public void RemoveEffect(StatusEffectType type)
    {
        if (activeEffects.TryGetValue(type, out var effect))
        {
            activeEffects.Remove(type);
            OnEffectExpired(effect);
            OnEffectsChanged?.Invoke();
        }
    }

    /// <summary>
    /// Clears all active status effects and resets visual overrides.
    /// </summary>
    public void ClearAllEffects()
    {
        if (activeEffects.Count == 0)
            return;

        foreach (var effect in activeEffects.Values)
        {
            OnEffectExpired(effect);
        }

        activeEffects.Clear();
        ResetVolumeOverrides();
        OnEffectsChanged?.Invoke();
    }

    private void OnEffectExpired(ActiveStatusEffect effect)
    {
        if (effect.type == StatusEffectType.GlitchDistortion || effect.type == StatusEffectType.Equalized)
        {
            ResetVolumeOverrides();
        }

        OnEffectRemoved?.Invoke(effect);
    }

    #region Stat Multipliers

    /// <summary>
    /// Calculates net speed multiplier across all active status effects.
    /// Returns 0 if Frozen. Multiplies buffs and debuffs together.
    /// </summary>
    public float GetSpeedMultiplier()
    {
        if (activeEffects.ContainsKey(StatusEffectType.Freeze))
            return 0f;

        float mult = 1f;

        // Buffs
        if (activeEffects.TryGetValue(StatusEffectType.Speed, out var speed))
            mult *= speed.GetSpeedMultiplier();
        if (activeEffects.TryGetValue(StatusEffectType.Groove, out var groove))
            mult *= groove.GetSpeedMultiplier();
        if (activeEffects.TryGetValue(StatusEffectType.Hype, out var hype))
            mult *= hype.GetSpeedMultiplier();
        if (activeEffects.TryGetValue(StatusEffectType.Sanctuary, out var sanc))
            mult *= sanc.GetSpeedMultiplier();

        // Debuffs
        if (activeEffects.TryGetValue(StatusEffectType.HeavyLegs, out var heavyLegs))
            mult *= heavyLegs.GetSpeedMultiplier();
        if (activeEffects.TryGetValue(StatusEffectType.Equalized, out var eq))
            mult *= eq.GetSpeedMultiplier();
        if (activeEffects.TryGetValue(StatusEffectType.WindStagger, out var wind))
            mult *= wind.GetSpeedMultiplier();
        if (activeEffects.TryGetValue(StatusEffectType.LagSpike, out var lag))
            mult *= lag.GetSpeedMultiplier();
        if (activeEffects.TryGetValue(StatusEffectType.Tremor, out var tremor))
            mult *= tremor.GetSpeedMultiplier();

        return Mathf.Max(0f, mult);
    }

    /// <summary>
    /// Calculates net damage taken multiplier considering Resistance, Sanctuary, and Vulnerability.
    /// </summary>
    public float GetDamageTakenMultiplier()
    {
        float mult = 1f;

        if (activeEffects.TryGetValue(StatusEffectType.Resistance, out var res))
            mult *= (1f - res.GetDamageResistance());
        if (activeEffects.TryGetValue(StatusEffectType.Sanctuary, out var sanc))
            mult *= (1f - sanc.GetDamageResistance());

        if (activeEffects.TryGetValue(StatusEffectType.Vulnerability, out var vuln))
            mult *= vuln.GetVulnerabilityMultiplier();
        if (activeEffects.TryGetValue(StatusEffectType.Equalized, out var eq))
            mult *= eq.GetVulnerabilityMultiplier();

        return Mathf.Max(0.05f, mult);
    }

    /// <summary>
    /// Calculates stamina regeneration multiplier from StaminaBoost, Groove, and Hype.
    /// </summary>
    public float GetStaminaRegenMultiplier()
    {
        float mult = 1f;

        if (activeEffects.TryGetValue(StatusEffectType.StaminaBoost, out var stam))
            mult *= stam.GetStaminaRegenMultiplier();
        if (activeEffects.TryGetValue(StatusEffectType.Groove, out var groove))
            mult *= groove.GetStaminaRegenMultiplier();
        if (activeEffects.TryGetValue(StatusEffectType.Hype, out var hype))
            mult *= hype.GetStaminaRegenMultiplier();

        return mult;
    }

    public bool HasEffect(StatusEffectType type) => activeEffects.ContainsKey(type);

    public bool IsFrozen => activeEffects.ContainsKey(StatusEffectType.Freeze);

    public IReadOnlyCollection<ActiveStatusEffect> GetActiveEffects() => activeEffects.Values;

    #endregion

    #region Networked RPCs

    [PunRPC]
    public void RPC_ApplyStatusEffect(byte effectType, int tier, float duration, float customMultiplier = 0f)
    {
        ApplyEffect((StatusEffectType)effectType, tier, duration, customMultiplier, announce: true);
    }

    [PunRPC]
    public void RPC_RemoveStatusEffect(byte effectType)
    {
        RemoveEffect((StatusEffectType)effectType);
    }

    [PunRPC]
    public void RPC_ClearAllStatusEffects()
    {
        ClearAllEffects();
    }

    #endregion

    #region Visual Overrides

    private void CacheVolume()
    {
        if (volumeCached) return;

        var volObj = GameObject.Find("Global Volume");
        if (volObj != null)
            cachedVolume = volObj.GetComponent<Volume>();
        else
            cachedVolume = FindFirstObjectByType<Volume>();

        if (cachedVolume != null && cachedVolume.profile != null)
        {
            cachedVolume.profile.TryGet(out chromatic);
            cachedVolume.profile.TryGet(out filmGrain);
            cachedVolume.profile.TryGet(out lensDistortion);
            cachedVolume.profile.TryGet(out vignette);

            if (chromatic != null)
                initialChromatic = chromatic.intensity.value;
            if (filmGrain != null)
                initialGrain = filmGrain.intensity.value;
            if (lensDistortion != null)
                initialDistortion = lensDistortion.intensity.value;
            if (vignette != null)
                initialVignette = vignette.intensity.value;

            volumeCached = true;
        }
    }

    private void ApplyGlitchDistortion(int tier)
    {
        CacheVolume();
        if (chromatic != null)
            chromatic.intensity.value = Mathf.Clamp01(0.4f + tier * 0.2f);
        if (filmGrain != null)
            filmGrain.intensity.value = Mathf.Clamp(8f + tier * 6f, 0f, 30f);
    }

    private void UpdateSensoryEffects()
    {
        if (!volumeCached || cachedVolume == null)
            return;

        bool hasGlitch = activeEffects.TryGetValue(StatusEffectType.GlitchDistortion, out var glitch);
        bool hasEqualized = activeEffects.TryGetValue(StatusEffectType.Equalized, out var eq);

        if (hasGlitch)
        {
            float jitter = Mathf.Sin(Time.time * 24f) * 0.12f + (Mathf.PerlinNoise(Time.time * 15f, 0f) * 0.1f);
            if (chromatic != null)
                chromatic.intensity.value = Mathf.Clamp01(0.4f + (glitch.tier * 0.18f) + jitter);
            if (lensDistortion != null)
                lensDistortion.intensity.value = Mathf.Sin(Time.time * 16f) * (0.08f * glitch.tier);
            if (filmGrain != null)
                filmGrain.intensity.value = Mathf.Clamp(6f + (glitch.tier * 5f) + (Mathf.PerlinNoise(0f, Time.time * 20f) * 8f), 0f, 30f);
        }

        if (hasEqualized)
        {
            if (vignette != null)
            {
                float pulse = Mathf.PingPong(Time.time * 3f, 0.2f);
                vignette.intensity.value = Mathf.Clamp01(initialVignette + 0.25f + pulse);
            }
        }
    }

    private void ResetVolumeOverrides()
    {
        if (chromatic != null)
            chromatic.intensity.value = initialChromatic;
        if (filmGrain != null)
            filmGrain.intensity.value = initialGrain;
        if (lensDistortion != null)
            lensDistortion.intensity.value = initialDistortion;
        if (vignette != null)
            vignette.intensity.value = initialVignette;
    }

    private void OnDisable()
    {
        ClearAllEffects();
        if (cachedMovement != null)
            cachedMovement.SpeedModifier = 1f;
    }

    #endregion
}
