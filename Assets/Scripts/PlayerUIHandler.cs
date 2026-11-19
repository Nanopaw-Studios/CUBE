using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerUIHandler : MonoBehaviour
{
    public RoundManager roundManager;
    private BasePlayerStats playerStats;
    private BasePlayer localBasePlayer;

    [Header("UI References")]
    public TMP_Text timerText;
    public GameObject survivorUI;
    public TMP_Text playerNameText;
    public TMP_Text pingText;
    public TMP_Text fpsText;
    public TMP_Text versionText;
    public GameObject healthBar;
    public Image damageVingette;
    public GameObject staminaBar;
    public GameObject healthText;
    public GameObject staminaText;

    public GameObject stats;
    public GameObject malice;
    public TMP_Text maliceValue;
    public GameObject money;
    public TMP_Text moneyText;
    public GameObject accountLevel;
    public TMP_Text accountLevelValue;
    public Slider accountXpSlider;

    [Header("Profile UI References")]
    public GameObject profileUI;
    public TMP_Text profileNameText;
    public Image profileKillerIcon;
    public Image profileSurvivorIcon;
    public TMP_Text profileKillsText;
    public TMP_Text profileSurvivorWinsText;
    public TMP_Text profileKillerWinsText;
    public TMP_Text profileAccountLevelText;


    private float deltaTime = 0.0f;
    private float updateTimer = 0.0f;
    private float updateInterval = 2.0f;

    private float smoothHealth;
    private float smoothStamina;
    public float lerpSpeed = 5f;

    [Header("Damage Vignette Settings")]
    public float flashFadeSpeed = 3f;
    private float currentFlashAlpha = 0f;
    private BasePlayerStats subscribedPlayerStats;
    private bool achievementSubscribed;
    private Slider cachedHealthSlider;
    private TMP_Text cachedHealthValueText;
    private Slider cachedStaminaSlider;
    private TMP_Text cachedStaminaValueText;

    [Header("Achievement UI References")]
    public GameObject achievementUI;
    public TMP_Text achievementTitleText;
    public TMP_Text achievementDescText;
    public float achievementDisplayDuration = 4.0f;

    private struct AchievementToastData
    {
        public string Title;
        public string Description;
    }

    private readonly System.Collections.Generic.Queue<AchievementToastData> _achievementQueue = new();
    private bool _isShowingAchievement;
    private float _achievementTimer;
    private bool _progressionSubscribed;

    private void Start()
    {
        if (roundManager == null)
            roundManager = FindFirstObjectByType<RoundManager>();

        if (timerText != null)
        {
            timerText.enableAutoSizing = true;
            timerText.fontSizeMin = 20;
            timerText.fontSizeMax = 46;
        }

        TryBindLocalPlayer();

        // Keep the money UI in sync whenever CC changes (add, spend, or refresh).
        if (PlayfabManager.Instance != null)
        {
            PlayfabManager.Instance.OnCubeCoinsChanged += OnCubeCoinsChanged;
            // Seed the display with whatever is already cached.
            if (moneyText != null)
                moneyText.text = PlayfabManager.Instance.CachedCC.ToString();
        }

        AutoBindAchievementUI();
        if (achievementUI != null)
            achievementUI.SetActive(false);

        AutoBindProgressionAndProfileUI();
        TrySubscribeToAchievements();
        TrySubscribeToProgression();
        RefreshProgressionHUD();
        RefreshProfileUI();
    }

    private void AutoBindProgressionAndProfileUI()
    {
        if (stats != null)
        {
            if (accountLevel == null)
            {
                Transform lvlTr = stats.transform.Find("Level");
                if (lvlTr != null)
                    accountLevel = lvlTr.gameObject;
            }
            if (accountLevel != null && accountLevelValue == null)
            {
                Transform valTr = accountLevel.transform.Find("LevelValue") ?? accountLevel.transform.Find("MaliceValue");
                if (valTr != null)
                    accountLevelValue = valTr.GetComponent<TMP_Text>();
            }
            if (accountLevel != null && accountXpSlider == null)
            {
                accountXpSlider = accountLevel.GetComponentInChildren<Slider>(true);
            }
        }

        if (profileUI == null)
        {
            Transform pTr = transform.Find("Profile");
            if (pTr != null)
                profileUI = pTr.gameObject;
        }

        if (profileUI != null)
        {
            if (profileNameText == null)
                profileNameText = profileUI.transform.Find("Name")?.GetComponent<TMP_Text>();
            if (profileKillerIcon == null)
                profileKillerIcon = profileUI.transform.Find("Icons/Killer/Icon")?.GetComponent<Image>();
            if (profileSurvivorIcon == null)
                profileSurvivorIcon = profileUI.transform.Find("Icons/Survivor/Icon")?.GetComponent<Image>();

            Transform content = profileUI.transform.Find("Scroll View/Viewport/Content");
            if (content != null)
            {
                if (profileKillsText == null)
                    profileKillsText = content.Find("Text (TMP)")?.GetComponent<TMP_Text>();
                if (profileSurvivorWinsText == null)
                    profileSurvivorWinsText = content.Find("Text (TMP) (1)")?.GetComponent<TMP_Text>();
                if (profileKillerWinsText == null)
                    profileKillerWinsText = content.Find("Text (TMP) (2)")?.GetComponent<TMP_Text>();
                if (profileAccountLevelText == null)
                    profileAccountLevelText = content.Find("AccountLevelText")?.GetComponent<TMP_Text>();
            }
        }
    }

    private void TrySubscribeToProgression()
    {
        if (_progressionSubscribed)
            return;

        ProgressionManager prog = ProgressionManager.EnsureExists();
        if (prog == null)
            return;

        prog.OnAccountXPChanged += OnAccountXPChanged;
        prog.OnProgressionSynced += OnProgressionSynced;
        _progressionSubscribed = true;
    }

    private void OnAccountXPChanged(int level, int currentXP, int requiredXP)
    {
        RefreshProgressionHUD();
        RefreshProfileUI();
    }

    private void OnProgressionSynced()
    {
        RefreshProgressionHUD();
        RefreshProfileUI();
    }

    public void RefreshProgressionHUD()
    {
        ProgressionManager prog = ProgressionManager.Instance;
        if (prog == null)
            return;

        if (accountLevelValue != null)
        {
            accountLevelValue.text = $"LVL {prog.AccountLevel}\n<size=60%>{prog.AccountXP}/{prog.AccountXPForNextLevel} XP</size>";
        }

        if (accountXpSlider != null)
        {
            accountXpSlider.minValue = 0f;
            accountXpSlider.maxValue = Mathf.Max(1, prog.AccountXPForNextLevel);
            accountXpSlider.value = prog.AccountXP;
        }
    }

    public void RefreshProfileUI()
    {
        if (profileUI == null)
            return;

        ProgressionManager prog = ProgressionManager.Instance;
        int acctLvl = prog != null ? prog.AccountLevel : 1;
        int acctXp = prog != null ? prog.AccountXP : 0;
        int reqXp = prog != null ? prog.AccountXPForNextLevel : 500;

        if (profileNameText != null)
        {
            string pName = PlayfabManager.GetPlayerName();
            if (string.IsNullOrEmpty(pName))
                pName = PhotonNetwork.NickName;
            profileNameText.text = $"{pName}  <color=#00FFFF>[LVL {acctLvl}]</color>";
        }

        if (localBasePlayer != null)
        {
            if (profileKillerIcon != null && localBasePlayer.equippedKiller != null)
                profileKillerIcon.sprite = localBasePlayer.equippedKiller.image;
            if (profileSurvivorIcon != null && localBasePlayer.equippedSurvivor != null)
                profileSurvivorIcon.sprite = localBasePlayer.equippedSurvivor.image;
        }

        var statsSvc = CUBE.Achievements.StatisticsService.Instance;
        int kills = statsSvc != null
            ? Mathf.RoundToInt(statsSvc.GetStat(CUBE.Achievements.StatId.PlayersEliminated, CUBE.Achievements.StatScope.Lifetime))
            : 0;
        int survWins = statsSvc != null
            ? Mathf.RoundToInt(statsSvc.GetStat(CUBE.Achievements.StatId.RoundsSurvived, CUBE.Achievements.StatScope.Lifetime))
            : 0;
        int totalWins = statsSvc != null
            ? Mathf.RoundToInt(statsSvc.GetStat(CUBE.Achievements.StatId.LifetimeWins, CUBE.Achievements.StatScope.Lifetime))
            : 0;
        int killerWins = Mathf.Max(0, totalWins - survWins);

        if (profileKillsText != null)
            profileKillsText.text = $"Kills: {kills}";
        if (profileSurvivorWinsText != null)
            profileSurvivorWinsText.text = $"Survivor Wins: {survWins}";
        if (profileKillerWinsText != null)
            profileKillerWinsText.text = $"Killer Wins: {killerWins}";
        if (profileAccountLevelText != null)
            profileAccountLevelText.text = $"Account Level: {acctLvl} ({acctXp}/{reqXp} XP)";
    }


    /// <summary>
    /// AchievementService may initialize after this HUD, so retry cheaply until it
    /// exists instead of unsubscribing/resubscribing every frame in Update.
    /// </summary>
    private void TrySubscribeToAchievements()
    {
        if (achievementSubscribed)
            return;

        var service = CUBE.Achievements.AchievementService.Instance;
        if (service == null)
            return;

        service.OnAchievementUnlocked += OnAchievementUnlocked;
        achievementSubscribed = true;
    }

    private void TryBindLocalPlayer()
    {
        if (playerStats != null && localBasePlayer != null)
            return;

        GameObject player = null;
        if (PhotonLauncher.LocalPlayerInstance != null)
        {
            player = PhotonLauncher.LocalPlayerInstance;
        }
        else if (Camera.main != null)
        {
            var camRefs = Camera.main.GetComponent<PlayerCameraReferences>();
            if (camRefs != null)
                player = camRefs.GetPlayer();
        }

        if (player != null)
        {
            var stats = player.GetComponent<BasePlayerStats>();
            var basePlayer = player.GetComponent<BasePlayer>();
            if (stats != null)
            {
                playerStats = stats;
                BindPlayerStats(playerStats);
            }
            if (basePlayer != null)
            {
                localBasePlayer = basePlayer;
            }
        }
    }

    private void BindPlayerStats(BasePlayerStats stats)
    {
        if (subscribedPlayerStats == stats)
            return;

        if (subscribedPlayerStats != null)
        {
            subscribedPlayerStats.OnDamageTaken -= OnLocalPlayerDamageTaken;
        }

        subscribedPlayerStats = stats;

        if (subscribedPlayerStats != null)
        {
            subscribedPlayerStats.OnDamageTaken += OnLocalPlayerDamageTaken;
        }
    }

    private void OnLocalPlayerDamageTaken(int damage)
    {
        // Instant red flash on hit
        currentFlashAlpha = 0.75f;
    }

    private void OnDestroy()
    {
        if (subscribedPlayerStats != null)
        {
            subscribedPlayerStats.OnDamageTaken -= OnLocalPlayerDamageTaken;
        }
        if (PlayfabManager.Instance != null)
            PlayfabManager.Instance.OnCubeCoinsChanged -= OnCubeCoinsChanged;

        if (_progressionSubscribed && ProgressionManager.Instance != null)
        {
            ProgressionManager.Instance.OnAccountXPChanged -= OnAccountXPChanged;
            ProgressionManager.Instance.OnProgressionSynced -= OnProgressionSynced;
            _progressionSubscribed = false;
        }

        if (achievementSubscribed && CUBE.Achievements.AchievementService.Instance != null)
        {
            CUBE.Achievements.AchievementService.Instance.OnAchievementUnlocked -= OnAchievementUnlocked;
            achievementSubscribed = false;
        }
    }


    private void OnAchievementUnlocked(CUBE.Achievements.AchievementUnlockedEventArgs args)
    {
        if (args.Definition == null) return;

        string name = LocalizationManager.GetAchievementTitle(args.Definition);
        string desc = LocalizationManager.GetAchievementDescription(args.Definition);

        _achievementQueue.Enqueue(new AchievementToastData
        {
            Title = $"'{name}' achieved",
            Description = $"You achieved '{name}'!\n{desc}"
        });
    }


    private void AutoBindAchievementUI()
    {
        if (achievementUI == null)
        {
            Transform bgTr = transform.Find("Achievement/Background") 
                          ?? transform.Find("Canvas/Achievement/Background") 
                          ?? transform.Find("Achievement");
            if (bgTr != null)
            {
                achievementUI = bgTr.gameObject;
            }
        }

        if (achievementUI != null)
        {
            if (achievementTitleText == null)
            {
                Transform tTr = achievementUI.transform.Find("Title");
                if (tTr != null) achievementTitleText = tTr.GetComponent<TMP_Text>();
            }

            if (achievementDescText == null)
            {
                Transform dTr = achievementUI.transform.Find("Description");
                if (dTr != null) achievementDescText = dTr.GetComponent<TMP_Text>();
            }
        }
    }

    private void OnCubeCoinsChanged(int newBalance)
    {
        if (moneyText != null)
            moneyText.text = newBalance.ToString();
    }

    private void Update()
    {
        if (!achievementSubscribed)
            TrySubscribeToAchievements();

        // Process Achievement Notification Queue
        if (_isShowingAchievement)
        {
            _achievementTimer -= Time.unscaledDeltaTime;
            if (_achievementTimer <= 0f)
            {
                _isShowingAchievement = false;
                if (achievementUI != null)
                    achievementUI.SetActive(false);
            }
        }

        if (!_isShowingAchievement && _achievementQueue.Count > 0)
        {
            AutoBindAchievementUI();
            var toast = _achievementQueue.Dequeue();
            _isShowingAchievement = true;
            _achievementTimer = achievementDisplayDuration;

            if (achievementTitleText != null)
                achievementTitleText.text = toast.Title;

            if (achievementDescText != null)
                achievementDescText.text = toast.Description;

            if (achievementUI != null)
                achievementUI.SetActive(true);
        }

        if (playerStats == null || localBasePlayer == null)
        {
            TryBindLocalPlayer();
        }

        if (playerStats != null)
        {
            if (healthBar != null)
            {
                if (cachedHealthSlider == null)
                    cachedHealthSlider = healthBar.GetComponent<Slider>();

                if (cachedHealthSlider != null)
                {
                    cachedHealthSlider.maxValue = playerStats.maxHealth;
                    smoothHealth = Mathf.Lerp(
                        smoothHealth,
                        playerStats.health,
                        Time.deltaTime * lerpSpeed
                    );
                    cachedHealthSlider.value = smoothHealth;
                }

                if (healthText != null)
                {
                    if (cachedHealthValueText == null)
                        cachedHealthValueText = healthText.GetComponent<TMP_Text>();

                    if (cachedHealthValueText != null)
                        cachedHealthValueText.text = $"{playerStats.health}/{playerStats.maxHealth}";
                }
            }

            if (damageVingette != null)
            {
                // Decay hit-flash alpha over time
                if (currentFlashAlpha > 0f)
                {
                    currentFlashAlpha = Mathf.MoveTowards(
                        currentFlashAlpha,
                        0f,
                        Time.deltaTime * flashFadeSpeed
                    );
                }

                // Calculate persistent low-health vignette alpha when health <= 50% maxHealth
                float lowHealthAlpha = 0f;
                if (playerStats.maxHealth > 0)
                {
                    float healthRatio = smoothHealth / playerStats.maxHealth;
                    if (healthRatio <= 0.5f)
                    {
                        // Smoothly scale low health alpha from 0 at 50% HP down to 0.7 at 0 HP
                        lowHealthAlpha = (0.5f - healthRatio) / 0.5f * 0.7f;
                    }
                }

                float finalAlpha = Mathf.Clamp01(Mathf.Max(currentFlashAlpha, lowHealthAlpha));
                if (finalAlpha > 0.001f)
                {
                    damageVingette.gameObject.SetActive(true);
                    Color c = Color.red;
                    c.a = finalAlpha;
                    damageVingette.color = c;
                }
                else
                {
                    damageVingette.gameObject.SetActive(false);
                }
            }

            if (staminaBar != null)
            {
                if (cachedStaminaSlider == null)
                    cachedStaminaSlider = staminaBar.GetComponent<Slider>();

                if (cachedStaminaSlider != null)
                {
                    cachedStaminaSlider.maxValue = playerStats.maxStamina;
                    smoothStamina = Mathf.Lerp(
                        smoothStamina,
                        playerStats.stamina,
                        Time.deltaTime * lerpSpeed
                    );
                    cachedStaminaSlider.value = smoothStamina;
                }

                if (staminaText != null)
                {
                    if (cachedStaminaValueText == null)
                        cachedStaminaValueText = staminaText.GetComponent<TMP_Text>();

                    if (cachedStaminaValueText != null)
                        cachedStaminaValueText.text = $"{playerStats.stamina}/{playerStats.maxStamina}";
                }
            }
        }

        if (stats != null && maliceValue != null && playerStats != null)
        {
            maliceValue.text = playerStats.malice.ToString();
        }

        if (timerText != null && roundManager != null)
        {
            if (roundManager.roundActive)
            {
                int timeRemaining = roundManager.GetCurrentTime();
                int minutes = timeRemaining / 60;
                int seconds = timeRemaining % 60;
                timerText.text = $"{minutes}:{seconds:00}";
            }
            else
            {
                int minPlayers = roundManager.GetRequiredPlayerCount();
                int currentPlayers = PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom != null
                    ? PhotonNetwork.CurrentRoom.PlayerCount
                    : 1;

                if (!roundManager.HasEnoughPlayers())
                {
                    timerText.text = LocalizationManager.Format(
                        "hud.waiting_players",
                        "Waiting for players ({0}/{1} minimum)... Free-play lobby active.",
                        currentPlayers,
                        minPlayers
                    );
                }
                else
                {
                    int timeRemaining = roundManager.GetCurrentTime();
                    int minutes = timeRemaining / 60;
                    int seconds = timeRemaining % 60;
                    timerText.text = LocalizationManager.Format(
                        "hud.waiting_countdown",
                        "Waiting for players ({0}/{1} minimum)... {2}:{3:00}",
                        currentPlayers,
                        minPlayers,
                        minutes,
                        seconds
                    );
                }

            }
        }

        if (survivorUI != null && roundManager != null)
        {
            // Only show in-round UI when the round is active AND this player has been
            // assigned a role (not a mid-round joiner sitting in intermission).
            bool showUI =
                roundManager != null
                && roundManager.roundActive
                && !roundManager.intermissionActive
                && localBasePlayer != null
                && localBasePlayer.GetPlayerType() != BasePlayer.PlayerType.None;

            survivorUI.SetActive(showUI);

            if (showUI)
            {
                if (pingText != null)
                    pingText.text = $"Ping: {PhotonNetwork.GetPing()}ms";
                if (versionText != null)
                    versionText.text = $"v{Application.version}";

                if (fpsText != null)
                {
                    deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
                    updateTimer += Time.unscaledDeltaTime;
                    if (updateTimer >= updateInterval)
                    {
                        float fps = 1.0f / deltaTime;
                        fpsText.text = $"FPS: {fps:F1}";
                        updateTimer = 0f;
                    }
                }
            }
        }
        else if (roundManager != null)
        {
            if (playerNameText != null)
            {
                bool showName = roundManager.intermissionActive;
                if (showName)
                    playerNameText.text = PhotonNetwork.NickName;
            }
        }
    }
}
