using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays a sleek, animated casting/channeling bar on the player's HUD during ability startups.
/// Solves the "latency" feeling by providing immediate visual, audio, and countdown feedback.
/// Supports interruption (flashes red) and clean completion transitions.
/// </summary>
public class AbilityCastBar : MonoBehaviour
{
    private static AbilityCastBar instance;

    private Canvas targetCanvas;
    private GameObject rootPanel;
    private RectTransform fillRect;
    private Image fillImage;
    private Image iconImage;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI timerText;

    private Coroutine hideCoroutine;
    private bool isInterrupted = false;

    private readonly Color normalFillColor = new Color(0f, 0.898f, 1f, 1f);      // Bright Cyan
    private readonly Color interruptedFillColor = new Color(1f, 0.15f, 0.25f, 1f); // Crimson Red
    private readonly Color completeFillColor = new Color(0.2f, 1f, 0.5f, 1f);      // Green

    private void Awake()
    {
        instance = this;
        EnsureUI();
    }

    private void Start()
    {
        EnsureUI();
    }

    private void EnsureUI()
    {
        if (rootPanel != null)
            return;

        // Find or wait for active overlay canvas
        if (targetCanvas == null)
        {
            GameObject hudGo = GameObject.Find("Canvas");
            if (hudGo != null)
                targetCanvas = hudGo.GetComponent<Canvas>();

            if (targetCanvas == null)
            {
                foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                {
                    if (c.renderMode == RenderMode.ScreenSpaceOverlay)
                    {
                        targetCanvas = c;
                        break;
                    }
                }
            }
        }

        if (targetCanvas == null)
            return;

        // Build Cast Bar Hierarchy
        rootPanel = new GameObject("AbilityCastBar_Root", typeof(RectTransform), typeof(Image));
        rootPanel.transform.SetParent(targetCanvas.transform, false);

        RectTransform rootRt = rootPanel.GetComponent<RectTransform>();
        rootRt.anchorMin = new Vector2(0.5f, 0.22f);
        rootRt.anchorMax = new Vector2(0.5f, 0.22f);
        rootRt.pivot = new Vector2(0.5f, 0.5f);
        rootRt.sizeDelta = new Vector2(340f, 48f);

        Image bgImage = rootPanel.GetComponent<Image>();
        bgImage.color = new Color(0.06f, 0.08f, 0.12f, 0.92f);

        // Subtle Border outline
        Outline outline = rootPanel.AddComponent<Outline>();
        outline.effectColor = new Color(0.18f, 0.24f, 0.35f, 0.8f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        // Icon (Left)
        GameObject iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconGo.transform.SetParent(rootPanel.transform, false);
        RectTransform iconRt = iconGo.GetComponent<RectTransform>();
        iconRt.anchorMin = new Vector2(0f, 0.5f);
        iconRt.anchorMax = new Vector2(0f, 0.5f);
        iconRt.pivot = new Vector2(0f, 0.5f);
        iconRt.anchoredPosition = new Vector2(8f, 0f);
        iconRt.sizeDelta = new Vector2(34f, 34f);
        iconImage = iconGo.GetComponent<Image>();
        iconImage.preserveAspect = true;

        // Title Text (Top Left)
        GameObject titleGo = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleGo.transform.SetParent(rootPanel.transform, false);
        RectTransform titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0f, 1f);
        titleRt.anchoredPosition = new Vector2(50f, -8f);
        titleRt.sizeDelta = new Vector2(-120f, 20f);
        titleText = titleGo.GetComponent<TextMeshProUGUI>();
        titleText.fontSize = 14;
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = Color.white;
        titleText.alignment = TextAlignmentOptions.Left;

        // Timer Text (Top Right)
        GameObject timerGo = new GameObject("TimerText", typeof(RectTransform), typeof(TextMeshProUGUI));
        timerGo.transform.SetParent(rootPanel.transform, false);
        RectTransform timerRt = timerGo.GetComponent<RectTransform>();
        timerRt.anchorMin = new Vector2(1f, 1f);
        timerRt.anchorMax = new Vector2(1f, 1f);
        timerRt.pivot = new Vector2(1f, 1f);
        timerRt.anchoredPosition = new Vector2(-12f, -8f);
        timerRt.sizeDelta = new Vector2(60f, 20f);
        timerText = timerGo.GetComponent<TextMeshProUGUI>();
        timerText.fontSize = 13;
        timerText.fontStyle = FontStyles.Bold;
        timerText.color = normalFillColor;
        timerText.alignment = TextAlignmentOptions.Right;

        // Progress Bar Background (Bottom)
        GameObject barBgGo = new GameObject("BarBackground", typeof(RectTransform), typeof(Image));
        barBgGo.transform.SetParent(rootPanel.transform, false);
        RectTransform barBgRt = barBgGo.GetComponent<RectTransform>();
        barBgRt.anchorMin = new Vector2(0f, 0f);
        barBgRt.anchorMax = new Vector2(1f, 0f);
        barBgRt.pivot = new Vector2(0.5f, 0f);
        barBgRt.anchoredPosition = new Vector2(21f, 8f);
        barBgRt.sizeDelta = new Vector2(-60f, 8f);
        Image barBgImage = barBgGo.GetComponent<Image>();
        barBgImage.color = new Color(0.12f, 0.15f, 0.22f, 0.8f);

        // Progress Bar Fill
        GameObject fillGo = new GameObject("BarFill", typeof(RectTransform), typeof(Image));
        fillGo.transform.SetParent(barBgGo.transform, false);
        fillRect = fillGo.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.anchoredPosition = Vector2.zero;
        fillRect.sizeDelta = Vector2.zero;
        fillImage = fillGo.GetComponent<Image>();
        fillImage.color = normalFillColor;

        rootPanel.SetActive(false);
    }

    /// <summary>
    /// Displays and begins a new cast bar on the HUD.
    /// </summary>
    public static void Show(string title, float duration, Sprite icon = null)
    {
        EnsureInstance();
        if (instance == null)
            return;

        instance.EnsureUI();
        if (instance.rootPanel == null)
            return;

        if (instance.hideCoroutine != null)
        {
            instance.StopCoroutine(instance.hideCoroutine);
            instance.hideCoroutine = null;
        }

        instance.isInterrupted = false;
        instance.rootPanel.SetActive(true);

        if (instance.titleText != null)
            instance.titleText.text = title;

        if (instance.timerText != null)
            instance.timerText.text = $"{duration:F1}s";

        if (instance.fillImage != null)
            instance.fillImage.color = instance.normalFillColor;

        if (instance.fillRect != null)
            instance.fillRect.anchorMax = new Vector2(0f, 1f);

        if (instance.iconImage != null)
        {
            if (icon != null)
            {
                instance.iconImage.sprite = icon;
                instance.iconImage.gameObject.SetActive(true);
            }
            else
            {
                instance.iconImage.gameObject.SetActive(false);
            }
        }

        // Tactile start sound
        AudioClip startSfx = Resources.Load<AudioClip>("SFX/woosh");
        if (startSfx != null && Camera.main != null)
        {
            AudioSource.PlayClipAtPoint(startSfx, Camera.main.transform.position, 0.6f);
        }
    }

    /// <summary>
    /// Updates progress (0 to 1) and remaining time on the cast bar.
    /// </summary>
    public static void UpdateProgress(float progress01, float remainingTime)
    {
        if (instance == null || instance.rootPanel == null || instance.isInterrupted)
            return;

        if (!instance.rootPanel.activeSelf)
            instance.rootPanel.SetActive(true);

        if (instance.fillRect != null)
            instance.fillRect.anchorMax = new Vector2(Mathf.Clamp01(progress01), 1f);

        if (instance.timerText != null)
            instance.timerText.text = $"{Mathf.Max(0f, remainingTime):F1}s";
    }

    /// <summary>
    /// Called when the caster is hit or cancelled during startup.
    /// Flashes red, plays an interruption cue, and hides after a brief delay.
    /// </summary>
    public static void Interrupt(string message = "INTERRUPTED!")
    {
        if (instance == null || instance.rootPanel == null)
            return;

        instance.isInterrupted = true;
        if (instance.titleText != null)
            instance.titleText.text = $"<color=#ff2244><b>{message}</b></color>";

        if (instance.fillImage != null)
            instance.fillImage.color = instance.interruptedFillColor;

        if (instance.timerText != null)
            instance.timerText.text = "<color=#ff2244>CANCEL</color>";

        // Play interruption click
        AudioClip hitSfx = Resources.Load<AudioClip>("SFX/punch_hit");
        if (hitSfx != null && Camera.main != null)
        {
            AudioSource.PlayClipAtPoint(hitSfx, Camera.main.transform.position, 0.4f);
        }

        if (instance.hideCoroutine != null)
            instance.StopCoroutine(instance.hideCoroutine);
        instance.hideCoroutine = instance.StartCoroutine(instance.HideDelayed(0.75f));
    }

    /// <summary>
    /// Called when the startup successfully completes.
    /// </summary>
    public static void Complete()
    {
        if (instance == null || instance.rootPanel == null || instance.isInterrupted)
            return;

        if (instance.fillImage != null)
            instance.fillImage.color = instance.completeFillColor;

        if (instance.fillRect != null)
            instance.fillRect.anchorMax = new Vector2(1f, 1f);

        if (instance.hideCoroutine != null)
            instance.StopCoroutine(instance.hideCoroutine);
        instance.hideCoroutine = instance.StartCoroutine(instance.HideDelayed(0.15f));
    }

    private IEnumerator HideDelayed(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (rootPanel != null)
            rootPanel.SetActive(false);
        hideCoroutine = null;
    }

    private static void EnsureInstance()
    {
        if (instance != null)
            return;

        instance = FindFirstObjectByType<AbilityCastBar>();
        if (instance == null)
        {
            GameObject go = new GameObject("AbilityCastBar_Manager", typeof(AbilityCastBar));
            instance = go.GetComponent<AbilityCastBar>();
        }
    }
}
