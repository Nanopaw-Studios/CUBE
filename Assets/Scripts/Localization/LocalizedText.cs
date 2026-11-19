using TMPro;
using UnityEngine;

/// <summary>
/// Attached to static UI TMP_Text components. Automatically translates the text using
/// LocalizationManager and updates immediately whenever the active language changes.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class LocalizedText : MonoBehaviour
{
    [Tooltip("Localization key in the GitHub JSON table (e.g. 'menu.play', 'shop.title').")]
    public string localizationKey;

    [Tooltip("Optional override for the fallback English text. If empty, uses the initial TMP_Text value.")]
    [TextArea(1, 3)]
    public string fallbackText;

    private TMP_Text _text;
    private bool _cachedInitial;

    private void Awake()
    {
        CacheTextComponent();
    }

    private void OnEnable()
    {
        CacheTextComponent();
        LocalizationManager.OnLanguageChanged += RefreshText;
        RefreshText();
    }

    private void OnDisable()
    {
        LocalizationManager.OnLanguageChanged -= RefreshText;
    }

    private void CacheTextComponent()
    {
        if (_text == null)
            _text = GetComponent<TMP_Text>();

        if (!_cachedInitial && _text != null)
        {
            _cachedInitial = true;
            if (string.IsNullOrEmpty(fallbackText))
                fallbackText = _text.text;
        }
    }

    public void RefreshText()
    {
        CacheTextComponent();
        if (_text == null || string.IsNullOrEmpty(localizationKey))
            return;

        _text.text = LocalizationManager.Get(localizationKey, fallbackText);
    }
}
