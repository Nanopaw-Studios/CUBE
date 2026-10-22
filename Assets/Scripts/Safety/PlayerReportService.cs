using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Photon.Pun;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

/// <summary>A configurable report category. Id is sent to the backend; Label is shown in UI.</summary>
public readonly struct ReportCategory
{
    public readonly string Id;
    public readonly string Label;

    public ReportCategory(string id, string label)
    {
        Id = id;
        Label = label;
    }
}

/// <summary>
/// Report categories shown in the report flow. Edit this list to change categories; Ids are
/// stable machine values used by moderators to filter reports, so avoid renaming existing ones.
/// </summary>
public static class PlayerReportCategories
{
    public static readonly IReadOnlyList<ReportCategory> All = new[]
    {
        new ReportCategory("harassment", "Harassment or bullying"),
        new ReportCategory("hate_speech", "Hate speech or discrimination"),
        new ReportCategory("inappropriate_name", "Inappropriate player name"),
        new ReportCategory("cheating", "Cheating or exploiting"),
        new ReportCategory("inappropriate_voice", "Inappropriate voice communication"),
        new ReportCategory("griefing", "Griefing or intentional disruption"),
        new ReportCategory("other", "Other"),
    };

    public static bool TryGet(string id, out ReportCategory category)
    {
        foreach (var c in All)
        {
            if (c.Id == id)
            {
                category = c;
                return true;
            }
        }
        category = default;
        return false;
    }
}

/// <summary>
/// Data captured for a single report. The REPORTER's identity is intentionally NOT part of this
/// model: PlayFab derives it server-side from the authenticated session ticket, so it cannot be
/// spoofed by the client and is never transmitted to other players.
/// </summary>
public class PlayerReport
{
    public string ReportId;
    public string ReporteePlayFabId;
    public string ReporteeDisplayName; // Snapshot; names can change and may be the reported content.
    public string CategoryId;
    public string Details;             // Optional, sanitised, length-capped.
    public string SessionId;           // Photon room name (match identifier).
    public string GameVersion;
    public string Platform;
    public DateTime CreatedUtc;
}

public enum ReportSubmitStatus
{
    Success,
    ValidationFailed,
    NotLoggedIn,
    RateLimited,
    DailyLimitReached,
    Duplicate,
    InProgress,
    NetworkError,
    ServerError,
}

public readonly struct ReportSubmitResult
{
    public readonly ReportSubmitStatus Status;
    public readonly string Message;           // User-facing text.
    public readonly int SubmissionsRemaining; // -1 if unknown.

    public ReportSubmitResult(ReportSubmitStatus status, string message, int remaining = -1)
    {
        Status = status;
        Message = message;
        SubmissionsRemaining = remaining;
    }

    public bool IsSuccess => Status == ReportSubmitStatus.Success;

    /// <summary>True when retrying the same report later could succeed.</summary>
    public bool CanRetry => Status == ReportSubmitStatus.NetworkError || Status == ReportSubmitStatus.ServerError
        || Status == ReportSubmitStatus.RateLimited || Status == ReportSubmitStatus.NotLoggedIn;
}

/// <summary>
/// Submits player reports to PlayFab via the Client <c>ReportPlayer</c> API.
///
/// Trust model:
///  - PlayFab authenticates the reporter from the session ticket (client cannot forge it).
///  - PlayFab enforces a server-side limit of 5 reports per player per day.
///  - Reports are recorded by PlayFab (visible to the title's moderators via PlayStream /
///    Game Manager), never sent to other Photon clients.
/// Client-side checks below exist for immediate feedback and to avoid wasting the daily quota;
/// they are NOT the security boundary.
/// </summary>
public static class PlayerReportService
{
    public const int MaxDetailsLength = 300;
    public const float MinSecondsBetweenSubmissions = 10f;
    private const int PlayFabDailyLimit = 5;
    private const string DailyPrefsPrefix = "CUBE.ReportQuota.";

    private static bool inFlight;
    private static float lastSubmitTime = -999f;

    // Targets already reported in the current room by this client: "{roomName}|{playFabId}".
    private static readonly HashSet<string> reportedThisSession = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public static bool IsSubmitting => inFlight;

    public static float CooldownRemaining =>
        Mathf.Max(0f, MinSecondsBetweenSubmissions - (Time.unscaledTime - lastSubmitTime));

    public static bool HasReportedThisSession(string playFabId) =>
        !string.IsNullOrEmpty(playFabId) && reportedThisSession.Contains(SessionKey(playFabId));

    /// <summary>
    /// Builds a validated report. Returns null and sets <paramref name="error"/> on invalid input.
    /// </summary>
    public static PlayerReport CreateReport(string reporteePlayFabId, string reporteeDisplayName, string categoryId,
        string details, out string error)
    {
        error = null;

        if (!PlayerSafetyIdentity.IsValidPlayFabId(reporteePlayFabId))
        {
            error = "This player can't be reported because their account couldn't be identified.";
            return null;
        }

        string local = PlayerSafetyIdentity.LocalPlayFabId;
        if (local != null && string.Equals(local, reporteePlayFabId, StringComparison.OrdinalIgnoreCase))
        {
            error = "You can't report yourself.";
            return null;
        }

        if (!PlayerReportCategories.TryGet(categoryId, out _))
        {
            error = "Please choose a reason for your report.";
            return null;
        }

        string cleanDetails = SanitizeDetails(details);

        return new PlayerReport
        {
            ReportId = Guid.NewGuid().ToString("N"),
            ReporteePlayFabId = reporteePlayFabId,
            ReporteeDisplayName = PlayerSafetyIdentity.SanitizeSingleLine(reporteeDisplayName, 32),
            CategoryId = categoryId,
            Details = cleanDetails,
            SessionId = PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.Name : string.Empty,
            GameVersion = Application.version,
            Platform = Application.platform.ToString(),
            CreatedUtc = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Submits a report. <paramref name="onComplete"/> is always invoked exactly once on the main
    /// thread. Callers that may be destroyed before completion must null-check themselves.
    /// </summary>
    public static void Submit(PlayerReport report, Action<ReportSubmitResult> onComplete)
    {
        if (onComplete == null)
            throw new ArgumentNullException(nameof(onComplete));

        if (report == null || !PlayerSafetyIdentity.IsValidPlayFabId(report.ReporteePlayFabId)
            || !PlayerReportCategories.TryGet(report.CategoryId, out _))
        {
            onComplete(new ReportSubmitResult(ReportSubmitStatus.ValidationFailed, "This report is incomplete. Please try again."));
            return;
        }

        if (inFlight)
        {
            onComplete(new ReportSubmitResult(ReportSubmitStatus.InProgress, "A report is already being sent."));
            return;
        }

        if (HasReportedThisSession(report.ReporteePlayFabId))
        {
            onComplete(new ReportSubmitResult(ReportSubmitStatus.Duplicate,
                "You've already reported this player during this match. Our team will review it."));
            return;
        }

        if (CooldownRemaining > 0f)
        {
            onComplete(new ReportSubmitResult(ReportSubmitStatus.RateLimited,
                $"Please wait {Mathf.CeilToInt(CooldownRemaining)} seconds before sending another report."));
            return;
        }

        if (GetKnownRemainingToday() == 0)
        {
            onComplete(new ReportSubmitResult(ReportSubmitStatus.DailyLimitReached,
                "You've reached today's report limit. Please try again tomorrow.", 0));
            return;
        }

        if (!PlayFabClientAPI.IsClientLoggedIn())
        {
            onComplete(new ReportSubmitResult(ReportSubmitStatus.NotLoggedIn,
                "You're not signed in to CUBE services right now. Please try again in a moment."));
            return;
        }

        var request = new ReportPlayerClientRequest
        {
            ReporteeId = report.ReporteePlayFabId,
            Comment = BuildComment(report),
            CustomTags = new Dictionary<string, string>
            {
                { "report_id", report.ReportId },
                { "category", report.CategoryId },
                { "session_id", Truncate(report.SessionId, 64) },
                { "game_version", Truncate(report.GameVersion, 32) },
                { "platform", Truncate(report.Platform, 32) },
            },
        };

        inFlight = true;
        lastSubmitTime = Time.unscaledTime;
        int knownBefore = GetKnownRemainingToday();

        PlayFabClientAPI.ReportPlayer(
            request,
            result =>
            {
                inFlight = false;
                reportedThisSession.Add(SessionKey(report.ReporteePlayFabId));
                SetKnownRemainingToday(result.SubmissionsRemaining);

                // Log ids only; never log the free-text details.
                Debug.Log($"[PlayerReportService] Report {report.ReportId} ({report.CategoryId}) accepted. Remaining today: {result.SubmissionsRemaining}");

                string message = "Thanks — your report was sent to the CUBE team for review. "
                    + "Reports are confidential and the player won't be told who reported them.";

                // PlayFab returns success even past the daily limit, without recording the report.
                // If we had no prior knowledge for today and the quota is now 0, be honest.
                if (result.SubmissionsRemaining <= 0 && knownBefore < 0)
                    message = "Your report was sent, but you've now hit today's report limit. If you'd already sent "
                        + $"{PlayFabDailyLimit} reports today, this one may not have been recorded.";
                else if (result.SubmissionsRemaining <= 0)
                    message += " You've reached today's report limit.";

                onComplete(new ReportSubmitResult(ReportSubmitStatus.Success, message, result.SubmissionsRemaining));
            },
            error =>
            {
                inFlight = false;
                // Allow an immediate retry after a failure that never reached the server.
                if (error.Error == PlayFabErrorCode.ConnectionError || error.Error == PlayFabErrorCode.ServiceUnavailable)
                    lastSubmitTime = -999f;

                Debug.LogWarning($"[PlayerReportService] Report {report.ReportId} failed: {error.Error} (HTTP {error.HttpCode})");
                onComplete(MapError(error));
            });
    }

    /// <summary>Clears per-match duplicate tracking (call when leaving a room).</summary>
    public static void ResetSession() => reportedThisSession.Clear();

    private static ReportSubmitResult MapError(PlayFabError error)
    {
        switch (error.Error)
        {
            case PlayFabErrorCode.ConnectionError:
            case PlayFabErrorCode.ServiceUnavailable:
                return new ReportSubmitResult(ReportSubmitStatus.NetworkError,
                    "Couldn't reach the server. Check your connection and try again.");
            case PlayFabErrorCode.APIClientRequestRateLimitExceeded:
                return new ReportSubmitResult(ReportSubmitStatus.RateLimited,
                    "Too many requests. Please wait a moment and try again.");
            case PlayFabErrorCode.NotAuthenticated:
            case PlayFabErrorCode.NotAuthorized:
            case PlayFabErrorCode.InvalidSessionTicket:
            case PlayFabErrorCode.ExpiredAuthToken:
                return new ReportSubmitResult(ReportSubmitStatus.NotLoggedIn,
                    "Your session has expired. Please restart the game and try again.");
            case PlayFabErrorCode.AccountNotFound:
            case PlayFabErrorCode.InvalidParams:
                return new ReportSubmitResult(ReportSubmitStatus.ValidationFailed,
                    "This player's account couldn't be found, so the report couldn't be sent.");
            default:
                return new ReportSubmitResult(ReportSubmitStatus.ServerError,
                    "Something went wrong sending your report. Please try again.");
        }
    }

    private static string BuildComment(PlayerReport r)
    {
        PlayerReportCategories.TryGet(r.CategoryId, out var cat);
        var sb = new StringBuilder();
        sb.Append("[").Append(r.CategoryId).Append("] ").Append(cat.Label);
        sb.Append(" | name: ").Append(string.IsNullOrEmpty(r.ReporteeDisplayName) ? "(none)" : r.ReporteeDisplayName);
        sb.Append(" | session: ").Append(string.IsNullOrEmpty(r.SessionId) ? "(none)" : r.SessionId);
        sb.Append(" | v").Append(r.GameVersion);
        sb.Append(" | ").Append(r.CreatedUtc.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
        sb.Append(" | id: ").Append(r.ReportId);
        if (!string.IsNullOrEmpty(r.Details))
            sb.Append(" | details: ").Append(r.Details);
        return sb.ToString();
    }

    /// <summary>Strips control chars (newlines become spaces), trims and enforces the length cap.</summary>
    public static string SanitizeDetails(string details) =>
        PlayerSafetyIdentity.SanitizeSingleLine(details, MaxDetailsLength);

    private static string Truncate(string s, int max) =>
        string.IsNullOrEmpty(s) ? string.Empty : (s.Length <= max ? s : s.Substring(0, max));

    private static string SessionKey(string playFabId)
    {
        string room = PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.Name : "-";
        return room + "|" + playFabId;
    }

    // ---- Daily quota cache (hint only; PlayFab is authoritative) ----

    private static string QuotaKey()
    {
        string local = PlayerSafetyIdentity.LocalPlayFabId ?? "anon";
        return DailyPrefsPrefix + local;
    }

    private static string TodayUtc() => DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>Last remaining count PlayFab reported today, or -1 if unknown.</summary>
    private static int GetKnownRemainingToday()
    {
        string raw = PlayerPrefs.GetString(QuotaKey(), string.Empty);
        int sep = raw.IndexOf('|');
        if (sep <= 0 || raw.Substring(0, sep) != TodayUtc())
            return -1;
        return int.TryParse(raw.Substring(sep + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : -1;
    }

    private static void SetKnownRemainingToday(int remaining)
    {
        PlayerPrefs.SetString(QuotaKey(), TodayUtc() + "|" + Mathf.Max(0, remaining).ToString(CultureInfo.InvariantCulture));
        PlayerPrefs.Save();
    }
}
