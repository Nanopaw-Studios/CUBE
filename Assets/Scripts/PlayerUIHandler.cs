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

        if (CUBE.Achievements.AchievementService.Instance != null)
        {
            CUBE.Achievements.AchievementService.Instance.OnAchievementUnlocked += OnAchievementUnlocked;
        }
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

        if (CUBE.Achievements.AchievementService.Instance != null)
        {
            CUBE.Achievements.AchievementService.Instance.OnAchievementUnlocked -= OnAchievementUnlocked;
        }
    }

    private void OnAchievementUnlocked(CUBE.Achievements.AchievementUnlockedEventArgs args)
    {
        if (args.Definition == null) return;

        string name = args.Definition.DisplayName;
        string desc = args.Definition.Description;

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
        if (CUBE.Achievements.AchievementService.Instance != null)
        {
            CUBE.Achievements.AchievementService.Instance.OnAchievementUnlocked -= OnAchievementUnlocked;
            CUBE.Achievements.AchievementService.Instance.OnAchievementUnlocked += OnAchievementUnlocked;
        }

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
                Slider healthSlider = healthBar.GetComponent<Slider>();
                healthSlider.maxValue = playerStats.maxHealth;
                smoothHealth = Mathf.Lerp(
                    smoothHealth,
                    playerStats.health,
                    Time.deltaTime * lerpSpeed
                );
                healthSlider.value = smoothHealth;
                if (healthText != null)
                    healthText.GetComponent<TMP_Text>().text =
                        $"{playerStats.health}/{playerStats.maxHealth}";
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
                Slider staminaSlider = staminaBar.GetComponent<Slider>();
                staminaSlider.maxValue = playerStats.maxStamina;
                smoothStamina = Mathf.Lerp(
                    smoothStamina,
                    playerStats.stamina,
                    Time.deltaTime * lerpSpeed
                );
                staminaSlider.value = smoothStamina;
                if (staminaText != null)
                    staminaText.GetComponent<TMP_Text>().text =
                        $"{playerStats.stamina}/{playerStats.maxStamina}";
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
                    timerText.text = $"Waiting for players ({currentPlayers}/{minPlayers} minimum)... Free-play lobby active.";
                }
                else
                {
                    int timeRemaining = roundManager.GetCurrentTime();
                    int minutes = timeRemaining / 60;
                    int seconds = timeRemaining % 60;
                    timerText.text = $"Waiting for players ({currentPlayers}/{minPlayers} minimum)... {minutes}:{seconds:00}";
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
