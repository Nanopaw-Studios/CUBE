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

    private void Start()
    {
        playerStats = Camera
            .main.GetComponent<PlayerCameraReferences>()
            .GetPlayer()
            .GetComponent<BasePlayerStats>();
        localBasePlayer = Camera
            .main.GetComponent<PlayerCameraReferences>()
            .GetPlayer()
            .GetComponent<BasePlayer>();

        if (playerStats == null)
        {
            Debug.LogError("BasePlayerStats component not found on the player!");
        }
        else
        {
            BindPlayerStats(playerStats);
        }

        // Keep the money UI in sync whenever CC changes (add, spend, or refresh).
        if (PlayfabManager.Instance != null)
        {
            PlayfabManager.Instance.OnCubeCoinsChanged += OnCubeCoinsChanged;
            // Seed the display with whatever is already cached.
            if (moneyText != null)
                moneyText.text = PlayfabManager.Instance.CachedCC.ToString();
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
    }

    private void OnCubeCoinsChanged(int newBalance)
    {
        if (moneyText != null)
            moneyText.text = newBalance.ToString();
    }

    private void Update()
    {
        if (playerStats == null || localBasePlayer == null)
        {
            var player = Camera.main.GetComponent<PlayerCameraReferences>().GetPlayer();
            playerStats = player.GetComponent<BasePlayerStats>();
            localBasePlayer = player.GetComponent<BasePlayer>();
            if (playerStats != null)
            {
                BindPlayerStats(playerStats);
            }
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
                    Color c = damageVingette.color;
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
            if (roundManager.HasEnoughPlayers())
            {
                int timeRemaining = roundManager.GetCurrentTime();
                int minutes = timeRemaining / 60;
                int seconds = timeRemaining % 60;
                timerText.text = $"{minutes}:{seconds:00}";
            }
            else
            {
                timerText.text = "Waiting for enough players..";
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
