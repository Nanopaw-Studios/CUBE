using System;
using UnityEngine;

/// <summary>
/// Manages player preferences, default values, and runtime notifications
/// for the developer-exclusive secondary physics system.
/// </summary>
public static class DevPhysicsSettings
{
    // PlayerPrefs Keys
    public const string KeyEnabled = "DevPhysics_Enabled";
    public const string KeySpringPower = "DevPhysics_SpringPower";
    public const string KeyDamping = "DevPhysics_Damping";
    public const string KeyInertia = "DevPhysics_Inertia";
    public const string KeyLimitDistance = "DevPhysics_LimitDistance";
    public const string KeySizeMultiplier = "DevPhysics_SizeMultiplier";
    public const string KeyVerticalOffset = "DevPhysics_VerticalOffset";

    // Defaults (tuned from magicaPreset.json)
    public const float DefaultSpringPower = 0.01f;
    public const float DefaultDamping = 0.2f;
    public const float DefaultInertia = 1.0f;
    public const float DefaultLimitDistance = 0.05f;
    public const float DefaultSizeMultiplier = 1.0f;
    public const float DefaultVerticalOffset = 0.0f;
    public const bool DefaultEnabled = true;

    private const string KeyPresetVersion = "DevPhysics_PresetVersion";
    private const int CurrentPresetVersion = 2;

    static DevPhysicsSettings()
    {
        // Migrate to magicaPreset defaults if first time running with new preset
        if (PlayerPrefs.GetInt(KeyPresetVersion, 0) < CurrentPresetVersion)
        {
            ResetToDefaults();
            PlayerPrefs.SetInt(KeyPresetVersion, CurrentPresetVersion);
            PlayerPrefs.Save();
        }
    }

    // Ranges for sliders
    public const float MinSpringPower = 0.001f;
    public const float MaxSpringPower = 0.2f;

    public const float MinDamping = 0.0f;
    public const float MaxDamping = 1.0f;

    public const float MinInertia = 0.0f;
    public const float MaxInertia = 1.0f;

    public const float MinLimitDistance = 0.005f;
    public const float MaxLimitDistance = 0.3f;

    public const float MinSizeMultiplier = 0.2f;
    public const float MaxSizeMultiplier = 3.0f;

    public const float MinVerticalOffset = -0.4f;
    public const float MaxVerticalOffset = 0.4f;

    public static event Action OnSettingsChanged;

    /// <summary>
    /// Safely invokes OnSettingsChanged while removing any stale or destroyed delegates,
    /// preventing MissingReferenceException from breaking subsequent handlers.
    /// </summary>
    public static void NotifySettingsChanged()
    {
        if (OnSettingsChanged == null) return;
        Delegate[] invocationList = OnSettingsChanged.GetInvocationList();
        foreach (Delegate d in invocationList)
        {
            if (d is Action action)
            {
                try
                {
                    if (action.Target is UnityEngine.Object unityObj && unityObj == null)
                    {
                        OnSettingsChanged -= action;
                        continue;
                    }
                    action();
                }
                catch (Exception ex)
                {
                    OnSettingsChanged -= action;
                    Debug.LogWarning($"[DevPhysicsSettings] Pruned stale event handler: {ex.Message}");
                }
            }
        }
    }

#if UNITY_EDITOR
    // Allows instant developer testing in the Unity Editor without live PlayFab setup
    public static bool EditorForceDevBadge = true;
#endif

    public static bool IsDevPhysicsEnabled
    {
        get => PlayerPrefs.GetInt(KeyEnabled, DefaultEnabled ? 1 : 0) == 1;
        set
        {
            PlayerPrefs.SetInt(KeyEnabled, value ? 1 : 0);
            PlayerPrefs.Save();
            NotifySettingsChanged();
        }
    }

    public static float SpringPower
    {
        get => Mathf.Clamp(PlayerPrefs.GetFloat(KeySpringPower, DefaultSpringPower), MinSpringPower, MaxSpringPower);
        set
        {
            PlayerPrefs.SetFloat(KeySpringPower, Mathf.Clamp(value, MinSpringPower, MaxSpringPower));
            PlayerPrefs.Save();
            NotifySettingsChanged();
        }
    }

    public static float Damping
    {
        get => Mathf.Clamp(PlayerPrefs.GetFloat(KeyDamping, DefaultDamping), MinDamping, MaxDamping);
        set
        {
            PlayerPrefs.SetFloat(KeyDamping, Mathf.Clamp(value, MinDamping, MaxDamping));
            PlayerPrefs.Save();
            NotifySettingsChanged();
        }
    }

    public static float Inertia
    {
        get => Mathf.Clamp(PlayerPrefs.GetFloat(KeyInertia, DefaultInertia), MinInertia, MaxInertia);
        set
        {
            PlayerPrefs.SetFloat(KeyInertia, Mathf.Clamp(value, MinInertia, MaxInertia));
            PlayerPrefs.Save();
            NotifySettingsChanged();
        }
    }

    public static float LimitDistance
    {
        get => Mathf.Clamp(PlayerPrefs.GetFloat(KeyLimitDistance, DefaultLimitDistance), MinLimitDistance, MaxLimitDistance);
        set
        {
            PlayerPrefs.SetFloat(KeyLimitDistance, Mathf.Clamp(value, MinLimitDistance, MaxLimitDistance));
            PlayerPrefs.Save();
            NotifySettingsChanged();
        }
    }

    public static float SizeMultiplier
    {
        get => Mathf.Clamp(PlayerPrefs.GetFloat(KeySizeMultiplier, DefaultSizeMultiplier), MinSizeMultiplier, MaxSizeMultiplier);
        set
        {
            PlayerPrefs.SetFloat(KeySizeMultiplier, Mathf.Clamp(value, MinSizeMultiplier, MaxSizeMultiplier));
            PlayerPrefs.Save();
            NotifySettingsChanged();
        }
    }

    public static float VerticalOffset
    {
        get => Mathf.Clamp(PlayerPrefs.GetFloat(KeyVerticalOffset, DefaultVerticalOffset), MinVerticalOffset, MaxVerticalOffset);
        set
        {
            PlayerPrefs.SetFloat(KeyVerticalOffset, Mathf.Clamp(value, MinVerticalOffset, MaxVerticalOffset));
            PlayerPrefs.Save();
            NotifySettingsChanged();
        }
    }

    public static void ResetToDefaults()
    {
        PlayerPrefs.SetInt(KeyEnabled, DefaultEnabled ? 1 : 0);
        PlayerPrefs.SetFloat(KeySpringPower, DefaultSpringPower);
        PlayerPrefs.SetFloat(KeyDamping, DefaultDamping);
        PlayerPrefs.SetFloat(KeyInertia, DefaultInertia);
        PlayerPrefs.SetFloat(KeyLimitDistance, DefaultLimitDistance);
        PlayerPrefs.SetFloat(KeySizeMultiplier, DefaultSizeMultiplier);
        PlayerPrefs.SetFloat(KeyVerticalOffset, DefaultVerticalOffset);
        PlayerPrefs.Save();
        NotifySettingsChanged();
    }
}
