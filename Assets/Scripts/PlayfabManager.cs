using System;
using System.Collections.Generic;
using System.Globalization;
using HSVPicker;
using Photon.Pun;
using Photon.Realtime;
using PlayFab;
using PlayFab.ClientModels;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayfabManager : MonoBehaviourPunCallbacks
{
    public static PlayfabManager Instance { get; private set; }

    [Header("Login UI")]
    public GameObject nameWindow;
    public TMP_InputField nameField;
    public TMP_Text infoText;
    public ColorPicker picker;

    [Header("Currency")]
    [Tooltip("PlayFab Virtual Currency code used for Cube Coins")]
    public string cubeCoinsCode = "CC";

    private TMP_Text moneyText;

    public int CachedCC { get; private set; }
    public event Action<int> OnCubeCoinsChanged;

    public bool IsLoggedIn { get; private set; }
    public event Action OnLoginSuccessEvent;

    string PlayFabId;
    static string displayName;

    /// <summary>The authenticated local player's PlayFabId (null until login succeeds).</summary>
    public string LocalPlayFabId => PlayFabId;

    private float searchInterval = 1f;
    private float searchTimer = 0f;

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

    private void InfoText(string text)
    {
        if (infoText != null)
            infoText.text = text;
    }

    private void Start()
    {
        SceneManager.activeSceneChanged += SceneChanged;

        InfoText(Application.version + " | " + "Not Logged In");
        Login();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        SceneManager.activeSceneChanged -= SceneChanged;
    }

    void Login()
    {
        CrossPlatformManager.EnsureExists();

        if (CrossPlatformManager.Instance.CurrentPlatform == PlatformType.Xbox)
        {
            Debug.Log("[PlayfabManager] Xbox platform detected. Initiating Xbox Live authentication...");
            CrossPlatformManager.Instance.AuthenticatePlatform((success, gamertag, xuid, token) =>
            {
                if (success && !string.IsNullOrEmpty(token) && token != "SIMULATED_XBOX_TOKEN")
                {
                    Debug.Log($"[PlayfabManager] Performing LoginWithXbox for Gamertag: {gamertag}...");
                    var request = new LoginWithXboxRequest
                    {
                        XboxToken = token,
                        CreateAccount = true,
                        InfoRequestParameters = new GetPlayerCombinedInfoRequestParams
                        {
                            GetPlayerProfile = true,
                            GetUserData = true,
                        },
                    };
                    PlayFabClientAPI.LoginWithXbox(request, res => OnLoginSuccess(res, gamertag), OnError);
                }
                else
                {
                    Debug.Log($"[PlayfabManager] Using device fallback auth (simulated/offline Xbox token) for Gamertag: {gamertag}...");
                    LoginWithCustomId(gamertag);
                }
            });
        }
        else
        {
            LoginWithCustomId(null);
        }
    }

    void LoginWithCustomId(string platformGamertag)
    {
        var request = new LoginWithCustomIDRequest
        {
            CustomId = SystemInfo.deviceUniqueIdentifier,
            CreateAccount = true,
            InfoRequestParameters = new GetPlayerCombinedInfoRequestParams
            {
                GetPlayerProfile = true,
                GetUserData = true,
            },
        };
        PlayFabClientAPI.LoginWithCustomID(request, res => OnLoginSuccess(res, platformGamertag), OnError);
    }


    void SceneChanged(Scene prev, Scene next)
    {
        moneyText = null;
    }

    private void Update()
    {
        if (moneyText == null)
        {
            searchTimer += Time.deltaTime;
            if (searchTimer >= searchInterval)
            {
                searchTimer = 0f;
                TryFindMoneyText();
            }
        }
    }

    void TryFindMoneyText()
    {
        // Prefer the reference from PlayerUIHandler to avoid Canvas name ambiguity
        // when multiple Canvas objects exist (e.g. per-player HUDs in multiplayer).
        PlayerUIHandler uiHandler = FindFirstObjectByType<PlayerUIHandler>();
        if (uiHandler != null && uiHandler.moneyText != null)
        {
            moneyText = uiHandler.moneyText;
            Debug.Log("[PlayfabManager] Money text found via PlayerUIHandler.");
            RefreshCubeCoins();
            return;
        }

        // Fallback: traverse the hierarchy by name.
        GameObject canvas = GameObject.Find("Canvas");
        if (canvas == null)
            return;

        Transform stats = canvas.transform.Find("Stats");
        if (stats == null)
            return;

        Transform money = stats.transform.Find("Money");
        if (money == null)
            return;

        Transform text = money.transform.Find("MaliceValue");
        if (text == null)
            return;

        moneyText = text.GetComponent<TMP_Text>();
        if (moneyText != null)
        {
            Debug.Log("[PlayfabManager] Money text found via Canvas hierarchy.");
            RefreshCubeCoins();
        }
    }

    private void OnError(PlayFabError error)
    {
        Debug.LogError($"[PlayFab] Error {error.Error}: {error.GenerateErrorReport()}");
    }

    private void OnLoginSuccess(LoginResult result)
    {
        OnLoginSuccess(result, null);
    }

    private void OnLoginSuccess(LoginResult result, string platformGamertag)
    {
        Debug.Log("Logged in! additional info: " + result.ToJson());

        if (!string.IsNullOrEmpty(result.PlayFabId))
            PlayFabId = result.PlayFabId;

        bool isXbox = CrossPlatformManager.Instance != null && CrossPlatformManager.Instance.CurrentPlatform == PlatformType.Xbox;

        if (isXbox || !string.IsNullOrEmpty(platformGamertag))
        {
            // XR-045: On Xbox, the Gamertag MUST be the user's display name and nameWindow must NEVER be shown.
            string targetName = !string.IsNullOrEmpty(platformGamertag) ? platformGamertag :
                                (CrossPlatformManager.Instance != null && !string.IsNullOrEmpty(CrossPlatformManager.Instance.LocalGamertag) ? CrossPlatformManager.Instance.LocalGamertag : "XboxPlayer");

            displayName = targetName;
            InfoText(Application.version + " | " + displayName + " | " + "Logged In");

            if (nameWindow != null)
                nameWindow.SetActive(false);

            var profile = result.InfoResultPayload?.PlayerProfile;
            if (profile == null || profile.DisplayName != targetName)
            {
                var updateReq = new UpdateUserTitleDisplayNameRequest { DisplayName = targetName };
                PlayFabClientAPI.UpdateUserTitleDisplayName(updateReq,
                    updateRes => Debug.Log($"[PlayfabManager] Synced PlayFab display name to Xbox Gamertag: {updateRes.DisplayName}"),
                    updateErr => Debug.LogWarning($"[PlayfabManager] Could not update PlayFab display name to Gamertag: {updateErr.ErrorMessage}")
                );
            }
        }
        else
        {
            var profile = result.InfoResultPayload?.PlayerProfile;
            if (profile != null && !string.IsNullOrEmpty(profile.DisplayName))
            {
                displayName = profile.DisplayName;
                InfoText(Application.version + " | " + displayName + " | " + "Logged In");
            }
            else
            {
                InfoText(Application.version + " | " + "Not Set!" + " | " + "Logged In");
                if (nameWindow != null)
                    nameWindow.SetActive(true);
            }
        }

        // Cache developer badge verification status from user data
        if (result.InfoResultPayload?.UserData != null &&
            result.InfoResultPayload.UserData.TryGetValue("VerificationStatus", out var vEntry))
        {
            SetVerificationStatus(vEntry.Value);
        }
        else
        {
            CheckDeveloperStatus();
        }

        GetColorFromData();
        PhotonAuth(result.PlayFabId);

        IsLoggedIn = true;
        OnLoginSuccessEvent?.Invoke();

        // If UI already exists, pull CC immediately.
        // If not, TryFindMoneyText() will eventually find it and call RefreshCubeCoins().
        RefreshCubeCoins();
    }

    public void PhotonAuth(string playFabId)
    {
        var request = new GetPhotonAuthenticationTokenRequest
        {
            PhotonApplicationId = PhotonNetwork.PhotonServerSettings.AppSettings.AppIdRealtime,
        };

        PlayFabClientAPI.GetPhotonAuthenticationToken(
            request,
            result => OnPhotonAuthSuccess(result, playFabId),
            error => Debug.LogError(error.GenerateErrorReport())
        );
    }

    private void OnPhotonAuthSuccess(GetPhotonAuthenticationTokenResult result, string playFabId)
    {
        Debug.Log("Got Photon Auth Token");

        var auth = new AuthenticationValues(playFabId)
        {
            AuthType = CustomAuthenticationType.Custom
        };
        auth.AddAuthParameter("username", playFabId);
        auth.AddAuthParameter("token", result.PhotonCustomAuthenticationToken);

        PhotonNetwork.AuthValues = auth;
        PhotonNetwork.NickName = playFabId;

        PhotonNetwork.ConnectUsingSettings();
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log("Photon connected + authenticated to Master.");
    }

    public override void OnCustomAuthenticationFailed(string debugMessage)
    {
        Debug.LogError("Custom auth failed: " + debugMessage);
    }

    public void SubmitName()
    {
        var request = new UpdateUserTitleDisplayNameRequest { DisplayName = nameField.text };
        PlayFabClientAPI.UpdateUserTitleDisplayName(request, OnDisplayNameUpdate, OnError);
    }

    private void OnDisplayNameUpdate(UpdateUserTitleDisplayNameResult result)
    {
        Debug.Log("Updated name to: " + result.DisplayName);
        displayName = result.DisplayName;
        InfoText(Application.version + " | " + displayName + " | " + "Logged In");
        if (nameWindow != null)
            nameWindow.SetActive(false);
    }

    // =============================
    // CUBE COINS (CC) API
    // =============================

    /// <summary>
    /// Re-reads CC from PlayFab and updates UI + cache.
    /// </summary>
    public void RefreshCubeCoins()
    {
        // GetUserInventory returns VirtualCurrency dict.
        PlayFabClientAPI.GetUserInventory(
            new GetUserInventoryRequest(),
            result =>
            {
                int cc = 0;
                if (
                    result.VirtualCurrency != null
                    && result.VirtualCurrency.TryGetValue(cubeCoinsCode, out var v)
                )
                    cc = v;

                SetCachedCC(cc);
            },
            OnError
        );
    }

    private void SetCachedCC(int cc)
    {
        CachedCC = cc;

        if (moneyText != null)
            moneyText.text = cc.ToString();

        OnCubeCoinsChanged?.Invoke(cc);
    }

    private struct PendingCCRequest
    {
        public int amount;
        public string reason;
    }

    private readonly Queue<PendingCCRequest> _pendingCCRequests = new Queue<PendingCCRequest>();
    private bool _isAddingCC = false;

    /// <summary>
    /// Adds CC. Use for rewards, end-of-round payouts, etc.
    /// Safely queues and batches requests to prevent PlayFab concurrent edit conflicts.
    /// </summary>
    public void AddCubeCoins(int amount, string reason = "")
    {
        if (amount <= 0)
        {
            Debug.LogWarning("AddCubeCoins called with non-positive amount.");
            return;
        }

        _pendingCCRequests.Enqueue(new PendingCCRequest { amount = amount, reason = reason });
        ProcessNextCCBatch();
    }

    private void ProcessNextCCBatch()
    {
        if (_isAddingCC || _pendingCCRequests.Count == 0)
            return;

        _isAddingCC = true;

        int totalAmount = 0;
        var reasons = new List<string>();
        while (_pendingCCRequests.Count > 0)
        {
            var req = _pendingCCRequests.Dequeue();
            totalAmount += req.amount;
            if (!string.IsNullOrEmpty(req.reason))
                reasons.Add(req.reason);
        }

        string combinedReason = string.Join(", ", reasons);

        var request = new AddUserVirtualCurrencyRequest
        {
            VirtualCurrency = cubeCoinsCode,
            Amount = totalAmount,
        };

        PlayFabClientAPI.AddUserVirtualCurrency(
            request,
            result =>
            {
                _isAddingCC = false;
                SetCachedCC(result.Balance);
                Debug.Log(
                    $"[PlayFab] +{totalAmount} {cubeCoinsCode} (reason: {combinedReason}) -> balance {result.Balance}"
                );
                ProcessNextCCBatch();
            },
            error =>
            {
                _isAddingCC = false;
                Debug.LogError(
                    $"[PlayFab] AddCubeCoins FAILED (reason: {combinedReason}): {error.GenerateErrorReport()}"
                );
                ProcessNextCCBatch();
            }
        );
    }

    /// <summary>
    /// Tries to spend CC. Calls back with success and the balance (new or unchanged).
    /// </summary>
    public void TrySpendCubeCoins(int cost, string reason, Action<bool, int> onResult)
    {
        if (cost <= 0)
        {
            onResult?.Invoke(true, CachedCC);
            return;
        }

        // Optional fast-fail (UI responsiveness). Real authority is PlayFab�s response.
        if (CachedCC < cost)
        {
            onResult?.Invoke(false, CachedCC);
            return;
        }

        var request = new SubtractUserVirtualCurrencyRequest
        {
            VirtualCurrency = cubeCoinsCode,
            Amount = cost,
        };

        PlayFabClientAPI.SubtractUserVirtualCurrency(
            request,
            result =>
            {
                SetCachedCC(result.Balance);
                Debug.Log(
                    $"-{cost} {cubeCoinsCode} (reason: {reason}) -> balance {result.Balance}"
                );
                onResult?.Invoke(true, result.Balance);
            },
            error =>
            {
                // If PlayFab rejects (insufficient funds, etc.) we keep cache as-is and notify.
                Debug.LogWarning("Spend failed: " + error.GenerateErrorReport());
                onResult?.Invoke(false, CachedCC);
            }
        );
    }

    // =============================
    // Your existing color + misc
    // =============================

    void GetColorFromData()
    {
        PlayFabClientAPI.GetUserData(
            new GetUserDataRequest() { PlayFabId = this.PlayFabId },
            OnSuccessGetData,
            OnError
        );
    }

    public void OnSuccessGetData(GetUserDataResult result)
    {
        foreach (var item in result.Data)
        {
            if (item.Value.Value.Contains("#"))
            {
                string colorTrim = item.Value.Value.Trim('#');
                Color color = FromHex(colorTrim);
                picker.CurrentColor = color;
            }
        }
    }

    public static void IsItemOwned(string itemID, Action<bool> callback)
    {
        PlayFabClientAPI.GetUserInventory(
            new GetUserInventoryRequest(),
            result =>
            {
                bool owned = false;
                foreach (var item in result.Inventory)
                {
                    if (item.ItemId == itemID)
                    {
                        owned = true;
                        break;
                    }
                }
                callback?.Invoke(owned);
            },
            error =>
            {
                Debug.LogError("Got error!");
                Debug.LogError(error.GenerateErrorReport());
                callback?.Invoke(false);
            }
        );
    }

    public void SetColorToPicker()
    {
        Color color = picker.CurrentColor;
        string hex = ColorUtility.ToHtmlStringRGBA(color);

        PlayFabClientAPI.UpdateUserData(
            new UpdateUserDataRequest()
            {
                Data = new System.Collections.Generic.Dictionary<string, string>()
                {
                    { "PlayerColor", "#" + hex },
                },
            },
            OnSetColorData,
            OnError
        );
    }

    public void OnSetColorData(UpdateUserDataResult result)
    {
        Debug.Log("Set Color: " + result.ToString());
    }

    public static string GetPlayerName() => displayName;

    /// <summary>
    /// Returns the active platform type (Xbox, Steam, PC, etc.) detected for this client.
    /// </summary>
    public static PlatformType GetActivePlatform()
    {
        return CrossPlatformManager.Instance != null ? CrossPlatformManager.Instance.CurrentPlatform : PlatformType.PC;
    }

    /// <summary>
    /// Returns true if this client is running in an Xbox GDK/GameCore environment or simulated Xbox mode.
    /// </summary>
    public static bool IsXboxPlatform()
    {
        return GetActivePlatform() == PlatformType.Xbox;
    }

    /// <summary>
    /// Returns the local player's 64-bit Xbox User ID (XUID) if authenticated on Xbox Live, or 0.
    /// </summary>
    public static ulong GetLocalXuid()
    {
        return CrossPlatformManager.Instance != null ? CrossPlatformManager.Instance.LocalXuid : 0;
    }


    /// <summary>
    /// Gets a single UserData value by key.
    /// Calls back with null if missing.
    /// </summary>
    public void GetUserDataValue(string key, Action<string> onResult)
    {
        PlayFabClientAPI.GetUserData(
            new GetUserDataRequest
            {
                PlayFabId = PlayFabId,
                Keys = new System.Collections.Generic.List<string> { key },
            },
            result =>
            {
                if (result.Data != null && result.Data.TryGetValue(key, out var entry))
                {
                    onResult?.Invoke(entry.Value);
                }
                else
                {
                    onResult?.Invoke(null);
                }
            },
            error =>
            {
                Debug.LogWarning(
                    $"GetUserDataValue failed for key '{key}':\n{error.GenerateErrorReport()}"
                );
                onResult?.Invoke(null);
            }
        );
    }

    public static string VerificationStatus { get; private set; } = "";

    /// <summary>
    /// True if the authenticated player has the Developer verification badge.
    /// </summary>
    public static bool IsDeveloper
    {
        get
        {
#if UNITY_EDITOR
            if (DevPhysicsSettings.EditorForceDevBadge)
                return true;
#endif
            return string.Equals(VerificationStatus, "Developer", StringComparison.OrdinalIgnoreCase);
        }
    }

    public static event Action<bool> OnDeveloperStatusChanged;

    public static void SetVerificationStatus(string status)
    {
        VerificationStatus = status ?? "";
        bool isDev = IsDeveloper;
        OnDeveloperStatusChanged?.Invoke(isDev);
    }

    /// <summary>
    /// Refreshes the local player's VerificationStatus from PlayFab and returns whether they are a Developer.
    /// </summary>
    public void CheckDeveloperStatus(Action<bool> callback = null)
    {
        GetUserDataValue("VerificationStatus", status =>
        {
            SetVerificationStatus(status);
            callback?.Invoke(IsDeveloper);
        });
    }

    public static Color FromHex(string hex)
    {
        if (hex.Length < 6)
            throw new System.FormatException("Needs a string with a length of at least 6");

        var r = hex[..2];
        var g = hex.Substring(2, 2);
        var b = hex.Substring(4, 2);
        string alpha = (hex.Length >= 8) ? hex.Substring(6, 2) : "FF";

        return new Color(
            (int.Parse(r, NumberStyles.HexNumber) / 255f),
            (int.Parse(g, NumberStyles.HexNumber) / 255f),
            (int.Parse(b, NumberStyles.HexNumber) / 255f),
            (int.Parse(alpha, NumberStyles.HexNumber) / 255f)
        );
    }
}
