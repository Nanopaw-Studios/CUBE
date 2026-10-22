using System;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using Photon.Voice.Unity;
using UnityEngine;
using PhotonPlayer = Photon.Realtime.Player;

/// <summary>
/// Local, per-account player muting.
///
/// - Mutes are LOCAL ONLY: nothing is sent over the network, no room/player properties are
///   changed, and the muted player's microphone / Recorder is never touched.
/// - Mutes are keyed by PlayFabId (persistent), never by ActorNumber or display name, so they
///   survive leaving/rejoining rooms and restarting the game.
/// - Persisted in PlayerPrefs, scoped to the logged-in PlayFab account on this device.
///
/// Communication channels covered: Photon Voice (the only player-to-player channel in CUBE;
/// there is no text chat). If text chat is added later, it must filter with IsMuted().
/// </summary>
public static class PlayerMuteService
{
    public const int MaxMutedPlayers = 200;
    private const string PrefsKeyPrefix = "CUBE.MutedPlayers.";

    [Serializable]
    private class MuteListData
    {
        public List<string> ids = new List<string>();
    }

    private static readonly HashSet<string> muted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private static string loadedForAccount; // PlayFabId the current set belongs to

    /// <summary>Raised with the PlayFabId whose mute state changed.</summary>
    public static event Action<string> OnMuteStateChanged;

    public enum MuteResult
    {
        Success,
        AlreadyInState,
        InvalidTarget,
        CannotMuteSelf,
        ListFull,
    }

    public static bool IsMuted(string playFabId)
    {
        EnsureLoaded();
        return !string.IsNullOrEmpty(playFabId) && muted.Contains(playFabId);
    }

    public static bool IsMuted(PhotonPlayer player) => IsMuted(PlayerSafetyIdentity.GetStableId(player));

    public static int Count
    {
        get
        {
            EnsureLoaded();
            return muted.Count;
        }
    }

    public static MuteResult SetMuted(string playFabId, bool mute)
    {
        EnsureLoaded();

        if (!PlayerSafetyIdentity.IsValidPlayFabId(playFabId))
            return MuteResult.InvalidTarget;

        string local = PlayerSafetyIdentity.LocalPlayFabId;
        if (mute && local != null && string.Equals(local, playFabId, StringComparison.OrdinalIgnoreCase))
            return MuteResult.CannotMuteSelf;

        if (mute == muted.Contains(playFabId))
            return MuteResult.AlreadyInState;

        if (mute && muted.Count >= MaxMutedPlayers)
            return MuteResult.ListFull;

        if (mute)
            muted.Add(playFabId);
        else
            muted.Remove(playFabId);

        Save();
        VoiceMuteApplier.EnsureExists();
        OnMuteStateChanged?.Invoke(playFabId);
        return MuteResult.Success;
    }

    public static MuteResult SetMuted(PhotonPlayer player, bool mute)
    {
        if (player != null && player.IsLocal)
            return MuteResult.CannotMuteSelf;
        return SetMuted(PlayerSafetyIdentity.GetStableId(player), mute);
    }

    /// <summary>
    /// Reloads the list if the logged-in account changed. Before login, the set is kept
    /// in memory only (not persisted) so it cannot leak across accounts.
    /// </summary>
    private static void EnsureLoaded()
    {
        string account = PlayerSafetyIdentity.LocalPlayFabId;
        if (account == loadedForAccount)
            return;

        // Do not discard in-memory mutes made before login resolved; merge them in.
        var pending = loadedForAccount == null ? new List<string>(muted) : null;

        muted.Clear();
        loadedForAccount = account;

        if (account != null)
        {
            string json = PlayerPrefs.GetString(PrefsKeyPrefix + account, string.Empty);
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    var data = JsonUtility.FromJson<MuteListData>(json);
                    if (data?.ids != null)
                    {
                        foreach (string id in data.ids)
                        {
                            if (PlayerSafetyIdentity.IsValidPlayFabId(id) && muted.Count < MaxMutedPlayers)
                                muted.Add(id);
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[PlayerMuteService] Mute list was unreadable and has been reset: {e.Message}");
                }
            }
        }

        if (pending != null)
        {
            foreach (string id in pending)
            {
                if (muted.Count >= MaxMutedPlayers)
                    break;
                if (!string.Equals(id, account, StringComparison.OrdinalIgnoreCase))
                    muted.Add(id);
            }
            if (pending.Count > 0)
                Save();
        }
    }

    private static void Save()
    {
        if (loadedForAccount == null)
            return; // Not logged in: session-only.

        var data = new MuteListData { ids = new List<string>(muted) };
        PlayerPrefs.SetString(PrefsKeyPrefix + loadedForAccount, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }
}

/// <summary>
/// Applies PlayerMuteService state to remote Photon Voice output.
///
/// Each remote player's BaseCharacter carries a Photon Voice <see cref="Speaker"/> whose
/// AudioSource plays that player's incoming voice stream. Muting sets AudioSource.mute on
/// THIS client only: the stream is still received (so unmuting is instant, no rejoin), the
/// remote player's mic is unaffected, and other players hear them normally.
///
/// Characters are respawned/re-instantiated across rounds, so speakers are re-scanned on a
/// short interval as well as immediately whenever mute state changes.
/// </summary>
public class VoiceMuteApplier : MonoBehaviour
{
    private const float RescanInterval = 1f;

    private static VoiceMuteApplier instance;

    // AudioSources this component muted, so unmuting never un-mutes anything we didn't mute.
    private readonly HashSet<AudioSource> mutedByUs = new HashSet<AudioSource>();
    private readonly List<AudioSource> staleBuffer = new List<AudioSource>();
    private float nextScan;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap() => EnsureExists();

    public static void EnsureExists()
    {
        if (instance != null)
            return;

        var go = new GameObject("[VoiceMuteApplier]");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<VoiceMuteApplier>();
    }

    private void OnEnable() => PlayerMuteService.OnMuteStateChanged += HandleMuteChanged;

    private void OnDisable() => PlayerMuteService.OnMuteStateChanged -= HandleMuteChanged;

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void HandleMuteChanged(string _) => ApplyAll();

    private void Update()
    {
        if (Time.unscaledTime < nextScan)
            return;
        nextScan = Time.unscaledTime + RescanInterval;
        ApplyAll();
    }

    private void ApplyAll()
    {
        // Drop destroyed sources (characters despawned / scene change).
        staleBuffer.Clear();
        foreach (var src in mutedByUs)
            if (src == null)
                staleBuffer.Add(src);
        foreach (var src in staleBuffer)
            mutedByUs.Remove(src);

        if (!PhotonNetwork.InRoom)
            return;

        Speaker[] speakers = FindObjectsByType<Speaker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Speaker speaker in speakers)
        {
            if (speaker == null)
                continue;

            AudioSource source = speaker.GetComponent<AudioSource>();
            if (source == null)
                continue;

            PhotonView pv = speaker.GetComponentInParent<PhotonView>(true);
            // Never touch the local player's own character or unowned speakers.
            bool shouldMute = pv != null && !pv.IsMine && pv.Owner != null &&
                (PlayerMuteService.IsMuted(pv.Owner) || (CrossPlatformManager.Instance != null && !CrossPlatformManager.Instance.CanHearVoice(pv.Owner)));

            if (shouldMute)
            {
                if (!source.mute)
                    source.mute = true;
                mutedByUs.Add(source);
            }
            else if (mutedByUs.Remove(source))
            {
                source.mute = false;
            }
        }
    }
}
