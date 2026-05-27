using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CUBEDiscordManager : MonoBehaviour
{
    [Header("Large Image Key")]
    public string largeImageKey = "game_logo";

    [Header("Role Image Keys (match keys in Discord Dev Portal)")]
    public string GlitchImgKey = "glitch";
    public string TitanImgKey = "titan";
    public string CoderImgKey = "coder";
    public string DJImgKey = "dj";
    public string HealerImgKey = "healer";

    [Header("Update")]
    [Min(0.25f)]
    public float updateInterval = 5f;

    private RoundManager roundManager;
    private BasePlayer basePlayer;
    private float nextUpdateTime;

    private Dictionary<string, string> activeJoinSecrets = new Dictionary<string, string>();

    private struct PresenceData
    {
        public string state,
            details,
            largeText,
            smallImageKey;
        public int partySize,
            partyMax;
        public string partyId,
            joinSecret;
    }

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);

        var all = FindObjectsByType<CUBEDiscordManager>(FindObjectsSortMode.None);
        if (all.Length > 1)
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        if (LaymsDiscordRPC.current == null)
        {
            Debug.LogWarning("[Discord] LaymsDiscordRPC not found in scene.");
            return;
        }

        if (!LaymsDiscordRPC.current.Connected)
        {
            Debug.LogWarning("[Discord] Not connected to Discord. Rich Presence will be skipped.");
            return;
        }

        SceneManager.activeSceneChanged += OnSceneChanged;
        StartCoroutine(PushPresenceNextFrame(BuildMainMenuPresence));
    }

    private void OnDestroy()
    {
        SceneManager.activeSceneChanged -= OnSceneChanged;
        UnhookRoundManager();
    }

    private void OnSceneChanged(Scene oldScene, Scene newScene)
    {
        UnhookRoundManager();
        basePlayer = null;

        if (newScene.name == "SampleScene")
        {
            roundManager = FindFirstObjectByType<RoundManager>();

            if (roundManager == null)
            {
                Debug.LogWarning("[Discord] No RoundManager found in SampleScene.");
                return;
            }

            roundManager.OnRoundStart += OnRoundStart;
            roundManager.OnIntermissionStart += OnIntermissionStart;
            nextUpdateTime = Time.time;
            StartCoroutine(PushPresenceNextFrame(BuildGameplayPresence));
        }
        else
        {
            StartCoroutine(PushPresenceNextFrame(BuildMainMenuPresence));
        }
    }

    private void UnhookRoundManager()
    {
        if (roundManager == null)
            return;
        roundManager.OnRoundStart -= OnRoundStart;
        roundManager.OnIntermissionStart -= OnIntermissionStart;
        roundManager = null;
    }

    private void Update()
    {
        if (roundManager == null)
            return;
        if (Time.time >= nextUpdateTime)
        {
            SendPresence(BuildGameplayPresence());
            nextUpdateTime = Time.time + updateInterval;
        }
    }

    private void OnRoundStart() => SendPresence(BuildGameplayPresence());

    private void OnIntermissionStart() => SendPresence(BuildGameplayPresence());

    // -------- Presence builders --------

    private PresenceData BuildMainMenuPresence()
    {
        string state =
            PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom != null
                ? $"{PhotonNetwork.CurrentRoom.PlayerCount}/{PhotonNetwork.CurrentRoom.MaxPlayers} players"
                : "Browsing Menus";

        return new PresenceData
        {
            state = state,
            details = "In Main Menu",
            largeText = "Main Menu",
            smallImageKey = "",
            partySize = 0,
            partyMax = 0,
            partyId = "",
            joinSecret = "",
        };
    }

    private PresenceData BuildGameplayPresence()
    {
        bool inRound = roundManager != null && roundManager.roundActive;
        string state = inRound ? "In Round" : "Intermission";

        string details = "In Intermission";
        string smallImageKey = "";

        if (inRound)
        {
            var player = GetBasePlayer();
            if (player != null)
            {
                if (player.GetPlayerType() == BasePlayer.PlayerType.Killer)
                {
                    var killer = player.GetEquippedKiller();

                    string killerName =
                        killer != null && killer.character != null ? killer._Name : "Unknown";

                    details = $"Playing Killer: {killerName}";
                    smallImageKey = GetRoleImageKey(killerName);
                }
                else
                {
                    var survivor = player.GetEquippedSurvivor();

                    string survivorName =
                        survivor != null && survivor.character != null ? survivor._Name : "Unknown";

                    details = $"Playing Survivor: {survivorName}";
                    smallImageKey = GetRoleImageKey(survivorName);
                }
            }
            else
            {
                details = "In Round";
            }
        }

        int partySize = 0,
            partyMax = 0;
        string partyId = "",
            joinSecret = "",
            largeText = "";

        if (PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom != null)
        {
            partySize = PhotonNetwork.CurrentRoom.PlayerCount;
            partyMax = PhotonNetwork.CurrentRoom.MaxPlayers;
            partyId = PhotonNetwork.CurrentRoom.Name;
            largeText = $"{partySize}/{partyMax} players";

            if (!activeJoinSecrets.TryGetValue(partyId, out joinSecret))
            {
                joinSecret = $"{LaymsDiscordRPC.current.applicationId}:{partyId}";
                activeJoinSecrets[partyId] = joinSecret;
            }
        }

        return new PresenceData
        {
            state = state,
            details = details,
            largeText = largeText,
            smallImageKey = smallImageKey,
            partySize = partySize,
            partyMax = partyMax,
            partyId = partyId,
            joinSecret = joinSecret,
        };
    }

    // -------- Helpers --------

    private BasePlayer GetBasePlayer()
    {
        if (basePlayer != null)
            return basePlayer;
        var camRefs = Camera.main?.GetComponent<PlayerCameraReferences>();
        basePlayer = camRefs?.GetPlayer()?.GetComponent<BasePlayer>();
        return basePlayer;
    }

    private string GetRoleImageKey(string roleName)
    {
        if (string.IsNullOrEmpty(roleName))
            return "";

        string clean = roleName.Trim().ToLower();

        Debug.Log($"[Discord] RoleName RAW: '{roleName}' | CLEAN: '{clean}'");

        if (clean.Contains("glitch"))
            return GlitchImgKey;
        if (clean.Contains("titan"))
            return TitanImgKey;
        if (clean.Contains("coder"))
            return CoderImgKey;
        if (clean == "dj" || clean.Contains("dj"))
            return DJImgKey;
        if (clean.Contains("healer"))
            return HealerImgKey;

        Debug.LogWarning($"[Discord] No image match for role: {clean}");
        return "";
    }

    // -------- Sending --------

    private void SendPresence(PresenceData p)
    {
        if (LaymsDiscordRPC.current == null || !LaymsDiscordRPC.current.Connected)
            return;

        Debug.Log($"[Discord] Sending presence: {p.state} | {p.details}");

        LaymsDiscordRPC.current.SetPresence(
            state: p.state,
            details: p.details,
            largeImageKey: largeImageKey,
            largeImageText: p.largeText,
            smallImageKey: p.smallImageKey,
            smallImageText: p.details,
            partySize: p.partySize,
            partyMax: p.partyMax,
            partyId: p.partyId,
            joinSecret: p.joinSecret
        );
    }

    private IEnumerator PushPresenceNextFrame(Func<PresenceData> builder)
    {
        yield return null;
        yield return null;
        SendPresence(builder());
    }
}
