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

    private void Start()
    {
        playerStats = Camera.main.GetComponent<PlayerCameraReferences>().GetPlayer().GetComponent<BasePlayerStats>();
        localBasePlayer = Camera.main.GetComponent<PlayerCameraReferences>().GetPlayer().GetComponent<BasePlayer>();
        if (playerStats == null)
        {
            Debug.LogError("BasePlayerStats component not found on the player!");
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

    private void OnDestroy()
    {
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
        }

        if (playerStats != null)
        {
            if (healthBar != null)
            {
                Slider healthSlider = healthBar.GetComponent<Slider>();
                healthSlider.maxValue = playerStats.maxHealth;
                smoothHealth = Mathf.Lerp(smoothHealth, playerStats.health, Time.deltaTime * lerpSpeed);
                healthSlider.value = smoothHealth;
                if (healthText != null)
                    healthText.GetComponent<TMP_Text>().text = $"{playerStats.health}/{playerStats.maxHealth}";
            }
            if (staminaBar != null)
            {
                Slider staminaSlider = staminaBar.GetComponent<Slider>();
                staminaSlider.maxValue = playerStats.maxStamina;
                smoothStamina = Mathf.Lerp(smoothStamina, playerStats.stamina, Time.deltaTime * lerpSpeed);
                staminaSlider.value = smoothStamina;
                if (staminaText != null)
                    staminaText.GetComponent<TMP_Text>().text = $"{playerStats.stamina}/{playerStats.maxStamina}";
            }
        }

        if (stats != null && maliceValue != null && playerStats != null)
        {
            maliceValue.text = playerStats.malice.ToString();
        }

        if (timerText != null && roundManager != null)
        {
            int timeRemaining = roundManager.GetCurrentTime();
            int minutes = timeRemaining / 60;
            int seconds = timeRemaining % 60;
            timerText.text = $"{minutes}:{seconds:00}";
        }

        if (survivorUI != null && roundManager != null)
        {
            // Only show in-round UI when the round is active AND this player has been
            // assigned a role (not a mid-round joiner sitting in intermission).
            bool hasRole = localBasePlayer != null &&
                localBasePlayer.GetPlayerType() != BasePlayer.PlayerType.None;
            bool showUI =
                roundManager != null &&
                roundManager.roundActive &&
                !roundManager.intermissionActive &&
                localBasePlayer != null &&
                localBasePlayer.GetPlayerType() != BasePlayer.PlayerType.None;

            survivorUI.SetActive(showUI);

            if (showUI)
            {
                if (pingText != null) pingText.text = $"Ping: {PhotonNetwork.GetPing()}ms";
                if (versionText != null) versionText.text = $"v{Application.version}";

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
