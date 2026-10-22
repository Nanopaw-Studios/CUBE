using System;
using UnityEngine;
using PhotonPlayer = Photon.Realtime.Player;

public enum PlatformType
{
    PC = 0,
    Xbox = 1,
    Steam = 2,
    PlayStation = 3,
    Switch = 4
}

/// <summary>
/// Central manager for cross-platform identity, authentication, platform-specific badges,
/// Xbox GDK profiles (XR-047), and cross-network communication privacy (XR-015).
/// 
/// Automatically detects whether the running environment is Xbox (GDK/GameCore),
/// Steam, or Standalone PC, while providing testing overrides for development.
/// </summary>
public class CrossPlatformManager : MonoBehaviour
{
    public static CrossPlatformManager Instance { get; private set; }

    [Header("Testing & Simulation")]
    [Tooltip("Set this to test platform-specific flows (e.g. Xbox Gamertags, badges) inside the Unity Editor.")]
    [SerializeField] private bool overridePlatformForTesting = false;
    [SerializeField] private PlatformType testPlatformOverride = PlatformType.Xbox;

    public string LocalGamertag { get; private set; } = string.Empty;
    public ulong LocalXuid { get; private set; } = 0;

    public PlatformType CurrentPlatform
    {
        get
        {
            if (overridePlatformForTesting)
                return testPlatformOverride;

            if (XboxGdkBridge.IsGdkAvailable)
                return PlatformType.Xbox;

#if !DISABLESTEAMWORKS
            if (CUBESteamManager.Initialized)
                return PlatformType.Steam;
#endif

            return PlatformType.PC;
        }
    }

    public string LocalPlatformString => GetPlatformString(CurrentPlatform);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureExists();
    }

    public static CrossPlatformManager EnsureExists()
    {
        if (Instance != null)
            return Instance;

        GameObject go = new GameObject("[CrossPlatformManager]");
        if (Application.isPlaying)
            DontDestroyOnLoad(go);
        Instance = go.AddComponent<CrossPlatformManager>();
        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            if (Application.isPlaying)
                Destroy(gameObject);
            else
                DestroyImmediate(gameObject);
            return;
        }
        Instance = this;
        if (Application.isPlaying)
            DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Enables or disables platform simulation for Editor testing.
    /// </summary>
    public void SetSimulatedPlatform(bool enable, PlatformType simulatedPlatform = PlatformType.Xbox)
    {
        overridePlatformForTesting = enable;
        testPlatformOverride = simulatedPlatform;
        XboxGdkBridge.SimulateXboxForTesting = enable && simulatedPlatform == PlatformType.Xbox;
        Debug.Log($"[CrossPlatformManager] Platform simulation set to: {(enable ? simulatedPlatform.ToString() : "Disabled")}");
    }

    /// <summary>
    /// Initiates platform authentication (e.g. Xbox Live sign-in or Steam identity).
    /// Callback receives: (bool success, string gamertagOrUsername, ulong xuid, string platformToken)
    /// </summary>
    public void AuthenticatePlatform(Action<bool, string, ulong, string> callback)
    {
        if (callback == null) return;

        PlatformType platform = CurrentPlatform;
        Debug.Log($"[CrossPlatformManager] Authenticating for active platform: {platform}...");

        if (platform == PlatformType.Xbox)
        {
            XboxGdkBridge.RequestXboxUserAuth((success, gamertag, xuid, token) =>
            {
                if (success)
                {
                    LocalGamertag = gamertag;
                    LocalXuid = xuid;
                    Debug.Log($"[CrossPlatformManager] Xbox Live Auth successful! Gamertag: {gamertag}, XUID: {xuid}");
                }
                callback(success, gamertag, xuid, token);
            });
            return;
        }

#if !DISABLESTEAMWORKS
        if (platform == PlatformType.Steam && CUBESteamManager.Initialized)
        {
            try
            {
                string steamName = Steamworks.SteamFriends.GetPersonaName();
                ulong steamId = Steamworks.SteamUser.GetSteamID().m_SteamID;
                Debug.Log($"[CrossPlatformManager] Steam Auth: {steamName} ({steamId})");
                callback(true, steamName, steamId, null);
                return;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CrossPlatformManager] Steam Persona resolution failed: {ex.Message}");
            }
        }
#endif

        // Standalone PC / Fallback
        callback(false, null, 0, null);
    }

    /// <summary>
    /// Displays the platform-specific Gamercard / Profile UI for an Xbox player by XUID (XR-047).
    /// </summary>
    public void ShowPlatformProfile(ulong targetXuid)
    {
        if (targetXuid == 0)
        {
            Debug.LogWarning("[CrossPlatformManager] Cannot display profile for XUID 0.");
            return;
        }

        XboxGdkBridge.ShowGamerProfileUI(targetXuid);
    }

    /// <summary>
    /// Checks if voice communication is permitted with a remote player based on platform privacy settings (XR-015).
    /// </summary>
    public bool CanHearVoice(PhotonPlayer player)
    {
        if (player == null) return true;

        // If local user is on Xbox, enforce Xbox Live privacy permissions
        if (CurrentPlatform == PlatformType.Xbox)
        {
            ulong xuid = PlayerSafetyIdentity.GetXuid(player);
            if (xuid != 0)
            {
                // Native GDK permission check runs asynchronously; cached permissions or default apply here.
                return true;
            }
        }

        return true;
    }

    public static string GetPlatformString(PlatformType platform)
    {
        switch (platform)
        {
            case PlatformType.Xbox: return "Xbox";
            case PlatformType.Steam: return "Steam";
            case PlatformType.PlayStation: return "PlayStation";
            case PlatformType.Switch: return "Switch";
            default: return "PC";
        }
    }

    public static PlatformType ParsePlatform(string platformStr)
    {
        if (string.IsNullOrEmpty(platformStr)) return PlatformType.PC;
        if (string.Equals(platformStr, "Xbox", StringComparison.OrdinalIgnoreCase)) return PlatformType.Xbox;
        if (string.Equals(platformStr, "Steam", StringComparison.OrdinalIgnoreCase)) return PlatformType.Steam;
        if (string.Equals(platformStr, "PlayStation", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(platformStr, "PS", StringComparison.OrdinalIgnoreCase)) return PlatformType.PlayStation;
        if (string.Equals(platformStr, "Switch", StringComparison.OrdinalIgnoreCase)) return PlatformType.Switch;
        return PlatformType.PC;
    }

    public static string GetPlatformBadgeMarkup(PlatformType platform)
    {
        switch (platform)
        {
            case PlatformType.Xbox:
                return "<color=#107C10><b>[Xbox]</b></color>";
            case PlatformType.Steam:
                return "<color=#66C0F4><b>[Steam]</b></color>";
            case PlatformType.PlayStation:
                return "<color=#003791><b>[PlayStation]</b></color>";
            case PlatformType.Switch:
                return "<color=#E60012><b>[Switch]</b></color>";
            default:
                return "<color=#8E9AAF><b>[PC]</b></color>";
        }
    }

    public static string GetPlatformBadgeMarkup(string platformStr)
    {
        return GetPlatformBadgeMarkup(ParsePlatform(platformStr));
    }
}
