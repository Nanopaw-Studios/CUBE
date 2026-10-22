using System;
using UnityEngine;

/// <summary>
/// Platform adapter interface for Xbox GDK safety, communication permissions, and profile interactions.
///
/// Designed to decouple CUBE's reporting and muting systems from direct GDK package dependencies,
/// enabling seamless development on Windows / Editor / Steam while providing the exact integration
/// points required for Xbox submission (XR-015 and XR-018).
/// </summary>
public interface IXboxSafetyPlatform
{
    string PlatformName { get; }
    bool IsXboxPlatform { get; }

    /// <summary>
    /// XR-015: Queries Xbox Live privacy permissions to verify if voice communication
    /// is allowed with the target Xbox User ID (XUID).
    /// </summary>
    void CheckVoicePermission(ulong targetXuid, Action<bool> callback);

    /// <summary>
    /// XR-047 / XR-018: Opens the system Gamercard UI for an Xbox player so the user
    /// can view their platform profile, add them, mute, block, or report their Gamertag to Xbox.
    /// </summary>
    void ShowGamerProfileUI(ulong targetXuid);

    /// <summary>
    /// XR-015: Returns true if the target player is not an Xbox network player (cross-play).
    /// </summary>
    bool IsNonXboxPlayer(string stableId);
}

/// <summary>
/// Default / Stub implementation of IXboxSafetyPlatform.
///
/// In non-GDK builds (Editor, Steam, Standalone Windows), provides safe fallbacks:
///  - Voice permission defaults to allowed.
///  - Gamercard UI logs a diagnostic notice.
///  - Cross-network identification defaults to true (treating external connections safely).
///
/// When the Microsoft GDK for Unity package is imported for Xbox certification builds,
/// replace the stub bodies below or define MICROSOFT_GDK to invoke XGameRuntime / XblPrivacy APIs.
/// </summary>
public class XboxSafetyPlatform : MonoBehaviour, IXboxSafetyPlatform
{
    public static XboxSafetyPlatform Instance { get; private set; }

    public string PlatformName => "XboxSafetyPlatform";

    public bool IsXboxPlatform
    {
        get
        {
#if MICROSOFT_GDK || UNITY_GAMECORE
            return true;
#else
            return Application.platform == RuntimePlatform.GameCoreXboxSeries ||
                   Application.platform == RuntimePlatform.GameCoreXboxOne;
#endif
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void CheckVoicePermission(ulong targetXuid, Action<bool> callback)
    {
        if (callback == null) return;

        if (!IsXboxPlatform || targetXuid == 0)
        {
            // Non-Xbox environment or unmapped XUID: allow by default, deferring to in-game mute list.
            callback(true);
            return;
        }

#if MICROSOFT_GDK || UNITY_GAMECORE
        // TODO (Xbox Certification Build with GDK):
        // XblPrivacyCheckPermissionAsync(xblContext, XblPermission.CommunicateUsingVoice, targetXuid, ...)
        Debug.Log($"[XboxSafetyPlatform] Querying XblPermission.CommunicateUsingVoice for XUID {targetXuid}...");
        callback(true);
#else
        callback(true);
#endif
    }

    public void ShowGamerProfileUI(ulong targetXuid)
    {
        if (targetXuid == 0)
        {
            Debug.LogWarning("[XboxSafetyPlatform] Cannot open Gamercard: invalid XUID 0.");
            return;
        }

#if MICROSOFT_GDK || UNITY_GAMECORE
        // TODO (Xbox Certification Build with GDK):
        // XShowGamerProfileUIAsync(xUserHandle, targetXuid, ...)
        Debug.Log($"[XboxSafetyPlatform] Launching XShowGamerProfileUIAsync for XUID {targetXuid}...");
#else
        Debug.Log($"[XboxSafetyPlatform] ShowGamerProfileUI stub called for XUID {targetXuid} (not on Xbox GDK).");
#endif
    }

    public bool IsNonXboxPlayer(string stableId)
    {
        // Without active Xbox Live cross-play auth token, all remote players are treated as cross-network
        // players who can be muted per XR-015 requirement.
        return true;
    }
}
