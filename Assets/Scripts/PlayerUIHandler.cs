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
                smoothHealth = Mathf.Lerp(smoothHealth, playerStats.health, Time.deltaTime * lerpSpeed);
                healthBar.GetComponent<Slider>().value = smoothHealth;
                if (healthText != null)
                    healthText.GetComponent<TMP_Text>().text = $"{playerStats.health}/100";
            }
            if (staminaBar != null)
            {
                smoothStamina = Mathf.Lerp(smoothStamina, playerStats.stamina, Time.deltaTime * lerpSpeed);
                staminaBar.GetComponent<Slider>().value = smoothStamina;
                if (staminaText != null)
                    staminaText.GetComponent<TMP_Text>().text = $"{playerStats.stamina}/100";
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
                           (localBasePlayer.GetPlayerType() == BasePlayer.PlayerType.Killer ||
                            localBasePlayer.GetPlayerType() == BasePlayer.PlayerType.Survivor);
            bool showUI = roundManager.roundActive && hasRole && !roundManager.intermissionActive;

            survivorUI.SetActive(showUI);

            if (showUI)
            {
                // Player name only appears during intermission, never during active rounds.
                if (playerNameText != null) playerNameText.gameObject.SetActive(false);
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
            else if (roundManager.intermissionActive)
            {
                // Show the player name only during intermission.
                if (playerNameText != null)
                {
                    playerNameText.gameObject.SetActive(true);
                    playerNameText.text = PhotonNetwork.NickName;
                }
            }
            else
            {
                if (playerNameText != null) playerNameText.gameObject.SetActive(false);
            }
        }
    }
}
