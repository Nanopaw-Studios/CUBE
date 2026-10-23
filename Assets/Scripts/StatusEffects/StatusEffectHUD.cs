using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Displays active status effect badges on the local player's HUD.
/// Shows icon, effect name (with Roman numerals), and a ticking countdown timer.
/// Automatically binds to the local player's StatusEffectManager.
/// </summary>
public class StatusEffectHUD : MonoBehaviour
{
    private StatusEffectManager statusManager;
    private readonly Dictionary<StatusEffectType, GameObject> activePills = new();
    private readonly Dictionary<StatusEffectType, TextMeshProUGUI> pillTexts = new();

    [Tooltip("Parent transform holding the status effect pills. If null, creates a clean vertical/horizontal container.")]
    public Transform container;

    private void Start()
    {
        FindLocalStatusManager();
        EnsureContainer();
    }

    private void FindLocalStatusManager()
    {
        if (statusManager != null) return;

        // Try getting from parent/self
        statusManager = GetComponentInParent<StatusEffectManager>();

        if (statusManager == null && PhotonLauncher.LocalPlayerInstance != null)
        {
            statusManager = PhotonLauncher.LocalPlayerInstance.GetComponent<StatusEffectManager>();
        }

        if (statusManager != null)
        {
            statusManager.OnEffectsChanged += RefreshPills;
            RefreshPills();
        }
    }

    private void EnsureContainer()
    {
        if (container != null) return;

        // Try finding Canvas/SurvivorStats
        GameObject statsCanvas = GameObject.Find("Canvas/SurvivorStats");
        if (statsCanvas != null)
        {
            Transform existing = statsCanvas.transform.Find("StatusEffectsContainer");
            if (existing != null)
            {
                container = existing;
                return;
            }

            GameObject newContainer = new GameObject("StatusEffectsContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            newContainer.transform.SetParent(statsCanvas.transform, false);

            RectTransform rt = newContainer.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.12f);
            rt.anchorMax = new Vector2(0.5f, 0.12f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(400, 32);

            HorizontalLayoutGroup hlg = newContainer.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlHeight = true;
            hlg.childControlWidth = false;

            ContentSizeFitter csf = newContainer.GetComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            container = newContainer.transform;
        }
    }

    private void Update()
    {
        if (statusManager == null)
        {
            FindLocalStatusManager();
            return;
        }

        // Live tick update for duration texts
        foreach (var effect in statusManager.GetActiveEffects())
        {
            if (pillTexts.TryGetValue(effect.type, out var textComp) && textComp != null)
            {
                textComp.text = $"{effect.Icon} {effect.DisplayName}  <size=80%>{effect.durationRemaining:F1}s</size>";
            }
        }
    }

    private void RefreshPills()
    {
        if (statusManager == null || container == null) return;

        var currentActive = statusManager.GetActiveEffects();
        var activeTypes = new HashSet<StatusEffectType>();

        foreach (var effect in currentActive)
        {
            activeTypes.Add(effect.type);

            if (!activePills.ContainsKey(effect.type))
            {
                CreatePill(effect);
            }
            else
            {
                UpdatePillStyle(effect);
            }
        }

        // Clean up expired pills
        var toRemove = new List<StatusEffectType>();
        foreach (var type in activePills.Keys)
        {
            if (!activeTypes.Contains(type))
                toRemove.Add(type);
        }

        foreach (var type in toRemove)
        {
            if (activePills.TryGetValue(type, out var pill))
            {
                Destroy(pill);
                activePills.Remove(type);
                pillTexts.Remove(type);
            }
        }
    }

    private void CreatePill(ActiveStatusEffect effect)
    {
        GameObject pill = new GameObject($"Pill_{effect.type}", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
        pill.transform.SetParent(container, false);

        Image bg = pill.GetComponent<Image>();
        bg.color = new Color(0.08f, 0.08f, 0.10f, 0.85f); // Sleek dark card

        HorizontalLayoutGroup hlg = pill.GetComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(12, 12, 6, 6);
        hlg.childAlignment = TextAnchor.MiddleCenter;

        GameObject textGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGO.transform.SetParent(pill.transform, false);

        TextMeshProUGUI textComp = textGO.GetComponent<TextMeshProUGUI>();
        textComp.fontSize = 15;
        textComp.alignment = TextAlignmentOptions.Center;

        ColorUtility.TryParseHtmlString(effect.ColorHex, out Color effectColor);
        textComp.color = effectColor;
        textComp.text = $"{effect.Icon} {effect.DisplayName}  <size=80%>{effect.durationRemaining:F1}s</size>";

        activePills[effect.type] = pill;
        pillTexts[effect.type] = textComp;
    }

    private void UpdatePillStyle(ActiveStatusEffect effect)
    {
        if (pillTexts.TryGetValue(effect.type, out var textComp) && textComp != null)
        {
            ColorUtility.TryParseHtmlString(effect.ColorHex, out Color effectColor);
            textComp.color = effectColor;
        }
    }

    private void OnDestroy()
    {
        if (statusManager != null)
        {
            statusManager.OnEffectsChanged -= RefreshPills;
        }
    }
}
