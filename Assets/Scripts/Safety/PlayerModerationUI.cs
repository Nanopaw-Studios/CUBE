using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using PhotonPlayer = Photon.Realtime.Player;

/// <summary>
/// Accessible, in-game UI for player muting and reporting.
///
/// Designed to satisfy Xbox certification requirements (XR-015: Managing Player
/// Communication, XR-018: User Generated Content) and integrate cleanly with CUBE's
/// existing PauseMenu and visual style.
///
/// Features:
///  - Lists all players currently in the multiplayer session.
///  - Visual indicator for the local player (self-muting & self-reporting prevented).
///  - Per-player Mute/Unmute toggle, applied instantly to voice without affecting other clients.
///  - Per-player Report flow with 7 configurable categories, optional character-capped
///    details input, live character counter, confirmation, in-flight spinners, error/retry states,
///    and duplicate prevention.
///  - Subscribes to Photon callbacks to handle players joining and leaving dynamically.
///  - Controller friendly via Unity UI Navigation.
/// </summary>
public class PlayerModerationUI : MonoBehaviourPunCallbacks
{
    private static PlayerModerationUI activeInstance;

    [Header("UI Views")]
    [SerializeField] private RectTransform dialogRT;
    [SerializeField] private GameObject listViewRoot;
    [SerializeField] private GameObject reportViewRoot;

    [Header("List View Elements")]
    [SerializeField] private Transform playerListContent;
    [SerializeField] private TextMeshProUGUI emptyNoticeText;
    [SerializeField] private Button closeButton;

    [Header("Report View Elements")]
    [SerializeField] private TextMeshProUGUI reportTargetTitleText;
    [SerializeField] private TextMeshProUGUI reportStatusFeedbackText;
    [SerializeField] private TextMeshProUGUI charCounterText;
    [SerializeField] private TMP_InputField detailsInputField;
    [SerializeField] private Button submitReportBtn;
    [SerializeField] private Button cancelReportBtn;
    [SerializeField] private Transform categoryButtonsContainer;

    // Report View State & Controls
    private PhotonPlayer currentReporteePlayer;
    private string currentReporteeId;
    private string currentReporteeName;
    private string selectedCategoryId;
    private TextMeshProUGUI submitBtnText;
    private readonly List<Button> categoryButtons = new List<Button>();

    // Cached Font
    private TMP_FontAsset uiFont;

    // Styling constants
    private static readonly Color DarkBackdrop = new Color(0f, 0f, 0f, 0.82f);
    private static readonly Color DialogCardBg = new Color(0.12f, 0.12f, 0.16f, 0.98f);
    private static readonly Color RowBg = new Color(0.17f, 0.18f, 0.23f, 0.95f);
    private static readonly Color AccentGold = new Color(1f, 0.78f, 0.15f, 1f);
    private static readonly Color AccentBlue = new Color(0.22f, 0.62f, 0.92f, 1f);
    private static readonly Color RedMuted = new Color(0.78f, 0.24f, 0.24f, 1f);
    private static readonly Color SlateUnmuted = new Color(0.26f, 0.28f, 0.35f, 1f);
    private static readonly Color OrangeReport = new Color(0.85f, 0.48f, 0.18f, 1f);
    private static readonly Color GreenReported = new Color(0.24f, 0.52f, 0.32f, 1f);
    private static readonly Color GreenXbox = new Color(0.06f, 0.49f, 0.06f, 1f);

    public static PlayerModerationUI Instance => activeInstance;
    public static bool IsOpen => activeInstance != null && activeInstance.gameObject.activeInHierarchy;

    private void Awake()
    {
        activeInstance = this;

        // If authored in scene, wire inspector references
        if (closeButton != null)
            closeButton.onClick.AddListener(CloseDialog);
        if (cancelReportBtn != null)
            cancelReportBtn.onClick.AddListener(ShowListView);
        if (submitReportBtn != null)
        {
            submitBtnText = submitReportBtn.GetComponentInChildren<TextMeshProUGUI>(true);
            submitReportBtn.onClick.AddListener(OnSubmitReportClicked);
        }
        if (detailsInputField != null)
        {
            detailsInputField.onValueChanged.AddListener(val =>
            {
                if (charCounterText != null)
                    charCounterText.text = $"{val.Length} / {PlayerReportService.MaxDetailsLength}";
            });
        }

        InitCategoryButtons();
    }

    public void InitCategoryButtons()
    {
        if (categoryButtonsContainer != null && categoryButtonsContainer.childCount > 0)
        {
            categoryButtons.Clear();
            for (int i = 0; i < categoryButtonsContainer.childCount; i++)
            {
                var btn = categoryButtonsContainer.GetChild(i).GetComponent<Button>();
                if (btn != null && i < PlayerReportCategories.All.Count)
                {
                    string catId = PlayerReportCategories.All[i].Id;
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() => SelectCategory(catId));
                    categoryButtons.Add(btn);
                }
            }
        }
    }

    /// <summary>
    /// Opens the dialog directly (for scene-authored instances).
    /// </summary>
    public void OpenDialog()
    {
        activeInstance = this;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        ShowListView();
        RefreshPlayerList();
    }

    /// <summary>
    /// Closes the dialog.
    /// </summary>
    public void CloseDialog()
    {
        gameObject.SetActive(false);
        var pm = FindFirstObjectByType<PauseMenu>();
        if (pm != null)
            pm.SetFocusToPlayersButton();
    }

    /// <summary>
    /// Opens the moderation UI, using existing scene instance or creating one under parentCanvas.
    /// </summary>
    public static PlayerModerationUI Open(Transform parentCanvas = null, GameObject buttonTemplate = null)
    {
        if (activeInstance == null)
        {
            activeInstance = FindFirstObjectByType<PlayerModerationUI>(FindObjectsInactive.Include);
        }

        if (activeInstance != null)
        {
            activeInstance.OpenDialog();
            return activeInstance;
        }

        if (parentCanvas == null)
        {
            Canvas sceneCanvas = FindFirstObjectByType<Canvas>();
            if (sceneCanvas != null) parentCanvas = sceneCanvas.transform;
        }
        if (parentCanvas == null)
        {
            Debug.LogError("[PlayerModerationUI] Cannot open: no Canvas found in scene.");
            return null;
        }

        GameObject root = new GameObject("PlayerModerationUI_Modal", typeof(RectTransform));
        root.transform.SetParent(parentCanvas, false);
        activeInstance = root.AddComponent<PlayerModerationUI>();
        activeInstance.BuildUI(buttonTemplate);

        activeInstance.OpenDialog();
        return activeInstance;
    }

    public static void Close()
    {
        if (activeInstance != null)
        {
            activeInstance.CloseDialog();
        }
    }

    public override void OnEnable()
    {
        base.OnEnable();
        PlayerMuteService.OnMuteStateChanged += HandleMuteChanged;
        RefreshPlayerList();
    }

    public override void OnDisable()
    {
        base.OnDisable();
        PlayerMuteService.OnMuteStateChanged -= HandleMuteChanged;
    }

    private void HandleMuteChanged(string _)
    {
        if (listViewRoot != null && listViewRoot.activeSelf)
            RefreshPlayerList();
    }

    public override void OnPlayerEnteredRoom(Photon.Realtime.Player newPlayer)
    {
        RefreshPlayerList();
    }

    public override void OnPlayerLeftRoom(Photon.Realtime.Player otherPlayer)
    {
        RefreshPlayerList();
        if (reportViewRoot != null && reportViewRoot.activeSelf &&
            currentReporteePlayer != null && currentReporteePlayer.ActorNumber == otherPlayer.ActorNumber)
        {
            if (reportStatusFeedbackText != null)
            {
                reportStatusFeedbackText.text = "Note: This player has left the match, but your report will still be recorded.";
                reportStatusFeedbackText.color = new Color(1f, 0.85f, 0.35f, 1f);
            }
        }
    }

    public override void OnLeftRoom()
    {
        PlayerReportService.ResetSession();
        Close();
    }

    // ==========================================
    // UI BUILDER
    // ==========================================

    private void BuildUI(GameObject buttonTemplate)
    {
        if (listViewRoot != null) return;
        ResolveFont();

        RectTransform rootRT = GetComponent<RectTransform>();
        rootRT.anchorMin = Vector2.zero;
        rootRT.anchorMax = Vector2.one;
        rootRT.offsetMin = Vector2.zero;
        rootRT.offsetMax = Vector2.zero;

        // 1. Semi-transparent backdrop that blocks input to underlying UI
        Image bgImg = gameObject.AddComponent<Image>();
        bgImg.color = DarkBackdrop;
        bgImg.raycastTarget = true;

        Button backdropBtnDismiss = gameObject.AddComponent<Button>();
        backdropBtnDismiss.transition = Selectable.Transition.None;
        backdropBtnDismiss.onClick.AddListener(Close);

        // 2. Centered dialog card
        GameObject dialogGO = new GameObject("DialogCard", typeof(RectTransform), typeof(Image));
        dialogGO.transform.SetParent(transform, false);
        dialogRT = dialogGO.GetComponent<RectTransform>();
        dialogRT.anchorMin = new Vector2(0.5f, 0.5f);
        dialogRT.anchorMax = new Vector2(0.5f, 0.5f);
        dialogRT.pivot = new Vector2(0.5f, 0.5f);
        dialogRT.sizeDelta = new Vector2(880f, 620f);

        Image dialogBg = dialogGO.GetComponent<Image>();
        dialogBg.color = DialogCardBg;
        dialogBg.raycastTarget = true;

        // Block raycasts so clicks on the dialog do not dismiss via backdrop button
        Button dialogBlocker = dialogGO.AddComponent<Button>();
        dialogBlocker.transition = Selectable.Transition.None;

        // 3. Top Title Bar
        CreateHeader(dialogGO.transform);

        // 4. View Containers
        listViewRoot = CreateChildContainer(dialogGO.transform, "PlayerListView");
        reportViewRoot = CreateChildContainer(dialogGO.transform, "ReportView");

        BuildListView(listViewRoot.transform);
        BuildReportView(reportViewRoot.transform);

        ShowListView();
    }

    private void ResolveFont()
    {
        if (uiFont != null) return;
        var inScene = FindFirstObjectByType<TextMeshProUGUI>();
        if (inScene != null) uiFont = inScene.font;
        if (uiFont == null) uiFont = TMP_Settings.defaultFontAsset;
    }

    private static GameObject CreateChildContainer(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(25f, 25f);
        rt.offsetMax = new Vector2(-25f, -75f); // Leave room for 75px header
        return go;
    }

    private void CreateHeader(Transform parent)
    {
        GameObject headerGO = new GameObject("Header", typeof(RectTransform));
        headerGO.transform.SetParent(parent, false);
        RectTransform rt = headerGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(0f, 75f);

        // Title text
        GameObject titleGO = new GameObject("TitleText", typeof(RectTransform));
        titleGO.transform.SetParent(headerGO.transform, false);
        RectTransform trt = titleGO.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(0f, 0.5f);
        trt.anchorMax = new Vector2(1f, 0.5f);
        trt.pivot = new Vector2(0f, 0.5f);
        trt.anchoredPosition = new Vector2(30f, 8f);
        trt.sizeDelta = new Vector2(-120f, 32f);

        TextMeshProUGUI titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        if (uiFont != null) titleTMP.font = uiFont;
        titleTMP.text = "PLAYERS & MODERATION";
        titleTMP.fontSize = 22f;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = AccentGold;
        titleTMP.alignment = TextAlignmentOptions.Left;
        titleTMP.raycastTarget = false;

        // Subtitle text
        GameObject subGO = new GameObject("SubtitleText", typeof(RectTransform));
        subGO.transform.SetParent(headerGO.transform, false);
        RectTransform srt = subGO.GetComponent<RectTransform>();
        srt.anchorMin = new Vector2(0f, 0.5f);
        srt.anchorMax = new Vector2(1f, 0.5f);
        srt.pivot = new Vector2(0f, 0.5f);
        srt.anchoredPosition = new Vector2(30f, -16f);
        srt.sizeDelta = new Vector2(-120f, 24f);

        TextMeshProUGUI subTMP = subGO.AddComponent<TextMeshProUGUI>();
        if (uiFont != null) subTMP.font = uiFont;
        subTMP.text = "Mute voice or submit confidential reports to the CUBE moderation team.";
        subTMP.fontSize = 13f;
        subTMP.color = new Color(0.72f, 0.74f, 0.82f, 1f);
        subTMP.alignment = TextAlignmentOptions.Left;
        subTMP.raycastTarget = false;

        // Close 'X' Button
        GameObject closeBtnGO = new GameObject("CloseBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        closeBtnGO.transform.SetParent(headerGO.transform, false);
        RectTransform crt = closeBtnGO.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(1f, 0.5f);
        crt.anchorMax = new Vector2(1f, 0.5f);
        crt.pivot = new Vector2(1f, 0.5f);
        crt.anchoredPosition = new Vector2(-25f, 0f);
        crt.sizeDelta = new Vector2(40f, 40f);

        Image closeImg = closeBtnGO.GetComponent<Image>();
        closeImg.color = new Color(0.24f, 0.24f, 0.3f, 1f);
        closeImg.raycastTarget = true;

        Button closeBtn = closeBtnGO.GetComponent<Button>();
        closeBtn.targetGraphic = closeImg;
        closeBtn.onClick.AddListener(Close);

        GameObject xTextGO = new GameObject("Text", typeof(RectTransform));
        xTextGO.transform.SetParent(closeBtnGO.transform, false);
        RectTransform xrt = xTextGO.GetComponent<RectTransform>();
        xrt.anchorMin = Vector2.zero;
        xrt.anchorMax = Vector2.one;
        xrt.sizeDelta = Vector2.zero;

        TextMeshProUGUI xTMP = xTextGO.AddComponent<TextMeshProUGUI>();
        if (uiFont != null) xTMP.font = uiFont;
        xTMP.text = "✕";
        xTMP.fontSize = 20f;
        xTMP.fontStyle = FontStyles.Bold;
        xTMP.color = Color.white;
        xTMP.alignment = TextAlignmentOptions.Center;
        xTMP.raycastTarget = false;
    }

    // ==========================================
    // 1. PLAYER LIST VIEW
    // ==========================================

    private void BuildListView(Transform parent)
    {
        // Scroll view for player rows
        GameObject scrollGO = new GameObject("ScrollView", typeof(RectTransform), typeof(ScrollRect), typeof(Image));
        scrollGO.transform.SetParent(parent, false);
        RectTransform srt = scrollGO.GetComponent<RectTransform>();
        srt.anchorMin = Vector2.zero;
        srt.anchorMax = Vector2.one;
        srt.offsetMin = new Vector2(0f, 45f); // 45px for bottom bar
        srt.offsetMax = new Vector2(0f, 0f);

        Image sImg = scrollGO.GetComponent<Image>();
        sImg.color = Color.clear;
        sImg.raycastTarget = true;

        ScrollRect sr = scrollGO.GetComponent<ScrollRect>();
        sr.horizontal = false;
        sr.vertical = true;
        sr.scrollSensitivity = 25f;

        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        viewport.transform.SetParent(scrollGO.transform, false);
        RectTransform vprt = viewport.GetComponent<RectTransform>();
        vprt.anchorMin = Vector2.zero;
        vprt.anchorMax = Vector2.one;
        vprt.offsetMin = Vector2.zero;
        vprt.offsetMax = Vector2.zero;

        GameObject content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        RectTransform crt = content.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0f, 1f);
        crt.anchorMax = new Vector2(1f, 1f);
        crt.pivot = new Vector2(0.5f, 1f);

        VerticalLayoutGroup vlg = content.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 10f;
        vlg.padding = new RectOffset(5, 5, 5, 15);
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        ContentSizeFitter csf = content.GetComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        sr.viewport = vprt;
        sr.content = crt;
        playerListContent = content.transform;

        // Empty state notice
        GameObject noticeGO = new GameObject("EmptyNotice", typeof(RectTransform));
        noticeGO.transform.SetParent(parent, false);
        RectTransform nrt = noticeGO.GetComponent<RectTransform>();
        nrt.anchorMin = new Vector2(0f, 0.5f);
        nrt.anchorMax = new Vector2(1f, 0.5f);
        nrt.anchoredPosition = Vector2.zero;
        nrt.sizeDelta = new Vector2(0f, 60f);

        emptyNoticeText = noticeGO.AddComponent<TextMeshProUGUI>();
        if (uiFont != null) emptyNoticeText.font = uiFont;
        emptyNoticeText.text = "No players found in this session.";
        emptyNoticeText.fontSize = 18f;
        emptyNoticeText.color = new Color(0.6f, 0.6f, 0.7f, 1f);
        emptyNoticeText.alignment = TextAlignmentOptions.Center;
        emptyNoticeText.gameObject.SetActive(false);

        // Bottom status / disclaimer bar
        GameObject bottomBar = new GameObject("BottomBar", typeof(RectTransform));
        bottomBar.transform.SetParent(parent, false);
        RectTransform bbRT = bottomBar.GetComponent<RectTransform>();
        bbRT.anchorMin = new Vector2(0f, 0f);
        bbRT.anchorMax = new Vector2(1f, 0f);
        bbRT.pivot = new Vector2(0.5f, 0f);
        bbRT.sizeDelta = new Vector2(0f, 36f);

        TextMeshProUGUI footerText = bottomBar.AddComponent<TextMeshProUGUI>();
        if (uiFont != null) footerText.font = uiFont;
        footerText.text = "Reports are confidential. Muting is local to your account. Please respect the CUBE Code of Conduct.";
        footerText.fontSize = 12f;
        footerText.color = new Color(0.55f, 0.58f, 0.66f, 1f);
        footerText.alignment = TextAlignmentOptions.Left;
        footerText.raycastTarget = false;
    }

    public void RefreshPlayerList()
    {
        if (playerListContent == null) return;

        // Clean out previous entries
        for (int i = playerListContent.childCount - 1; i >= 0; i--)
        {
            Destroy(playerListContent.GetChild(i).gameObject);
        }

        PhotonPlayer[] players = PhotonNetwork.PlayerList;
        if (players == null || players.Length == 0)
        {
            if (emptyNoticeText != null)
            {
                emptyNoticeText.text = "You are not currently in a multiplayer match.";
                emptyNoticeText.gameObject.SetActive(true);
            }
            return;
        }

        if (emptyNoticeText != null)
            emptyNoticeText.gameObject.SetActive(false);

        foreach (PhotonPlayer p in players)
        {
            if (p == null) continue;
            CreatePlayerRow(playerListContent, p);
        }
    }

    private void CreatePlayerRow(Transform parent, PhotonPlayer player)
    {
        bool isLocal = PlayerSafetyIdentity.IsLocalPlayer(player);
        string stableId = PlayerSafetyIdentity.GetStableId(player);
        string displayName = PlayerSafetyIdentity.GetDisplayName(player);

        GameObject rowGO = new GameObject($"PlayerRow_{player.ActorNumber}", typeof(RectTransform), typeof(Image));
        rowGO.transform.SetParent(parent, false);
        RectTransform rrt = rowGO.GetComponent<RectTransform>();
        rrt.sizeDelta = new Vector2(0f, 54f);

        Image rowImg = rowGO.GetComponent<Image>();
        rowImg.color = RowBg;
        rowImg.raycastTarget = true;

        // Player Name Text with Cross-Play Platform Badge
        string platformBadge = PlayerSafetyIdentity.GetFormattedPlatformBadge(player);
        ulong xuid = PlayerSafetyIdentity.GetXuid(player);
        bool isXbox = PlayerSafetyIdentity.IsXboxPlayer(player) || xuid != 0;

        GameObject nameGO = new GameObject("NameText", typeof(RectTransform));
        nameGO.transform.SetParent(rowGO.transform, false);
        RectTransform nrt = nameGO.GetComponent<RectTransform>();
        nrt.anchorMin = new Vector2(0f, 0.5f);
        nrt.anchorMax = new Vector2(isXbox ? 0.42f : 0.55f, 0.5f);
        nrt.pivot = new Vector2(0f, 0.5f);
        nrt.anchoredPosition = new Vector2(20f, 0f);
        nrt.sizeDelta = new Vector2(0f, 40f);

        TextMeshProUGUI nameTMP = nameGO.AddComponent<TextMeshProUGUI>();
        if (uiFont != null) nameTMP.font = uiFont;
        nameTMP.text = $"{platformBadge} {displayName}" + (isLocal ? "  <color=#3498DB><b>[YOU]</b></color>" : string.Empty);
        nameTMP.fontSize = 17f;
        nameTMP.color = Color.white;
        nameTMP.richText = true;
        nameTMP.alignment = TextAlignmentOptions.Left;
        nameTMP.overflowMode = TextOverflowModes.Ellipsis;
        nameTMP.raycastTarget = false;

        // Actions container on right
        if (isLocal)
        {
            // Self: No actions required.
            return;
        }

        // 1. Xbox Profile / Gamercard Button (XR-047)
        if (isXbox)
        {
            Button profileBtn = CreateActionButton(
                rowGO.transform,
                "ProfileBtn",
                "PROFILE 🎮",
                GreenXbox,
                new Vector2(-250f, 0f),
                new Vector2(100f, 36f)
            );

            profileBtn.onClick.AddListener(() =>
            {
                CrossPlatformManager.Instance.ShowPlatformProfile(xuid);
            });
        }

        // 2. Mute Button
        bool isMuted = PlayerMuteService.IsMuted(player);
        Button muteBtn = CreateActionButton(
            rowGO.transform,
            "MuteBtn",
            isMuted ? "MUTED 🔇" : "MUTE 🔊",
            isMuted ? RedMuted : SlateUnmuted,
            new Vector2(-135f, 0f),
            new Vector2(100f, 36f)
        );

        TextMeshProUGUI muteBtnText = muteBtn.GetComponentInChildren<TextMeshProUGUI>(true);
        Image muteBtnImg = muteBtn.GetComponent<Image>();

        muteBtn.onClick.AddListener(() =>
        {
            bool currentMute = PlayerMuteService.IsMuted(player);
            bool targetMute = !currentMute;
            var res = PlayerMuteService.SetMuted(player, targetMute);
            if (res == PlayerMuteService.MuteResult.Success)
            {
                bool nowMuted = PlayerMuteService.IsMuted(player);
                if (muteBtnImg != null) muteBtnImg.color = nowMuted ? RedMuted : SlateUnmuted;
                if (muteBtnText != null) muteBtnText.text = nowMuted ? "MUTED 🔇" : "MUTE 🔊";
            }
        });

        // 3. Report Button
        bool alreadyReported = PlayerReportService.HasReportedThisSession(stableId);
        Button reportBtn = CreateActionButton(
            rowGO.transform,
            "ReportBtn",
            alreadyReported ? "REPORTED ✓" : "REPORT ⚠️",
            alreadyReported ? GreenReported : OrangeReport,
            new Vector2(-20f, 0f),
            new Vector2(100f, 36f)
        );
        reportBtn.interactable = !alreadyReported;

        reportBtn.onClick.AddListener(() =>
        {
            OpenReportDialog(player);
        });
    }

    private Button CreateActionButton(Transform parent, string name, string label, Color bgColor, Vector2 anchoredPos, Vector2 size)
    {
        GameObject btnGO = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        btnGO.transform.SetParent(parent, false);
        RectTransform rt = btnGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 0.5f);
        rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot = new Vector2(1f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        Image img = btnGO.GetComponent<Image>();
        img.color = bgColor;
        img.raycastTarget = true;

        Button btn = btnGO.GetComponent<Button>();
        btn.targetGraphic = img;

        GameObject textGO = new GameObject("Text", typeof(RectTransform));
        textGO.transform.SetParent(btnGO.transform, false);
        RectTransform trt = textGO.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.sizeDelta = Vector2.zero;

        TextMeshProUGUI tmp = textGO.AddComponent<TextMeshProUGUI>();
        if (uiFont != null) tmp.font = uiFont;
        tmp.text = label;
        tmp.fontSize = 14f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;

        return btn;
    }

    // ==========================================
    // 2. REPORT VIEW (SUBMISSION FLOW)
    // ==========================================

    private void BuildReportView(Transform parent)
    {
        // 1. Report Target Label
        GameObject targetGO = new GameObject("ReportTarget", typeof(RectTransform));
        targetGO.transform.SetParent(parent, false);
        RectTransform trt = targetGO.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(0f, 1f);
        trt.anchorMax = new Vector2(1f, 1f);
        trt.pivot = new Vector2(0f, 1f);
        trt.anchoredPosition = new Vector2(15f, 0f);
        trt.sizeDelta = new Vector2(-30f, 32f);

        reportTargetTitleText = targetGO.AddComponent<TextMeshProUGUI>();
        if (uiFont != null) reportTargetTitleText.font = uiFont;
        reportTargetTitleText.text = "Report Player: [Name]";
        reportTargetTitleText.fontSize = 18f;
        reportTargetTitleText.fontStyle = FontStyles.Bold;
        reportTargetTitleText.color = Color.white;
        reportTargetTitleText.alignment = TextAlignmentOptions.Left;

        // 2. Category instruction
        GameObject catInstrGO = new GameObject("CategoryLabel", typeof(RectTransform));
        catInstrGO.transform.SetParent(parent, false);
        RectTransform cirt = catInstrGO.GetComponent<RectTransform>();
        cirt.anchorMin = new Vector2(0f, 1f);
        cirt.anchorMax = new Vector2(1f, 1f);
        cirt.pivot = new Vector2(0f, 1f);
        cirt.anchoredPosition = new Vector2(15f, -34f);
        cirt.sizeDelta = new Vector2(-30f, 22f);

        TextMeshProUGUI catInstr = catInstrGO.AddComponent<TextMeshProUGUI>();
        if (uiFont != null) catInstr.font = uiFont;
        catInstr.text = "Select a reason for this report:";
        catInstr.fontSize = 13f;
        catInstr.color = new Color(0.7f, 0.73f, 0.8f, 1f);

        // 3. Category Buttons Container (2 columns x 4 rows)
        GameObject catGrid = new GameObject("CategoryGrid", typeof(RectTransform), typeof(GridLayoutGroup));
        catGrid.transform.SetParent(parent, false);
        RectTransform grt = catGrid.GetComponent<RectTransform>();
        grt.anchorMin = new Vector2(0f, 1f);
        grt.anchorMax = new Vector2(1f, 1f);
        grt.pivot = new Vector2(0.5f, 1f);
        grt.anchoredPosition = new Vector2(0f, -60f);
        grt.sizeDelta = new Vector2(-30f, 160f);

        GridLayoutGroup glg = catGrid.GetComponent<GridLayoutGroup>();
        glg.cellSize = new Vector2(385f, 34f);
        glg.spacing = new Vector2(10f, 6f);
        glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = 2;

        categoryButtons.Clear();
        foreach (var cat in PlayerReportCategories.All)
        {
            var catBtn = CreateCategoryButton(catGrid.transform, cat);
            categoryButtons.Add(catBtn);
        }

        // 4. Optional Details Input
        GameObject detailsLabelGO = new GameObject("DetailsLabel", typeof(RectTransform));
        detailsLabelGO.transform.SetParent(parent, false);
        RectTransform dlrt = detailsLabelGO.GetComponent<RectTransform>();
        dlrt.anchorMin = new Vector2(0f, 1f);
        dlrt.anchorMax = new Vector2(1f, 1f);
        dlrt.pivot = new Vector2(0f, 1f);
        dlrt.anchoredPosition = new Vector2(15f, -232f);
        dlrt.sizeDelta = new Vector2(-30f, 22f);

        TextMeshProUGUI detailsLabel = detailsLabelGO.AddComponent<TextMeshProUGUI>();
        if (uiFont != null) detailsLabel.font = uiFont;
        detailsLabel.text = "Additional details (optional):";
        detailsLabel.fontSize = 13f;
        detailsLabel.color = new Color(0.7f, 0.73f, 0.8f, 1f);

        // Character counter on the right
        GameObject counterGO = new GameObject("CharCounter", typeof(RectTransform));
        counterGO.transform.SetParent(detailsLabelGO.transform, false);
        RectTransform ccrt = counterGO.GetComponent<RectTransform>();
        ccrt.anchorMin = new Vector2(1f, 0f);
        ccrt.anchorMax = new Vector2(1f, 1f);
        ccrt.pivot = new Vector2(1f, 0.5f);
        ccrt.sizeDelta = new Vector2(160f, 0f);

        charCounterText = counterGO.AddComponent<TextMeshProUGUI>();
        if (uiFont != null) charCounterText.font = uiFont;
        charCounterText.text = $"0 / {PlayerReportService.MaxDetailsLength}";
        charCounterText.fontSize = 12f;
        charCounterText.color = new Color(0.6f, 0.6f, 0.7f, 1f);
        charCounterText.alignment = TextAlignmentOptions.Right;

        // Input Field Box
        GameObject inputGO = new GameObject("DetailsInputField", typeof(RectTransform), typeof(Image));
        inputGO.transform.SetParent(parent, false);
        RectTransform inrt = inputGO.GetComponent<RectTransform>();
        inrt.anchorMin = new Vector2(0f, 1f);
        inrt.anchorMax = new Vector2(1f, 1f);
        inrt.pivot = new Vector2(0.5f, 1f);
        inrt.anchoredPosition = new Vector2(0f, -258f);
        inrt.sizeDelta = new Vector2(-30f, 75f);

        Image inBg = inputGO.GetComponent<Image>();
        inBg.color = new Color(0.18f, 0.19f, 0.25f, 1f);
        inBg.raycastTarget = true;

        detailsInputField = inputGO.AddComponent<TMP_InputField>();
        detailsInputField.targetGraphic = inBg;
        detailsInputField.lineType = TMP_InputField.LineType.MultiLineSubmit;
        detailsInputField.characterLimit = PlayerReportService.MaxDetailsLength;

        // Text element for input
        GameObject inTextGO = new GameObject("Text", typeof(RectTransform));
        inTextGO.transform.SetParent(inputGO.transform, false);
        RectTransform itrt = inTextGO.GetComponent<RectTransform>();
        itrt.anchorMin = Vector2.zero;
        itrt.anchorMax = Vector2.one;
        itrt.offsetMin = new Vector2(10f, 8f);
        itrt.offsetMax = new Vector2(-10f, -8f);

        TextMeshProUGUI inTextTMP = inTextGO.AddComponent<TextMeshProUGUI>();
        if (uiFont != null) inTextTMP.font = uiFont;
        inTextTMP.fontSize = 14f;
        inTextTMP.color = Color.white;
        inTextTMP.textWrappingMode = TextWrappingModes.Normal;

        // Placeholder element
        GameObject phGO = new GameObject("Placeholder", typeof(RectTransform));
        phGO.transform.SetParent(inputGO.transform, false);
        RectTransform phrt = phGO.GetComponent<RectTransform>();
        phrt.anchorMin = Vector2.zero;
        phrt.anchorMax = Vector2.one;
        phrt.offsetMin = new Vector2(10f, 8f);
        phrt.offsetMax = new Vector2(-10f, -8f);

        TextMeshProUGUI phTMP = phGO.AddComponent<TextMeshProUGUI>();
        if (uiFont != null) phTMP.font = uiFont;
        phTMP.text = "Describe what occurred (optional)...";
        phTMP.fontSize = 14f;
        phTMP.fontStyle = FontStyles.Italic;
        phTMP.color = new Color(0.5f, 0.52f, 0.6f, 0.85f);
        phTMP.textWrappingMode = TextWrappingModes.Normal;

        detailsInputField.textComponent = inTextTMP;
        detailsInputField.placeholder = phTMP;
        detailsInputField.onValueChanged.AddListener(val =>
        {
            if (charCounterText != null)
                charCounterText.text = $"{val.Length} / {PlayerReportService.MaxDetailsLength}";
        });

        // 5. Feedback status text
        GameObject feedbackGO = new GameObject("FeedbackText", typeof(RectTransform));
        feedbackGO.transform.SetParent(parent, false);
        RectTransform fbrt = feedbackGO.GetComponent<RectTransform>();
        fbrt.anchorMin = new Vector2(0f, 1f);
        fbrt.anchorMax = new Vector2(1f, 1f);
        fbrt.pivot = new Vector2(0.5f, 1f);
        fbrt.anchoredPosition = new Vector2(0f, -342f);
        fbrt.sizeDelta = new Vector2(-30f, 55f);

        reportStatusFeedbackText = feedbackGO.AddComponent<TextMeshProUGUI>();
        if (uiFont != null) reportStatusFeedbackText.font = uiFont;
        reportStatusFeedbackText.fontSize = 13f;
        reportStatusFeedbackText.text = string.Empty;
        reportStatusFeedbackText.textWrappingMode = TextWrappingModes.Normal;
        reportStatusFeedbackText.alignment = TextAlignmentOptions.TopLeft;

        // 6. Action buttons (Submit and Cancel)
        GameObject actionsGO = new GameObject("ReportActions", typeof(RectTransform));
        actionsGO.transform.SetParent(parent, false);
        RectTransform art = actionsGO.GetComponent<RectTransform>();
        art.anchorMin = new Vector2(0f, 0f);
        art.anchorMax = new Vector2(1f, 0f);
        art.pivot = new Vector2(0.5f, 0f);
        art.sizeDelta = new Vector2(0f, 50f);

        // Cancel / Back Button
        cancelReportBtn = CreateActionButton(
            actionsGO.transform,
            "CancelBtn",
            "Back",
            SlateUnmuted,
            new Vector2(-235f, 0f),
            new Vector2(180f, 44f)
        );
        cancelReportBtn.onClick.AddListener(ShowListView);

        // Submit Button
        submitReportBtn = CreateActionButton(
            actionsGO.transform,
            "SubmitBtn",
            "Submit Report",
            OrangeReport,
            new Vector2(-25f, 0f),
            new Vector2(195f, 44f)
        );
        submitBtnText = submitReportBtn.GetComponentInChildren<TextMeshProUGUI>(true);
        submitReportBtn.onClick.AddListener(OnSubmitReportClicked);
    }

    private Button CreateCategoryButton(Transform parent, ReportCategory cat)
    {
        GameObject btnGO = new GameObject($"CatBtn_{cat.Id}", typeof(RectTransform), typeof(Image), typeof(Button));
        btnGO.transform.SetParent(parent, false);

        Image img = btnGO.GetComponent<Image>();
        img.color = new Color(0.2f, 0.22f, 0.28f, 1f);
        img.raycastTarget = true;

        Button btn = btnGO.GetComponent<Button>();
        btn.targetGraphic = img;

        GameObject textGO = new GameObject("Text", typeof(RectTransform));
        textGO.transform.SetParent(btnGO.transform, false);
        RectTransform trt = textGO.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(12f, 0f);
        trt.offsetMax = new Vector2(-12f, 0f);

        TextMeshProUGUI tmp = textGO.AddComponent<TextMeshProUGUI>();
        if (uiFont != null) tmp.font = uiFont;
        tmp.text = cat.Label;
        tmp.fontSize = 13f;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.raycastTarget = false;

        btn.onClick.AddListener(() =>
        {
            SelectCategory(cat.Id);
        });

        return btn;
    }

    private void SelectCategory(string categoryId)
    {
        selectedCategoryId = categoryId;
        for (int i = 0; i < PlayerReportCategories.All.Count && i < categoryButtons.Count; i++)
        {
            var cat = PlayerReportCategories.All[i];
            var btn = categoryButtons[i];
            bool isSelected = (cat.Id == categoryId);

            Image img = btn.GetComponent<Image>();
            if (img != null)
                img.color = isSelected ? AccentBlue : new Color(0.2f, 0.22f, 0.28f, 1f);

            var tmp = btn.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp != null)
                tmp.fontStyle = isSelected ? FontStyles.Bold : FontStyles.Normal;
        }

        if (reportStatusFeedbackText != null)
            reportStatusFeedbackText.text = string.Empty;
    }

    public void OpenReportDialog(PhotonPlayer target)
    {
        if (target == null) return;
        currentReporteePlayer = target;
        currentReporteeId = PlayerSafetyIdentity.GetStableId(target);
        currentReporteeName = PlayerSafetyIdentity.GetDisplayName(target);

        ShowReportView();

        if (reportTargetTitleText != null)
            reportTargetTitleText.text = $"Report Player: <color=#F1C40F>{currentReporteeName}</color>";

        if (detailsInputField != null)
            detailsInputField.text = string.Empty;

        if (charCounterText != null)
            charCounterText.text = $"0 / {PlayerReportService.MaxDetailsLength}";

        if (reportStatusFeedbackText != null)
            reportStatusFeedbackText.text = string.Empty;

        if (submitBtnText != null)
            submitBtnText.text = "Submit Report";

        if (submitReportBtn != null)
        {
            submitReportBtn.interactable = true;
            submitReportBtn.gameObject.SetActive(true);
        }

        if (cancelReportBtn != null)
        {
            var cancelTmp = cancelReportBtn.GetComponentInChildren<TextMeshProUGUI>(true);
            if (cancelTmp != null) cancelTmp.text = "Cancel";
        }

        // Default to first category
        SelectCategory(PlayerReportCategories.All[0].Id);
    }

    private void OnSubmitReportClicked()
    {
        if (string.IsNullOrEmpty(selectedCategoryId))
        {
            SetFeedback("Please select a report category.", Color.yellow);
            return;
        }

        string details = detailsInputField != null ? detailsInputField.text : string.Empty;
        PlayerReport report = PlayerReportService.CreateReport(
            currentReporteeId,
            currentReporteeName,
            selectedCategoryId,
            details,
            out string validationError
        );

        if (report == null)
        {
            SetFeedback(validationError ?? "Unable to create report.", Color.red);
            return;
        }

        // Lock UI during submission
        if (submitReportBtn != null) submitReportBtn.interactable = false;
        if (cancelReportBtn != null) cancelReportBtn.interactable = false;
        if (submitBtnText != null) submitBtnText.text = "Sending...";
        SetFeedback("Sending report to CUBE moderation...", Color.white);

        PlayerReportService.Submit(report, result =>
        {
            // Null-check this instance in case modal was destroyed during async network call
            if (this == null) return;

            if (cancelReportBtn != null)
                cancelReportBtn.interactable = true;

            if (result.IsSuccess)
            {
                SetFeedback(result.Message, new Color(0.4f, 0.9f, 0.5f, 1f));
                if (submitReportBtn != null) submitReportBtn.gameObject.SetActive(false);
                if (cancelReportBtn != null)
                {
                    var cancelTmp = cancelReportBtn.GetComponentInChildren<TextMeshProUGUI>(true);
                    if (cancelTmp != null) cancelTmp.text = "Done";
                }
                RefreshPlayerList();
            }
            else
            {
                SetFeedback(result.Message, new Color(0.95f, 0.4f, 0.4f, 1f));
                if (submitReportBtn != null)
                {
                    submitReportBtn.interactable = result.CanRetry;
                    if (submitBtnText != null)
                        submitBtnText.text = result.CanRetry ? "Retry" : "Unavailable";
                }
            }
        });
    }

    private void SetFeedback(string text, Color color)
    {
        if (reportStatusFeedbackText == null) return;
        reportStatusFeedbackText.text = text;
        reportStatusFeedbackText.color = color;
    }

    public void ShowListView()
    {
        if (listViewRoot != null) listViewRoot.SetActive(true);
        if (reportViewRoot != null) reportViewRoot.SetActive(false);
        RefreshPlayerList();
        FocusListInitialElement();
    }

    public void ShowReportView()
    {
        if (listViewRoot != null) listViewRoot.SetActive(false);
        if (reportViewRoot != null) reportViewRoot.SetActive(true);
        FocusReportInitialElement();
    }

    public void FocusListInitialElement()
    {
        StartCoroutine(SelectDelayed(GetFirstListSelectable()));
    }

    public void FocusReportInitialElement()
    {
        StartCoroutine(SelectDelayed(GetFirstReportSelectable()));
    }

    private GameObject GetFirstListSelectable()
    {
        if (playerListContent != null && playerListContent.childCount > 0)
        {
            for (int i = 0; i < playerListContent.childCount; i++)
            {
                var row = playerListContent.GetChild(i);
                if (row == null || !row.gameObject.activeInHierarchy) continue;
                var btn = row.GetComponentInChildren<Button>();
                if (btn != null && btn.interactable && btn.gameObject.activeInHierarchy)
                    return btn.gameObject;
            }
        }
        return closeButton != null ? closeButton.gameObject : null;
    }

    private GameObject GetFirstReportSelectable()
    {
        if (categoryButtons.Count > 0 && categoryButtons[0] != null && categoryButtons[0].gameObject.activeInHierarchy)
            return categoryButtons[0].gameObject;
        return cancelReportBtn != null ? cancelReportBtn.gameObject : null;
    }

    private IEnumerator SelectDelayed(GameObject target)
    {
        yield return null;
        if (target != null && target.activeInHierarchy && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(target);
        }
    }
}
