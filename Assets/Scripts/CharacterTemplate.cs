using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CharacterTemplate : MonoBehaviour
{
    public CubePlayerBase AssociatedCharacter;

    public Image image;
    public TMP_Text CharName;

    private CubePlayerBase _lastCharacter;
    private int _lastLevel = -1;
    private string _lastLang;

    private void Update()
    {
        if (AssociatedCharacter == null)
            return;

        int lvl = ProgressionManager.Instance != null
            ? ProgressionManager.Instance.GetCharacterMasteryLevel(AssociatedCharacter._Name)
            : 1;
        string lang = LocalizationManager.Instance != null ? LocalizationManager.Instance.CurrentLanguageCode : "en";

        if (_lastCharacter != AssociatedCharacter || _lastLevel != lvl || _lastLang != lang)
        {
            _lastCharacter = AssociatedCharacter;
            _lastLevel = lvl;
            _lastLang = lang;

            if (image != null)
                image.sprite = AssociatedCharacter.image;

            if (CharName != null)
            {
                string displayName = LocalizationManager.GetCharacterName(AssociatedCharacter);
                CharName.text = $"{displayName} <size=75%><color=#FFD700>[LVL {lvl}]</color></size>";
            }
        }
    }

}

