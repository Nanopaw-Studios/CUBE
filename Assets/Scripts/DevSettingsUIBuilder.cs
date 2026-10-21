using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Programmatically builds and manages the developer-exclusive physics settings category.
/// Injects rows directly into the game's Settings menu matching the native layout and typography.
/// </summary>
public static class DevSettingsUIBuilder
{
    private static readonly Color GoldColor = new Color(1f, 0.78f, 0.15f, 1f);

    public static void BuildDevCategory(Transform parentContent, GameObject templateSliderRow = null, float overrideRowWidth = 0f)
    {
        if (parentContent == null) return;

        // Prevent duplicate creation
        if (parentContent.Find("Dev_Header") != null)
            return;

        float rowWidth = overrideRowWidth > 0f
            ? overrideRowWidth
            : (templateSliderRow != null ? templateSliderRow.GetComponent<RectTransform>().sizeDelta.x : 982.25f);

        TMP_FontAsset fontAsset = null;
        if (templateSliderRow != null)
        {
            var tmp = templateSliderRow.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp != null) fontAsset = tmp.font;
        }
        if (fontAsset == null && parentContent != null)
        {
            var tmp = parentContent.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp != null) fontAsset = tmp.font;
        }

        // 1. Category Header Banner
        CreateHeader(parentContent, rowWidth, fontAsset);

        // 2. Enable / Disable Toggle Row (matching Fullscreen row)
        Transform fullscreenRow = parentContent.Find("Fullscreen");
        CreateToggleRow(parentContent, rowWidth, fullscreenRow != null ? fullscreenRow.gameObject : null, fontAsset);

        // 3. Slider Rows (matching Master Volume)
        CreateSliderRow(
            parentContent,
            "Dev_SpringPower",
            "Spring Power / Stiffness",
            DevPhysicsSettings.MinSpringPower,
            DevPhysicsSettings.MaxSpringPower,
            DevPhysicsSettings.SpringPower,
            "F3",
            val => DevPhysicsSettings.SpringPower = val,
            templateSliderRow,
            rowWidth,
            fontAsset
        );

        CreateSliderRow(
            parentContent,
            "Dev_Damping",
            "Damping",
            DevPhysicsSettings.MinDamping,
            DevPhysicsSettings.MaxDamping,
            DevPhysicsSettings.Damping,
            "F2",
            val => DevPhysicsSettings.Damping = val,
            templateSliderRow,
            rowWidth,
            fontAsset
        );

        CreateSliderRow(
            parentContent,
            "Dev_Inertia",
            "Inertia / Mass",
            DevPhysicsSettings.MinInertia,
            DevPhysicsSettings.MaxInertia,
            DevPhysicsSettings.Inertia,
            "F2",
            val => DevPhysicsSettings.Inertia = val,
            templateSliderRow,
            rowWidth,
            fontAsset
        );

        CreateSliderRow(
            parentContent,
            "Dev_LimitDistance",
            "Movement Limit Distance",
            DevPhysicsSettings.MinLimitDistance,
            DevPhysicsSettings.MaxLimitDistance,
            DevPhysicsSettings.LimitDistance,
            "F3",
            val => DevPhysicsSettings.LimitDistance = val,
            templateSliderRow,
            rowWidth,
            fontAsset
        );

        CreateSliderRow(
            parentContent,
            "Dev_Size",
            "Size (Scale)",
            DevPhysicsSettings.MinSizeMultiplier,
            DevPhysicsSettings.MaxSizeMultiplier,
            DevPhysicsSettings.SizeMultiplier,
            "F2",
            val => DevPhysicsSettings.SizeMultiplier = val,
            templateSliderRow,
            rowWidth,
            fontAsset
        );

        CreateSliderRow(
            parentContent,
            "Dev_Offset",
            "Vertical Placement",
            DevPhysicsSettings.MinVerticalOffset,
            DevPhysicsSettings.MaxVerticalOffset,
            DevPhysicsSettings.VerticalOffset,
            "F2",
            val => DevPhysicsSettings.VerticalOffset = val,
            templateSliderRow,
            rowWidth,
            fontAsset
        );

        // 4. Reset Defaults Button Row
        CreateResetButtonRow(parentContent, rowWidth, templateSliderRow, fontAsset);
    }

    private static void CreateHeader(Transform parent, float width, TMP_FontAsset font)
    {
        GameObject headerGO = new GameObject("Dev_Header", typeof(RectTransform));
        headerGO.transform.SetParent(parent, false);

        RectTransform rt = headerGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(width, 50f);

        TextMeshProUGUI tmp = headerGO.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = "★ DEV EXCLUSIVE — SECONDARY PHYSICS ★";
        tmp.fontSize = 24f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = GoldColor;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
    }

    private static void CreateToggleRow(Transform parent, float width, GameObject fullscreenTemplate, TMP_FontAsset font)
    {
        if (fullscreenTemplate != null)
        {
            GameObject clone = UnityEngine.Object.Instantiate(fullscreenTemplate, parent, false);
            clone.name = "Dev_Toggle";

            RectTransform rt = clone.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(width, 75f);

            // Update label
            Transform textT = clone.transform.Find("Text");
            TextMeshProUGUI label = textT != null ? textT.GetComponent<TextMeshProUGUI>() : null;
            if (label != null)
            {
                label.text = "Secondary Physics (Local)";
                label.raycastTarget = false;
            }

            // Rewire toggle
            Toggle toggle = clone.GetComponentInChildren<Toggle>(true);
            if (toggle != null)
            {
                toggle.interactable = true;
                toggle.onValueChanged.RemoveAllListeners();
                toggle.SetIsOnWithoutNotify(DevPhysicsSettings.IsDevPhysicsEnabled);
                toggle.onValueChanged.AddListener(isOn =>
                {
                    DevPhysicsSettings.IsDevPhysicsEnabled = isOn;
                });

                Action onToggleChange = () =>
                {
                    if (toggle != null)
                        toggle.SetIsOnWithoutNotify(DevPhysicsSettings.IsDevPhysicsEnabled);
                };
                DevPhysicsSettings.OnSettingsChanged += onToggleChange;
                var listener = clone.AddComponent<DevUIEventListener>();
                listener.cleanup = () => DevPhysicsSettings.OnSettingsChanged -= onToggleChange;
            }

            return;
        }

        // Fallback programmatic toggle row
        GameObject row = new GameObject("Dev_Toggle", typeof(RectTransform));
        row.transform.SetParent(parent, false);

        RectTransform rowRT = row.GetComponent<RectTransform>();
        rowRT.anchorMin = new Vector2(0f, 1f);
        rowRT.anchorMax = new Vector2(0f, 1f);
        rowRT.sizeDelta = new Vector2(width, 75f);

        // Label on left
        GameObject labelGO = new GameObject("Text", typeof(RectTransform));
        labelGO.transform.SetParent(row.transform, false);
        RectTransform lrt = labelGO.GetComponent<RectTransform>();
        lrt.anchorMin = new Vector2(0f, 0.5f);
        lrt.anchorMax = new Vector2(0f, 0.5f);
        lrt.pivot = new Vector2(0f, 0.5f);
        lrt.anchoredPosition = new Vector2(25f, 0f);
        lrt.sizeDelta = new Vector2(380f, 60f);

        TextMeshProUGUI lTmp = labelGO.AddComponent<TextMeshProUGUI>();
        if (font != null) lTmp.font = font;
        lTmp.text = "Secondary Physics (Local)";
        lTmp.fontSize = 26f;
        lTmp.color = Color.white;
        lTmp.alignment = TextAlignmentOptions.Left;
        lTmp.raycastTarget = false;

        // Button / Toggle on right
        GameObject btnGO = new GameObject("ToggleBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        btnGO.transform.SetParent(row.transform, false);
        RectTransform btnRT = btnGO.GetComponent<RectTransform>();
        btnRT.anchorMin = new Vector2(1f, 0.5f);
        btnRT.anchorMax = new Vector2(1f, 0.5f);
        btnRT.pivot = new Vector2(0.5f, 0.5f);
        btnRT.anchoredPosition = new Vector2(-235f, 0f);
        btnRT.sizeDelta = new Vector2(180f, 45f);

        Image btnImg = btnGO.GetComponent<Image>();
        btnImg.raycastTarget = true;

        Button btn = btnGO.GetComponent<Button>();
        btn.targetGraphic = btnImg;
        btn.interactable = true;

        GameObject btnTextGO = new GameObject("Text", typeof(RectTransform));
        btnTextGO.transform.SetParent(btnGO.transform, false);
        RectTransform textRT = btnTextGO.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.sizeDelta = Vector2.zero;

        TextMeshProUGUI btnTmp = btnTextGO.AddComponent<TextMeshProUGUI>();
        if (font != null) btnTmp.font = font;
        btnTmp.fontSize = 18f;
        btnTmp.fontStyle = FontStyles.Bold;
        btnTmp.alignment = TextAlignmentOptions.Center;
        btnTmp.color = Color.white;
        btnTmp.raycastTarget = false;

        void UpdateBtn()
        {
            if (btnGO == null || btnImg == null || btnTmp == null) return;
            bool enabled = DevPhysicsSettings.IsDevPhysicsEnabled;
            btnImg.color = enabled ? new Color(0.1f, 0.75f, 0.35f, 1f) : new Color(0.75f, 0.2f, 0.2f, 1f);
            btnTmp.text = enabled ? "ENABLED" : "DISABLED";
        }

        UpdateBtn();
        btn.onClick.AddListener(() =>
        {
            DevPhysicsSettings.IsDevPhysicsEnabled = !DevPhysicsSettings.IsDevPhysicsEnabled;
            UpdateBtn();
        });

        Action onBtnChange = UpdateBtn;
        DevPhysicsSettings.OnSettingsChanged += onBtnChange;
        var btnListener = row.AddComponent<DevUIEventListener>();
        btnListener.cleanup = () => DevPhysicsSettings.OnSettingsChanged -= onBtnChange;
    }

    private static void CreateSliderRow(
        Transform parent,
        string objectName,
        string labelText,
        float min,
        float max,
        float initialValue,
        string format,
        Action<float> onValueChanged,
        GameObject templateRow,
        float width,
        TMP_FontAsset font)
    {
        if (templateRow != null)
        {
            GameObject clone = UnityEngine.Object.Instantiate(templateRow, parent, false);
            clone.name = objectName;

            RectTransform rt = clone.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(width, 75f);

            // Destroy the SliderValueText component so it doesn't calculate percent and override our value
            var svt = clone.GetComponentInChildren<SliderValueText>(true);
            if (svt != null)
                UnityEngine.Object.Destroy(svt);

            // Prevent clone background image from blocking raycasts
            Image rowImg = clone.GetComponent<Image>();
            if (rowImg != null)
                rowImg.raycastTarget = false;

            // Update Label (child named "Text")
            Transform textT = clone.transform.Find("Text");
            if (textT != null)
            {
                RectTransform cloneTextRT = textT.GetComponent<RectTransform>();
                cloneTextRT.anchorMin = new Vector2(0f, 0.5f);
                cloneTextRT.anchorMax = new Vector2(0f, 0.5f);
                cloneTextRT.pivot = new Vector2(0f, 0.5f);
                cloneTextRT.anchoredPosition = new Vector2(25f, 0f);
                cloneTextRT.sizeDelta = new Vector2(380f, 60f);
            }
            TextMeshProUGUI label = textT != null ? textT.GetComponent<TextMeshProUGUI>() : null;
            if (label != null)
            {
                label.text = labelText;
                label.fontSize = 26f;
                label.raycastTarget = false;
            }

            // Update Value display (child named "Value")
            Transform valT = clone.transform.Find("Value");
            if (valT != null)
            {
                RectTransform cloneValRT = valT.GetComponent<RectTransform>();
                cloneValRT.anchorMin = new Vector2(1f, 0.5f);
                cloneValRT.anchorMax = new Vector2(1f, 0.5f);
                cloneValRT.pivot = new Vector2(1f, 0.5f);
                cloneValRT.anchoredPosition = new Vector2(-20f, 0f);
                cloneValRT.sizeDelta = new Vector2(90f, 60f);
            }
            TextMeshProUGUI valText = valT != null ? valT.GetComponent<TextMeshProUGUI>() : null;
            if (valText != null)
            {
                valText.fontSize = 26f;
                valText.raycastTarget = false;
            }

            // Configure Slider
            Slider slider = clone.GetComponentInChildren<Slider>(true);
            if (slider != null)
            {
                RectTransform cloneSliderRT = slider.GetComponent<RectTransform>();
                cloneSliderRT.anchorMin = new Vector2(1f, 0.5f);
                cloneSliderRT.anchorMax = new Vector2(1f, 0.5f);
                cloneSliderRT.pivot = new Vector2(0.5f, 0.5f);
                cloneSliderRT.anchoredPosition = new Vector2(-235f, 0f);
                cloneSliderRT.sizeDelta = new Vector2(240f, 25f);

                slider.interactable = true;
                slider.wholeNumbers = false;
                slider.onValueChanged.RemoveAllListeners();
                slider.minValue = min;
                slider.maxValue = max;
                slider.SetValueWithoutNotify(initialValue);

                if (valText != null)
                    valText.text = initialValue.ToString(format);

                slider.onValueChanged.AddListener(v =>
                {
                    if (valText != null)
                        valText.text = v.ToString(format);
                    onValueChanged?.Invoke(v);
                });

                // Attach conflict fix so ScrollRect drag doesn't hijack slider
                if (slider.gameObject.GetComponent<SliderScrollConflictFix>() == null)
                    slider.gameObject.AddComponent<SliderScrollConflictFix>();

                Action onSliderChange = () =>
                {
                    if (slider != null)
                    {
                        float cur = initialValue;
                        if (objectName.Contains("Power")) cur = DevPhysicsSettings.SpringPower;
                        else if (objectName.Contains("Damping")) cur = DevPhysicsSettings.Damping;
                        else if (objectName.Contains("Inertia")) cur = DevPhysicsSettings.Inertia;
                        else if (objectName.Contains("LimitDistance")) cur = DevPhysicsSettings.LimitDistance;
                        else if (objectName.Contains("Size")) cur = DevPhysicsSettings.SizeMultiplier;
                        else if (objectName.Contains("Offset")) cur = DevPhysicsSettings.VerticalOffset;

                        slider.SetValueWithoutNotify(cur);
                        if (valText != null)
                            valText.text = cur.ToString(format);
                    }
                };
                DevPhysicsSettings.OnSettingsChanged += onSliderChange;
                var sliderListener = clone.AddComponent<DevUIEventListener>();
                sliderListener.cleanup = () => DevPhysicsSettings.OnSettingsChanged -= onSliderChange;
            }

            return;
        }

        // Fallback row if no template
        GameObject row = new GameObject(objectName, typeof(RectTransform));
        row.transform.SetParent(parent, false);

        RectTransform rrt = row.GetComponent<RectTransform>();
        rrt.anchorMin = new Vector2(0f, 1f);
        rrt.anchorMax = new Vector2(0f, 1f);
        rrt.sizeDelta = new Vector2(width, 75f);

        // Label on left
        GameObject lGO = new GameObject("Text", typeof(RectTransform));
        lGO.transform.SetParent(row.transform, false);
        RectTransform lrt = lGO.GetComponent<RectTransform>();
        lrt.anchorMin = new Vector2(0f, 0.5f);
        lrt.anchorMax = new Vector2(0f, 0.5f);
        lrt.pivot = new Vector2(0f, 0.5f);
        lrt.anchoredPosition = new Vector2(25f, 0f);
        lrt.sizeDelta = new Vector2(380f, 60f);

        TextMeshProUGUI lTmp = lGO.AddComponent<TextMeshProUGUI>();
        if (font != null) lTmp.font = font;
        lTmp.text = labelText;
        lTmp.fontSize = 26f;
        lTmp.color = Color.white;
        lTmp.alignment = TextAlignmentOptions.Left;
        lTmp.raycastTarget = false;

        // Slider on right
        GameObject sGO = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
        sGO.transform.SetParent(row.transform, false);
        RectTransform srt = sGO.GetComponent<RectTransform>();
        srt.anchorMin = new Vector2(1f, 0.5f);
        srt.anchorMax = new Vector2(1f, 0.5f);
        srt.pivot = new Vector2(0.5f, 0.5f);
        srt.anchoredPosition = new Vector2(-235f, 0f);
        srt.sizeDelta = new Vector2(240f, 25f);

        Slider s = sGO.GetComponent<Slider>();
        s.interactable = true;
        s.wholeNumbers = false;
        s.minValue = min;
        s.maxValue = max;
        s.SetValueWithoutNotify(initialValue);

        if (s.gameObject.GetComponent<SliderScrollConflictFix>() == null)
            s.gameObject.AddComponent<SliderScrollConflictFix>();

        // Value text
        GameObject vGO = new GameObject("Value", typeof(RectTransform));
        vGO.transform.SetParent(row.transform, false);
        RectTransform vrt = vGO.GetComponent<RectTransform>();
        vrt.anchorMin = new Vector2(1f, 0.5f);
        vrt.anchorMax = new Vector2(1f, 0.5f);
        vrt.pivot = new Vector2(1f, 0.5f);
        vrt.anchoredPosition = new Vector2(-20f, 0f);
        vrt.sizeDelta = new Vector2(90f, 60f);

        TextMeshProUGUI vTmp = vGO.AddComponent<TextMeshProUGUI>();
        if (font != null) vTmp.font = font;
        vTmp.fontSize = 26f;
        vTmp.color = Color.white;
        vTmp.text = initialValue.ToString(format);
        vTmp.alignment = TextAlignmentOptions.Right;
        vTmp.raycastTarget = false;

        s.onValueChanged.AddListener(v =>
        {
            vTmp.text = v.ToString(format);
            onValueChanged?.Invoke(v);
        });

        Action onFallbackChange = () =>
        {
            if (s != null)
            {
                float cur = initialValue;
                if (objectName.Contains("Power")) cur = DevPhysicsSettings.SpringPower;
                else if (objectName.Contains("Damping")) cur = DevPhysicsSettings.Damping;
                else if (objectName.Contains("Inertia")) cur = DevPhysicsSettings.Inertia;
                else if (objectName.Contains("LimitDistance")) cur = DevPhysicsSettings.LimitDistance;
                else if (objectName.Contains("Size")) cur = DevPhysicsSettings.SizeMultiplier;
                else if (objectName.Contains("Offset")) cur = DevPhysicsSettings.VerticalOffset;

                s.SetValueWithoutNotify(cur);
                if (vTmp != null)
                    vTmp.text = cur.ToString(format);
            }
        };
        DevPhysicsSettings.OnSettingsChanged += onFallbackChange;
        var fallbackListener = row.AddComponent<DevUIEventListener>();
        fallbackListener.cleanup = () => DevPhysicsSettings.OnSettingsChanged -= onFallbackChange;
    }

    private static void CreateResetButtonRow(Transform parent, float width, GameObject templateRow, TMP_FontAsset font)
    {
        GameObject row = new GameObject("Dev_Reset", typeof(RectTransform));
        row.transform.SetParent(parent, false);

        RectTransform rt = row.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(width, 75f);

        // Label on left
        GameObject labelGO = new GameObject("Text", typeof(RectTransform));
        labelGO.transform.SetParent(row.transform, false);
        RectTransform lrt = labelGO.GetComponent<RectTransform>();
        lrt.anchorMin = new Vector2(0f, 0.5f);
        lrt.anchorMax = new Vector2(0f, 0.5f);
        lrt.pivot = new Vector2(0f, 0.5f);
        lrt.anchoredPosition = new Vector2(25f, 0f);
        lrt.sizeDelta = new Vector2(380f, 60f);

        TextMeshProUGUI lTmp = labelGO.AddComponent<TextMeshProUGUI>();
        if (font != null) lTmp.font = font;
        lTmp.text = "Reset Physics Defaults";
        lTmp.fontSize = 26f;
        lTmp.color = Color.white;
        lTmp.alignment = TextAlignmentOptions.Left;
        lTmp.raycastTarget = false;

        // Button on right
        GameObject btnGO = new GameObject("ResetBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        btnGO.transform.SetParent(row.transform, false);
        RectTransform btnRT = btnGO.GetComponent<RectTransform>();
        btnRT.anchorMin = new Vector2(1f, 0.5f);
        btnRT.anchorMax = new Vector2(1f, 0.5f);
        btnRT.pivot = new Vector2(0.5f, 0.5f);
        btnRT.anchoredPosition = new Vector2(-235f, 0f);
        btnRT.sizeDelta = new Vector2(180f, 48f);

        Image img = btnGO.GetComponent<Image>();
        img.color = new Color(0.28f, 0.28f, 0.32f, 1f);
        img.raycastTarget = true;

        Button btn = btnGO.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.interactable = true;

        GameObject textGO = new GameObject("Text", typeof(RectTransform));
        textGO.transform.SetParent(btnGO.transform, false);
        RectTransform textRT = textGO.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.sizeDelta = Vector2.zero;

        TextMeshProUGUI tmp = textGO.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = "Reset Defaults";
        tmp.fontSize = 18f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;

        btn.onClick.AddListener(() =>
        {
            DevPhysicsSettings.ResetToDefaults();
        });
    }

    /// <summary>
    /// Builds a standalone, developer-exclusive modal popup window containing all secondary physics settings.
    /// Can be opened from the Pause Menu or anywhere in gameplay.
    /// </summary>
    public static GameObject BuildDevPhysicsModal(Transform parent, GameObject templateButton = null, GameObject templateSliderRow = null)
    {
        if (parent == null) return null;

        Transform existing = parent.Find("Dev_PhysicsModal");
        if (existing != null)
            return existing.gameObject;

        // 1. Fullscreen dark backdrop blocker
        GameObject modalGO = new GameObject("Dev_PhysicsModal", typeof(RectTransform), typeof(Image));
        modalGO.transform.SetParent(parent, false);

        RectTransform modalRT = modalGO.GetComponent<RectTransform>();
        modalRT.anchorMin = Vector2.zero;
        modalRT.anchorMax = Vector2.one;
        modalRT.offsetMin = Vector2.zero;
        modalRT.offsetMax = Vector2.zero;

        Image modalBg = modalGO.GetComponent<Image>();
        modalBg.color = new Color(0f, 0f, 0f, 0.78f);
        modalBg.raycastTarget = true;

        // Backdrop button allows clicking outside the dialog to dismiss
        Button backdropBtn = modalGO.AddComponent<Button>();
        backdropBtn.transition = Selectable.Transition.None;
        backdropBtn.onClick.AddListener(() => modalGO.SetActive(false));

        // 2. Centered Dialog Box
        GameObject dialogGO = new GameObject("Dialog", typeof(RectTransform), typeof(Image));
        dialogGO.transform.SetParent(modalGO.transform, false);

        RectTransform dialogRT = dialogGO.GetComponent<RectTransform>();
        dialogRT.anchorMin = new Vector2(0.5f, 0.5f);
        dialogRT.anchorMax = new Vector2(0.5f, 0.5f);
        dialogRT.pivot = new Vector2(0.5f, 0.5f);
        dialogRT.sizeDelta = new Vector2(900f, 680f);

        Image dialogBg = dialogGO.GetComponent<Image>();
        dialogBg.color = new Color(0.12f, 0.12f, 0.15f, 0.98f);
        dialogBg.raycastTarget = true;

        // Resolve font asset from scene if available
        TMP_FontAsset fontAsset = null;
        if (templateSliderRow != null)
        {
            var tmp = templateSliderRow.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp != null) fontAsset = tmp.font;
        }
        if (fontAsset == null)
        {
            var tmp = parent.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp != null) fontAsset = tmp.font;
        }

        // 3. Scroll View for rows (with invisible raycast Image to capture mouse scrolling anywhere)
        GameObject scrollGO = new GameObject("ScrollView", typeof(RectTransform), typeof(ScrollRect), typeof(Image));
        scrollGO.transform.SetParent(dialogGO.transform, false);

        RectTransform scrollRT = scrollGO.GetComponent<RectTransform>();
        scrollRT.anchorMin = new Vector2(0f, 0f);
        scrollRT.anchorMax = new Vector2(1f, 1f);
        scrollRT.offsetMin = new Vector2(25f, 80f); // leave 80px for bottom bar
        scrollRT.offsetMax = new Vector2(-42f, -15f); // leave room for vertical scrollbar

        Image scrollImg = scrollGO.GetComponent<Image>();
        scrollImg.color = Color.clear;
        scrollImg.raycastTarget = true; // Crucial: enables mouse wheel scrolling anywhere in the viewport

        ScrollRect sr = scrollGO.GetComponent<ScrollRect>();
        sr.horizontal = false;
        sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Clamped;
        sr.scrollSensitivity = 30f;

        // Viewport using RectMask2D: modern, high-performance 2D clipping without stencil masks or raycast-blocking Images
        GameObject viewportGO = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        viewportGO.transform.SetParent(scrollGO.transform, false);

        RectTransform vpRT = viewportGO.GetComponent<RectTransform>();
        vpRT.anchorMin = Vector2.zero;
        vpRT.anchorMax = Vector2.one;
        vpRT.offsetMin = Vector2.zero;
        vpRT.offsetMax = Vector2.zero;

        // Content container
        GameObject contentGO = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentGO.transform.SetParent(viewportGO.transform, false);

        RectTransform contentRT = contentGO.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0f, 1f);
        contentRT.anchorMax = new Vector2(1f, 1f);
        contentRT.pivot = new Vector2(0.5f, 1f);
        contentRT.sizeDelta = new Vector2(0f, 0f);

        VerticalLayoutGroup vlg = contentGO.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 15f;
        vlg.padding = new RectOffset(10, 10, 10, 35); // 35px bottom margin ensures bottom rows aren't cut off
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        ContentSizeFitter csf = contentGO.GetComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        sr.viewport = vpRT;
        sr.content = contentRT;

        // 3b. Dedicated Vertical Scrollbar
        GameObject scrollbarGO = new GameObject("VerticalScrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        scrollbarGO.transform.SetParent(dialogGO.transform, false);

        RectTransform sbRT = scrollbarGO.GetComponent<RectTransform>();
        sbRT.anchorMin = new Vector2(1f, 0f);
        sbRT.anchorMax = new Vector2(1f, 1f);
        sbRT.pivot = new Vector2(1f, 0.5f);
        sbRT.anchoredPosition = new Vector2(-15f, 32f);
        sbRT.sizeDelta = new Vector2(12f, -120f);

        Image sbTrackImg = scrollbarGO.GetComponent<Image>();
        sbTrackImg.color = new Color(0.18f, 0.18f, 0.22f, 0.65f);
        sbTrackImg.raycastTarget = true;

        Scrollbar scrollbar = scrollbarGO.GetComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        GameObject slidingArea = new GameObject("Sliding Area", typeof(RectTransform));
        slidingArea.transform.SetParent(scrollbarGO.transform, false);
        RectTransform saRT = slidingArea.GetComponent<RectTransform>();
        saRT.anchorMin = Vector2.zero;
        saRT.anchorMax = Vector2.one;
        saRT.sizeDelta = Vector2.zero;

        GameObject handleGO = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handleGO.transform.SetParent(slidingArea.transform, false);
        RectTransform handleRT = handleGO.GetComponent<RectTransform>();
        handleRT.anchorMin = Vector2.zero;
        handleRT.anchorMax = Vector2.one;
        handleRT.sizeDelta = Vector2.zero;

        Image handleImg = handleGO.GetComponent<Image>();
        handleImg.color = new Color(0.48f, 0.48f, 0.58f, 0.9f);
        handleImg.raycastTarget = true;

        scrollbar.handleRect = handleRT;
        scrollbar.targetGraphic = handleImg;

        sr.verticalScrollbar = scrollbar;
        sr.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;

        // 4. Inject Dev Category into Content!
        BuildDevCategory(contentGO.transform, templateSliderRow, overrideRowWidth: 810f);

        // 5. Bottom "Done" Button
        GameObject bottomBarGO = new GameObject("BottomBar", typeof(RectTransform));
        bottomBarGO.transform.SetParent(dialogGO.transform, false);

        RectTransform bbRT = bottomBarGO.GetComponent<RectTransform>();
        bbRT.anchorMin = new Vector2(0f, 0f);
        bbRT.anchorMax = new Vector2(1f, 0f);
        bbRT.pivot = new Vector2(0.5f, 0f);
        bbRT.anchoredPosition = new Vector2(0f, 18f);
        bbRT.sizeDelta = new Vector2(0f, 50f);

        GameObject doneBtnGO;
        if (templateButton != null)
        {
            doneBtnGO = UnityEngine.Object.Instantiate(templateButton, bottomBarGO.transform, false);
            doneBtnGO.name = "DoneBtn";
            RectTransform dRT = doneBtnGO.GetComponent<RectTransform>();
            dRT.anchorMin = new Vector2(0.5f, 0.5f);
            dRT.anchorMax = new Vector2(0.5f, 0.5f);
            dRT.pivot = new Vector2(0.5f, 0.5f);
            dRT.anchoredPosition = Vector2.zero;
            dRT.sizeDelta = new Vector2(220f, 48f);

            var btmp = doneBtnGO.GetComponentInChildren<TextMeshProUGUI>(true);
            if (btmp != null)
            {
                btmp.text = "Done";
                btmp.fontSize = 20f;
                btmp.alignment = TextAlignmentOptions.Center;
                btmp.raycastTarget = false;
            }

            // Recreate Button component to eliminate inherited persistent Quit listeners
            Button oldDoneBtn = doneBtnGO.GetComponent<Button>();
            if (oldDoneBtn != null)
            {
                var colors = oldDoneBtn.colors;
                var spriteState = oldDoneBtn.spriteState;
                var navigation = oldDoneBtn.navigation;
                var transition = oldDoneBtn.transition;

                UnityEngine.Object.DestroyImmediate(oldDoneBtn);

                Button doneBtn = doneBtnGO.AddComponent<Button>();
                doneBtn.colors = colors;
                doneBtn.spriteState = spriteState;
                doneBtn.navigation = navigation;
                doneBtn.transition = transition;

                Image dImg = doneBtnGO.GetComponent<Image>();
                if (dImg != null)
                {
                    dImg.raycastTarget = true;
                    doneBtn.targetGraphic = dImg;
                }

                doneBtn.interactable = true;
                doneBtn.onClick.AddListener(() =>
                {
                    modalGO.SetActive(false);
                });
            }
        }
        else
        {
            doneBtnGO = new GameObject("DoneBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            doneBtnGO.transform.SetParent(bottomBarGO.transform, false);

            RectTransform dRT = doneBtnGO.GetComponent<RectTransform>();
            dRT.anchorMin = new Vector2(0.5f, 0.5f);
            dRT.anchorMax = new Vector2(0.5f, 0.5f);
            dRT.pivot = new Vector2(0.5f, 0.5f);
            dRT.anchoredPosition = Vector2.zero;
            dRT.sizeDelta = new Vector2(220f, 48f);

            Image dImg = doneBtnGO.GetComponent<Image>();
            dImg.color = new Color(0.2f, 0.6f, 0.35f, 1f);
            dImg.raycastTarget = true;

            GameObject textGO = new GameObject("Text", typeof(RectTransform));
            textGO.transform.SetParent(doneBtnGO.transform, false);
            RectTransform tRT = textGO.GetComponent<RectTransform>();
            tRT.anchorMin = Vector2.zero;
            tRT.anchorMax = Vector2.one;
            tRT.sizeDelta = Vector2.zero;

            TextMeshProUGUI dTmp = textGO.AddComponent<TextMeshProUGUI>();
            if (fontAsset != null) dTmp.font = fontAsset;
            dTmp.text = "Done";
            dTmp.fontSize = 20f;
            dTmp.fontStyle = FontStyles.Bold;
            dTmp.color = Color.white;
            dTmp.alignment = TextAlignmentOptions.Center;
            dTmp.raycastTarget = false;

            Button doneBtn = doneBtnGO.GetComponent<Button>();
            doneBtn.targetGraphic = dImg;
            doneBtn.interactable = true;
            doneBtn.onClick.AddListener(() =>
            {
                modalGO.SetActive(false);
            });
        }

        return modalGO;
    }
}

/// <summary>
/// Helper attached to Sliders inside ScrollRects.
/// Disables the parent ScrollRect when the user presses or drags the slider,
/// ensuring the ScrollRect does not hijack the drag or lock up the slider.
/// </summary>
public class SliderScrollConflictFix : MonoBehaviour, IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerDownHandler, IPointerUpHandler
{
    private ScrollRect _scrollRect;

    private void Awake()
    {
        _scrollRect = GetComponentInParent<ScrollRect>();
    }

    public void OnInitializePotentialDrag(PointerEventData eventData)
    {
        eventData.useDragThreshold = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (_scrollRect != null)
            _scrollRect.enabled = false;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_scrollRect != null)
            _scrollRect.enabled = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        // Must implement IDragHandler so EventSystem treats this object hierarchy as drag consumer
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (_scrollRect != null)
            _scrollRect.enabled = true;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (_scrollRect != null)
            _scrollRect.enabled = true;
    }
}

/// <summary>
/// Helper component that invokes a cleanup callback when a UI GameObject is destroyed,
/// ensuring static event listeners are cleanly unsubscribed.
/// </summary>
public class DevUIEventListener : MonoBehaviour
{
    public Action cleanup;

    private void OnDestroy()
    {
        cleanup?.Invoke();
    }
}
