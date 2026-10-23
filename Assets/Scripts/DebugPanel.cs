using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// In-game debug panel for playtesters. Toggle with F2.
/// Allows swapping role, survivor, killer, adding coins/malice, and loading a map.
/// </summary>
public class DebugPanel : MonoBehaviour
{
    // ------------------------------------------------------------------ //
    //  Inspector
    // ------------------------------------------------------------------ //

    [Header("Toggle Key (Deprecated - replaced by CubeDebugMenu)")]
    [Tooltip("Key that shows/hides the debug panel.")]
    public Key toggleKey = Key.None;

    [Header("Currency")]
    [Tooltip("Amount of coins to grant per button press.")]
    public int coinGrantAmount = 100;

    [Tooltip("Amount of malice to grant per button press.")]
    public int maliceGrantAmount = 5;

    // ------------------------------------------------------------------ //
    //  Private state
    // ------------------------------------------------------------------ //

    private GameObject panelRoot;
    private bool isVisible;

    // Cached local player references — refreshed every time the panel opens.
    private BasePlayer localBasePlayer;
    private BasePlayerStats localStats;
    private PlayerCustomizer localCustomizer;

    // Available assets discovered at runtime.
    private List<KillerType> killers = new List<KillerType>();
    private List<SurvivorType> survivors = new List<SurvivorType>();
    private List<MapData> maps = new List<MapData>();

    // Current dropdown selection indices.
    private int selectedKillerIndex;
    private int selectedSurvivorIndex;
    private int selectedMapIndex;

    // UI widgets we need to read/write.
    private TMP_Dropdown killerDropdown;
    private TMP_Dropdown survivorDropdown;
    private TMP_Dropdown mapDropdown;
    private TMP_Text statusText;
    private TMP_InputField coinInput;
    private TMP_InputField maliceInput;

    // Known ScriptableObject resource folders.
    private const string KillersFolder = "ScriptableObjects/Killers";
    private const string SurvivorsFolder = "ScriptableObjects/Survivors";
    private const string MapsFolder = "ScriptableObjects/Maps";

    // ------------------------------------------------------------------ //
    //  Unity lifecycle
    // ------------------------------------------------------------------ //

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        LoadAssets();
        BuildUI();
        SetVisible(false);
    }

    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;

    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Re-cache local player references after a scene change.
        RefreshLocalPlayerRefs();
    }

    private void Update()
    {
        if (toggleKey == Key.None)
            return;

        if (Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame)
        {
            if (!isVisible)
                RefreshLocalPlayerRefs();
            SetVisible(!isVisible);
        }
    }

    // ------------------------------------------------------------------ //
    //  Asset loading
    // ------------------------------------------------------------------ //

    private void LoadAssets()
    {
        // Killers
        killers.Clear();
        var killerAssets = Resources.LoadAll<KillerType>(KillersFolder);
        killers.AddRange(killerAssets);

        // Survivors
        survivors.Clear();
        var survivorAssets = Resources.LoadAll<SurvivorType>(SurvivorsFolder);
        survivors.AddRange(survivorAssets);

        // Maps
        maps.Clear();
        var mapAssets = Resources.LoadAll<MapData>(MapsFolder);
        maps.AddRange(mapAssets);
    }

    // ------------------------------------------------------------------ //
    //  UI construction (pure UGUI, no prefabs required)
    // ------------------------------------------------------------------ //

    private void BuildUI()
    {
        // Root canvas
        var canvasGO = new GameObject("[DebugPanel]");
        canvasGO.transform.SetParent(transform);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;
        canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler
            .ScaleMode
            .ScaleWithScreenSize;
        canvasGO.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
        canvasGO.AddComponent<GraphicRaycaster>();
        panelRoot = canvasGO;

        // Background panel
        var bg = CreatePanel(
            canvasGO.transform,
            "Background",
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(380f, 640f),
            new Vector2(190f, -320f),
            new Color(0.05f, 0.05f, 0.05f, 0.95f)
        );

        float y = -20f;
        float rowH = 30f;
        float sectionGap = 14f;
        float panelW = 360f;

        // Title
        CreateLabel(
            bg.transform,
            "DebugPanel",
            "[DEBUG PANEL]",
            new Vector2(panelW, rowH),
            new Vector2(panelW * 0.5f, y),
            15,
            Color.yellow,
            TextAlignmentOptions.Center
        );
        y -= rowH + sectionGap;

        // Status text
        statusText = CreateLabel(
            bg.transform,
            "Status",
            "",
            new Vector2(panelW, rowH),
            new Vector2(panelW * 0.5f, y),
            11,
            new Color(0.7f, 1f, 0.7f),
            TextAlignmentOptions.Center
        );
        y -= rowH + sectionGap;

        // ── ROLE ──
        CreateLabel(
            bg.transform,
            "RoleHeader",
            "ROLE",
            new Vector2(panelW, rowH - 4f),
            new Vector2(panelW * 0.5f, y),
            11,
            new Color(0.9f, 0.6f, 0.1f),
            TextAlignmentOptions.Center
        );
        y -= rowH;

        float btnW = 110f;
        float btnGap = 8f;
        float totalBtnW = 3 * btnW + 2 * btnGap;
        float btnStartX = (panelW - totalBtnW) * 0.5f + btnW * 0.5f;
        CreateButton(
            bg.transform,
            "BtnSurvivor",
            "Set Survivor",
            new Vector2(btnW, rowH),
            new Vector2(btnStartX, y),
            () => SetRole(BasePlayer.PlayerType.Survivor)
        );
        CreateButton(
            bg.transform,
            "BtnKiller",
            "Set Killer",
            new Vector2(btnW, rowH),
            new Vector2(btnStartX + btnW + btnGap, y),
            () => SetRole(BasePlayer.PlayerType.Killer)
        );
        CreateButton(
            bg.transform,
            "BtnNone",
            "Clear Role",
            new Vector2(btnW, rowH),
            new Vector2(btnStartX + (btnW + btnGap) * 2, y),
            () => SetRole(BasePlayer.PlayerType.None)
        );
        y -= rowH + sectionGap;

        // ── SURVIVOR ──
        CreateLabel(
            bg.transform,
            "SurvivorHeader",
            "SURVIVOR",
            new Vector2(panelW, rowH - 4f),
            new Vector2(panelW * 0.5f, y),
            11,
            new Color(0.9f, 0.6f, 0.1f),
            TextAlignmentOptions.Center
        );
        y -= rowH;
        survivorDropdown = CreateDropdown(
            bg.transform,
            "SurvivorDropdown",
            BuildNames(survivors, s => s._Name),
            new Vector2(panelW - 120f, rowH),
            new Vector2((panelW - 120f) * 0.5f + 10f, y),
            i => selectedSurvivorIndex = i
        );
        CreateButton(
            bg.transform,
            "BtnApplySurvivor",
            "Apply",
            new Vector2(100f, rowH),
            new Vector2(panelW - 50f, y),
            ApplySurvivor
        );
        y -= rowH + sectionGap;

        // ── KILLER ──
        CreateLabel(
            bg.transform,
            "KillerHeader",
            "KILLER",
            new Vector2(panelW, rowH - 4f),
            new Vector2(panelW * 0.5f, y),
            11,
            new Color(0.9f, 0.6f, 0.1f),
            TextAlignmentOptions.Center
        );
        y -= rowH;
        killerDropdown = CreateDropdown(
            bg.transform,
            "KillerDropdown",
            BuildNames(killers, k => k._Name),
            new Vector2(panelW - 120f, rowH),
            new Vector2((panelW - 120f) * 0.5f + 10f, y),
            i => selectedKillerIndex = i
        );
        CreateButton(
            bg.transform,
            "BtnApplyKiller",
            "Apply",
            new Vector2(100f, rowH),
            new Vector2(panelW - 50f, y),
            ApplyKiller
        );
        y -= rowH + sectionGap;

        // ── COINS ──
        CreateLabel(
            bg.transform,
            "CoinsHeader",
            "COINS",
            new Vector2(panelW, rowH - 4f),
            new Vector2(panelW * 0.5f, y),
            11,
            new Color(0.9f, 0.6f, 0.1f),
            TextAlignmentOptions.Center
        );
        y -= rowH;
        coinInput = CreateInputField(
            bg.transform,
            "CoinInput",
            coinGrantAmount.ToString(),
            new Vector2(panelW - 220f, rowH),
            new Vector2((panelW - 220f) * 0.5f + 10f, y)
        );
        CreateButton(
            bg.transform,
            "BtnAddCoins",
            "Give Coins",
            new Vector2(100f, rowH),
            new Vector2(panelW * 0.5f + 50f, y),
            GiveCoins
        );
        CreateButton(
            bg.transform,
            "BtnRemoveCoins",
            "Take Coins",
            new Vector2(100f, rowH),
            new Vector2(panelW - 50f, y),
            TakeCoins
        );
        y -= rowH + sectionGap;

        // ── MALICE ──
        CreateLabel(
            bg.transform,
            "MaliceHeader",
            "MALICE",
            new Vector2(panelW, rowH - 4f),
            new Vector2(panelW * 0.5f, y),
            11,
            new Color(0.9f, 0.6f, 0.1f),
            TextAlignmentOptions.Center
        );
        y -= rowH;
        maliceInput = CreateInputField(
            bg.transform,
            "MaliceInput",
            maliceGrantAmount.ToString(),
            new Vector2(panelW - 220f, rowH),
            new Vector2((panelW - 220f) * 0.5f + 10f, y)
        );
        CreateButton(
            bg.transform,
            "BtnAddMalice",
            "Give Malice",
            new Vector2(100f, rowH),
            new Vector2(panelW * 0.5f + 50f, y),
            GiveMalice
        );
        CreateButton(
            bg.transform,
            "BtnRemoveMalice",
            "Take Malice",
            new Vector2(100f, rowH),
            new Vector2(panelW - 50f, y),
            TakeMalice
        );
        y -= rowH + sectionGap;

        // ── MAP ──
        CreateLabel(
            bg.transform,
            "MapHeader",
            "MAP",
            new Vector2(panelW, rowH - 4f),
            new Vector2(panelW * 0.5f, y),
            11,
            new Color(0.9f, 0.6f, 0.1f),
            TextAlignmentOptions.Center
        );
        y -= rowH;
        mapDropdown = CreateDropdown(
            bg.transform,
            "MapDropdown",
            BuildNames(maps, m => m.mapName),
            new Vector2(panelW - 120f, rowH),
            new Vector2((panelW - 120f) * 0.5f + 10f, y),
            i => selectedMapIndex = i
        );
        CreateButton(
            bg.transform,
            "BtnLoadMap",
            "Load Map",
            new Vector2(100f, rowH),
            new Vector2(panelW - 50f, y),
            LoadMap
        );
        y -= rowH + sectionGap;

        // ── DEV PHYSICS (Developer Exclusive) ──
        if (PlayfabManager.IsDeveloper)
        {
            CreateLabel(
                bg.transform,
                "DevPhysicsHeader",
                "★ DEV SECONDARY PHYSICS ★",
                new Vector2(panelW, rowH - 4f),
                new Vector2(panelW * 0.5f, y),
                11,
                new Color(1f, 0.75f, 0.1f),
                TextAlignmentOptions.Center
            );
            y -= rowH;

            CreateButton(
                bg.transform,
                "BtnToggleDevPhysics",
                DevPhysicsSettings.IsDevPhysicsEnabled ? "Physics: [ENABLED]" : "Physics: [DISABLED]",
                new Vector2(panelW - 20f, rowH),
                new Vector2(panelW * 0.5f, y),
                () =>
                {
                    DevPhysicsSettings.IsDevPhysicsEnabled = !DevPhysicsSettings.IsDevPhysicsEnabled;
                    SetStatus($"Dev Physics {(DevPhysicsSettings.IsDevPhysicsEnabled ? "Enabled" : "Disabled")}");
                    var btnText = bg.transform.Find("BtnToggleDevPhysics/Text")?.GetComponent<TMP_Text>();
                    if (btnText != null)
                        btnText.text = DevPhysicsSettings.IsDevPhysicsEnabled ? "Physics: [ENABLED]" : "Physics: [DISABLED]";
                }
            );
            y -= rowH;

            CreateButton(
                bg.transform,
                "BtnResetDevPhysics",
                "Reset Physics Defaults",
                new Vector2(panelW - 20f, rowH),
                new Vector2(panelW * 0.5f, y),
                () =>
                {
                    DevPhysicsSettings.ResetToDefaults();
                    SetStatus("Dev Physics reset to defaults");
                }
            );
            y -= rowH + sectionGap;
        }

        // ── Close ──
        CreateButton(
            bg.transform,
            "BtnClose",
            "Close  [F2]",
            new Vector2(panelW - 20f, rowH),
            new Vector2(panelW * 0.5f, y),
            () => SetVisible(false)
        );

        // Adjust panel height to fit content
        var bgRect = bg.GetComponent<RectTransform>();
        float totalH = Mathf.Abs(y) + rowH + 20f;
        bgRect.sizeDelta = new Vector2(380f, totalH);
        bgRect.anchoredPosition = new Vector2(190f, -(totalH * 0.5f));
    }

    // ------------------------------------------------------------------ //
    //  Debug actions
    // ------------------------------------------------------------------ //

    private void SetRole(BasePlayer.PlayerType type)
    {
        if (!EnsurePlayer())
            return;
        localBasePlayer.photonView.RPC(
            nameof(BasePlayer.SetPlayerType),
            RpcTarget.AllBuffered,
            type
        );
        SetStatus($"Role set to <b>{type}</b>");
    }

    private void ApplySurvivor()
    {
        if (!EnsurePlayer())
            return;
        if (survivors.Count == 0)
        {
            SetStatus("No survivors loaded.");
            return;
        }

        SurvivorType survivor = survivors[selectedSurvivorIndex];
        localBasePlayer.EquipSurvivor(survivor);

        // Sync the skin via PlayerCustomizer RPC so all clients see the change.
        PlayerCustomizer customizer = GetCustomizer();
        if (customizer != null)
            customizer.photonView.RPC(
                nameof(PlayerCustomizer.ApplyRoleAndCharacter),
                RpcTarget.AllBuffered,
                BasePlayer.PlayerType.Survivor,
                survivor._Name
            );

        SetStatus($"Survivor set to <b>{survivor._Name}</b>");
    }

    private void ApplyKiller()
    {
        if (!EnsurePlayer())
            return;
        if (killers.Count == 0)
        {
            SetStatus("No killers loaded.");
            return;
        }

        KillerType killer = killers[selectedKillerIndex];
        localBasePlayer.EquipKiller(killer);

        // Sync the skin via PlayerCustomizer RPC.
        PlayerCustomizer customizer = GetCustomizer();
        if (customizer != null)
            customizer.photonView.RPC(
                nameof(PlayerCustomizer.ApplyRoleAndCharacter),
                RpcTarget.AllBuffered,
                BasePlayer.PlayerType.Killer,
                killer._Name
            );

        SetStatus($"Killer set to <b>{killer._Name}</b>");
    }

    private void GiveCoins()
    {
        if (!EnsureStats())
            return;
        int amount = ParseInput(coinInput, coinGrantAmount);
        localStats.photonView.RPC(nameof(BasePlayerStats.GrantMoney), RpcTarget.All, amount);
        SetStatus($"Granted <b>{amount}</b> coins. Total: {localStats.money}");
    }

    private void TakeCoins()
    {
        if (!EnsureStats())
            return;
        int amount = ParseInput(coinInput, coinGrantAmount);
        localStats.photonView.RPC(nameof(BasePlayerStats.RemoveMoney), RpcTarget.All, amount);
        SetStatus($"Removed <b>{amount}</b> coins. Total: {localStats.money}");
    }

    private void GiveMalice()
    {
        if (!EnsureStats())
            return;
        int amount = ParseInput(maliceInput, maliceGrantAmount);
        localStats.photonView.RPC(nameof(BasePlayerStats.GrantMalice), RpcTarget.All, amount);
        SetStatus($"Granted <b>{amount}</b> malice. Total: {localStats.malice}");
    }

    private void TakeMalice()
    {
        if (!EnsureStats())
            return;
        int amount = ParseInput(maliceInput, maliceGrantAmount);
        localStats.photonView.RPC(nameof(BasePlayerStats.RemoveMalice), RpcTarget.All, amount);
        SetStatus($"Removed <b>{amount}</b> malice. Total: {localStats.malice}");
    }

    private void LoadMap()
    {
        if (maps.Count == 0)
        {
            SetStatus("No maps loaded.");
            return;
        }
        if (!PhotonNetwork.IsMasterClient)
        {
            SetStatus("Only the Master Client can load maps.");
            return;
        }

        MapData map = maps[selectedMapIndex];
        if (string.IsNullOrEmpty(map.mapName))
        {
            SetStatus("Selected map has no scene name.");
            return;
        }

        SetStatus($"Loading map: <b>{map.mapName}</b>…");
        PhotonNetwork.LoadLevel(map.mapName);
    }

    // ------------------------------------------------------------------ //
    //  Helpers
    // ------------------------------------------------------------------ //

    private void RefreshLocalPlayerRefs()
    {
        localBasePlayer = null;
        localStats = null;
        localCustomizer = null;

        foreach (BasePlayer bp in FindObjectsByType<BasePlayer>(FindObjectsSortMode.None))
        {
            if (bp.photonView != null && bp.photonView.IsMine)
            {
                localBasePlayer = bp;
                localStats = bp.GetComponent<BasePlayerStats>();
                localCustomizer = bp.GetComponent<PlayerCustomizer>();
                break;
            }
        }
    }

    private PlayerCustomizer GetCustomizer()
    {
        if (localCustomizer != null)
            return localCustomizer;
        if (localBasePlayer != null)
            localCustomizer = localBasePlayer.GetComponent<PlayerCustomizer>();
        return localCustomizer;
    }

    private bool EnsurePlayer()
    {
        if (localBasePlayer != null)
            return true;
        RefreshLocalPlayerRefs();
        if (localBasePlayer == null)
        {
            SetStatus("Local player not found.");
            return false;
        }
        return true;
    }

    private bool EnsureStats()
    {
        if (!EnsurePlayer())
            return false;
        if (localStats != null)
            return true;
        SetStatus("BasePlayerStats not found on local player.");
        return false;
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
        Debug.Log($"[DebugPanel] {message}");
    }

    private static int ParseInput(TMP_InputField field, int fallback)
    {
        if (field != null && int.TryParse(field.text, out int parsed) && parsed > 0)
            return parsed;
        return fallback;
    }

    private static List<string> BuildNames<T>(List<T> items, System.Func<T, string> nameSelector)
    {
        var names = new List<string>();
        foreach (T item in items)
            names.Add(nameSelector(item));
        if (names.Count == 0)
            names.Add("(none)");
        return names;
    }

    private void SetVisible(bool visible)
    {
        isVisible = visible;
        if (panelRoot != null)
            panelRoot.SetActive(visible);
    }

    // ------------------------------------------------------------------ //
    //  UGUI factory methods
    // ------------------------------------------------------------------ //

    private static GameObject CreatePanel(
        Transform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 sizeDelta,
        Vector2 anchoredPos,
        Color bgColor
    )
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.sizeDelta = sizeDelta;
        rt.anchoredPosition = anchoredPos;
        var img = go.AddComponent<Image>();
        img.color = bgColor;
        return go;
    }

    private static TMP_Text CreateLabel(
        Transform parent,
        string name,
        string text,
        Vector2 size,
        Vector2 pos,
        float fontSize,
        Color color,
        TextAlignmentOptions alignment
    )
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = alignment;
        return tmp;
    }

    private static void CreateButton(
        Transform parent,
        string name,
        string label,
        Vector2 size,
        Vector2 pos,
        UnityEngine.Events.UnityAction onClick
    )
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;

        var img = go.AddComponent<Image>();
        img.color = new Color(0.2f, 0.2f, 0.2f, 1f);

        var btn = go.AddComponent<Button>();
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.35f, 0.35f, 0.35f);
        colors.pressedColor = new Color(0.1f, 0.1f, 0.1f);
        btn.colors = colors;
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        // Label
        var textGO = new GameObject("Label");
        textGO.transform.SetParent(go.transform, false);
        var textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.sizeDelta = Vector2.zero;
        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 11f;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
    }

    private static TMP_Dropdown CreateDropdown(
        Transform parent,
        string name,
        List<string> options,
        Vector2 size,
        Vector2 pos,
        System.Action<int> onValueChanged
    )
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;

        var img = go.AddComponent<Image>();
        img.color = new Color(0.18f, 0.18f, 0.18f, 1f);

        var dropdown = go.AddComponent<TMP_Dropdown>();
        dropdown.targetGraphic = img;

        // Caption text
        var captionGO = new GameObject("Label");
        captionGO.transform.SetParent(go.transform, false);
        var captionRT = captionGO.AddComponent<RectTransform>();
        captionRT.anchorMin = new Vector2(0.05f, 0f);
        captionRT.anchorMax = new Vector2(0.9f, 1f);
        captionRT.sizeDelta = Vector2.zero;
        var captionTMP = captionGO.AddComponent<TextMeshProUGUI>();
        captionTMP.fontSize = 11f;
        captionTMP.color = Color.white;
        captionTMP.alignment = TextAlignmentOptions.Left;
        dropdown.captionText = captionTMP;

        // Template (minimal — just enough for TMP_Dropdown to function)
        var templateGO = new GameObject("Template");
        templateGO.transform.SetParent(go.transform, false);
        var templateRT = templateGO.AddComponent<RectTransform>();
        templateRT.anchorMin = new Vector2(0f, 0f);
        templateRT.anchorMax = new Vector2(1f, 0f);
        templateRT.pivot = new Vector2(0.5f, 1f);
        templateRT.sizeDelta = new Vector2(0f, 150f);
        templateRT.anchoredPosition = Vector2.zero;
        var templateImg = templateGO.AddComponent<Image>();
        templateImg.color = new Color(0.15f, 0.15f, 0.15f, 1f);
        var scrollRect = templateGO.AddComponent<ScrollRect>();

        var viewportGO = new GameObject("Viewport");
        viewportGO.transform.SetParent(templateGO.transform, false);
        var viewportRT = viewportGO.AddComponent<RectTransform>();
        viewportRT.anchorMin = Vector2.zero;
        viewportRT.anchorMax = Vector2.one;
        viewportRT.sizeDelta = Vector2.zero;
        viewportGO.AddComponent<Image>().color = Color.clear;
        var mask = viewportGO.AddComponent<Mask>();
        mask.showMaskGraphic = false;
        scrollRect.viewport = viewportRT;

        var contentGO = new GameObject("Content");
        contentGO.transform.SetParent(viewportGO.transform, false);
        var contentRT = contentGO.AddComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0f, 1f);
        contentRT.anchorMax = Vector2.one;
        contentRT.pivot = new Vector2(0.5f, 1f);
        contentRT.sizeDelta = new Vector2(0f, 28f);
        scrollRect.content = contentRT;

        var itemGO = new GameObject("Item");
        itemGO.transform.SetParent(contentGO.transform, false);
        var itemRT = itemGO.AddComponent<RectTransform>();
        itemRT.anchorMin = new Vector2(0f, 0.5f);
        itemRT.anchorMax = new Vector2(1f, 0.5f);
        itemRT.sizeDelta = new Vector2(0f, 26f);
        var itemToggle = itemGO.AddComponent<Toggle>();
        var itemBg = itemGO.AddComponent<Image>();
        itemBg.color = new Color(0.18f, 0.18f, 0.18f, 1f);
        itemToggle.targetGraphic = itemBg;
        var itemCheckmark = new GameObject("Item Checkmark");
        itemCheckmark.transform.SetParent(itemGO.transform, false);
        itemCheckmark.AddComponent<RectTransform>().sizeDelta = new Vector2(20f, 20f);
        itemCheckmark.AddComponent<Image>().color = new Color(1f, 0.8f, 0f, 1f);
        itemToggle.graphic = itemCheckmark.GetComponent<Image>();

        var itemLabelGO = new GameObject("Item Label");
        itemLabelGO.transform.SetParent(itemGO.transform, false);
        var itemLabelRT = itemLabelGO.AddComponent<RectTransform>();
        itemLabelRT.anchorMin = new Vector2(0.1f, 0f);
        itemLabelRT.anchorMax = Vector2.one;
        itemLabelRT.sizeDelta = Vector2.zero;
        var itemLabelTMP = itemLabelGO.AddComponent<TextMeshProUGUI>();
        itemLabelTMP.fontSize = 11f;
        itemLabelTMP.color = Color.white;
        itemLabelTMP.alignment = TextAlignmentOptions.Left;
        itemToggle.isOn = false;

        dropdown.itemText = itemLabelTMP;
        dropdown.template = templateRT;
        templateGO.SetActive(false);

        // Populate
        dropdown.ClearOptions();
        dropdown.AddOptions(options);
        dropdown.value = 0;
        dropdown.onValueChanged.AddListener(v => onValueChanged?.Invoke(v));

        return dropdown;
    }

    private static TMP_InputField CreateInputField(
        Transform parent,
        string name,
        string defaultText,
        Vector2 size,
        Vector2 pos
    )
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;

        var img = go.AddComponent<Image>();
        img.color = new Color(0.12f, 0.12f, 0.12f, 1f);

        var field = go.AddComponent<TMP_InputField>();
        field.targetGraphic = img;
        field.contentType = TMP_InputField.ContentType.IntegerNumber;

        var textGO = new GameObject("Text");
        textGO.transform.SetParent(go.transform, false);
        var textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = new Vector2(0.05f, 0f);
        textRT.anchorMax = Vector2.one;
        textRT.sizeDelta = Vector2.zero;
        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 11f;
        tmp.color = Color.white;

        field.textComponent = tmp;
        field.text = defaultText;

        return field;
    }
}
