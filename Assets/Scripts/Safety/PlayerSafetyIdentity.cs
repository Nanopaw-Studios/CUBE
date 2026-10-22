using System.Text;
using System.Text.RegularExpressions;
using Photon.Pun;
using Photon.Realtime;
using PhotonPlayer = Photon.Realtime.Player;

/// <summary>
/// Resolves stable player identities for reporting and muting.
///
/// CUBE's identity model (see PlayfabManager.OnPhotonAuthSuccess):
///  - Persistent account id = PlayFabId. It is assigned to Photon AuthValues.UserId
///    (validated by Photon when PlayFab custom authentication is enabled on the
///    Photon dashboard) and is also copied into PhotonNetwork.NickName.
///  - Transient session id  = Photon ActorNumber (changes every join, never persisted).
///  - Display name          = PlayFab title display name, broadcast in the "n" custom
///    property by PlayerNameSyncer. Not unique and not trusted as an identifier.
/// </summary>
public static class PlayerSafetyIdentity
{
    // Must match PlayerNameSyncer constants.
    private const string DisplayNameProperty = "n";
    public const string PlatformProperty = "plt";
    public const string XuidProperty = "xuid";
    public const string GamertagProperty = "gt";
    private const int MaxDisplayNameLength = 32;

    // PlayFab master player ids are hexadecimal strings (typically 16 chars).
    private static readonly Regex PlayFabIdPattern = new Regex("^[0-9A-Fa-f]{8,32}$", RegexOptions.Compiled);

    public static bool IsValidPlayFabId(string id) => !string.IsNullOrEmpty(id) && PlayFabIdPattern.IsMatch(id);

    /// <summary>The local player's PlayFabId, or null if not logged in yet.</summary>
    public static string LocalPlayFabId
    {
        get
        {
            string id = PlayfabManager.Instance != null ? PlayfabManager.Instance.LocalPlayFabId : null;
            if (string.IsNullOrEmpty(id))
                id = PhotonNetwork.AuthValues != null ? PhotonNetwork.AuthValues.UserId : null;
            return IsValidPlayFabId(id) ? id : null;
        }
    }

    /// <summary>
    /// Returns the persistent PlayFabId for a Photon player, or null if it cannot be determined.
    /// Prefers Player.UserId (requires RoomOptions.PublishUserId, set in PhotonMenuConnector);
    /// falls back to NickName, which CUBE sets to the PlayFabId on login.
    /// </summary>
    public static string GetStableId(PhotonPlayer player)
    {
        if (player == null)
            return null;

        if (IsValidPlayFabId(player.UserId))
            return player.UserId;

        if (IsValidPlayFabId(player.NickName))
            return player.NickName;

        return null;
    }

    public static bool IsLocalPlayer(PhotonPlayer player)
    {
        if (player == null)
            return false;
        if (player.IsLocal)
            return true;

        string local = LocalPlayFabId;
        string id = GetStableId(player);
        return local != null && id != null && string.Equals(local, id, System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns a display-safe name for UI. Strips control characters and caps length.
    /// Always render with TMP rich text disabled so names cannot inject markup.
    /// </summary>
    public static string GetDisplayName(PhotonPlayer player)
    {
        if (player == null)
            return "Unknown player";

        string raw = null;
        if (player.CustomProperties != null && player.CustomProperties.TryGetValue(DisplayNameProperty, out object value))
            raw = value as string;

        string clean = SanitizeSingleLine(raw, MaxDisplayNameLength);
        return string.IsNullOrEmpty(clean) ? $"Player {player.ActorNumber}" : clean;
    }

    /// <summary>Removes control characters, collapses whitespace and truncates.</summary>
    public static string SanitizeSingleLine(string input, int maxLength)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        var sb = new StringBuilder(input.Length);
        bool lastWasSpace = false;
        foreach (char c in input)
        {
            if (char.IsControl(c) || char.IsWhiteSpace(c))
            {
                if (!lastWasSpace && sb.Length > 0)
                    sb.Append(' ');
                lastWasSpace = true;
                continue;
            }
            sb.Append(c);
            lastWasSpace = false;
        }

        string result = sb.ToString().Trim();
        if (result.Length > maxLength)
            result = result.Substring(0, maxLength).TrimEnd();
        return result;
    }

    /// <summary>
    /// Returns the player's platform identifier string ("Xbox", "Steam", "PC", etc.) or "PC" as fallback.
    /// </summary>
    public static string GetPlatform(PhotonPlayer player)
    {
        if (player != null && player.CustomProperties != null &&
            player.CustomProperties.TryGetValue(PlatformProperty, out object val))
        {
            string s = val as string;
            if (!string.IsNullOrEmpty(s)) return s;
        }
        return "PC";
    }

    /// <summary>
    /// Returns the player's 64-bit Xbox User ID (XUID) if present in room custom properties, or 0.
    /// </summary>
    public static ulong GetXuid(PhotonPlayer player)
    {
        if (player != null && player.CustomProperties != null &&
            player.CustomProperties.TryGetValue(XuidProperty, out object val))
        {
            if (val is string s && ulong.TryParse(s, out ulong parsed)) return parsed;
            if (val is ulong u) return u;
            if (val is long l && l > 0) return (ulong)l;
        }
        return 0;
    }

    /// <summary>
    /// Returns the player's official platform Gamertag if available.
    /// </summary>
    public static string GetGamertag(PhotonPlayer player)
    {
        if (player != null && player.CustomProperties != null &&
            player.CustomProperties.TryGetValue(GamertagProperty, out object val))
        {
            return val as string;
        }
        return null;
    }

    /// <summary>
    /// Returns true if the player is connected from the Xbox network or has an active Xbox User ID.
    /// </summary>
    public static bool IsXboxPlayer(PhotonPlayer player)
    {
        if (player == null) return false;
        string plt = GetPlatform(player);
        return string.Equals(plt, "Xbox", System.StringComparison.OrdinalIgnoreCase) || GetXuid(player) != 0;
    }

    /// <summary>
    /// Returns a rich-text formatted badge for the player's platform (e.g. [Xbox], [Steam], [PC]).
    /// </summary>
    public static string GetFormattedPlatformBadge(PhotonPlayer player)
    {
        string plt = GetPlatform(player);
        return CrossPlatformManager.GetPlatformBadgeMarkup(plt);
    }
}

