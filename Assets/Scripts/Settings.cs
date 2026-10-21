using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class Settings : MonoBehaviour
{
    [Header("Audio")]
    public AudioMixer mixer;
    public Slider master;
    public Slider sfx;
    public Slider music;

    [Header("Graphics UI")]
    public TMP_Dropdown vsyncDropdown;
    public TMP_Dropdown graphicsDropdown;

    [Header("Sensitivity UI")]
    public Slider mouseSensitivity;
    public Slider gamepadSensitivity;

    private const string KEY_FULLSCREEN = "Fullscreen";
    private const string KEY_MASTER = "MasterVolume";
    private const string KEY_SFX = "SFXVolume";
    private const string KEY_MUSIC = "MusicVolume";
    private const string KEY_MOUSE_SENSITIVITY = "MouseSensitivity";
    private const string KEY_GAMEPAD_SENSITIVITY = "GamepadSensitivity";
    private const string KEY_VSYNC = "Vsync";
    private const string KEY_QUALITY = "Quality";

    private const string MIXER_MASTER = "MasterVolume";
    private const string MIXER_SFX = "SfxVolume";
    private const string MIXER_MUSIC = "MusicVolume";

    private void Start()
    {
        LoadSettings();

        // Hook up slider listeners (important)
        if (master != null)
            master.onValueChanged.AddListener(SetMasterVolume);
        if (sfx != null)
            sfx.onValueChanged.AddListener(SetSFXVolume);
        if (music != null)
            music.onValueChanged.AddListener(SetMusicVolume);

        if (mouseSensitivity != null)
            mouseSensitivity.onValueChanged.AddListener(SetMouseSensitivity);
        if (gamepadSensitivity != null)
            gamepadSensitivity.onValueChanged.AddListener(SetGamepadSensitivity);
    }

    // ---------------- AUDIO ----------------

    private float LinearToDecibels(float linear)
    {
        return linear > 0.0001f ? Mathf.Log10(linear) * 20f : -80f;
    }

    public void SetMasterVolume(float value)
    {
        Debug.Log("Master changed to: " + value);
        if (mixer != null)
            mixer.SetFloat(MIXER_MASTER, LinearToDecibels(value));

        PlayerPrefs.SetFloat(KEY_MASTER, value);
        PlayerPrefs.Save();
    }

    public void SetSFXVolume(float value)
    {
        Debug.Log("SFX changed to: " + value);
        if (mixer != null)
            mixer.SetFloat(MIXER_SFX, LinearToDecibels(value));

        PlayerPrefs.SetFloat(KEY_SFX, value);
        PlayerPrefs.Save();
    }

    public void SetMusicVolume(float value)
    {
        Debug.Log("Music changed to: " + value);
        if (mixer != null)
            mixer.SetFloat(MIXER_MUSIC, LinearToDecibels(value));

        PlayerPrefs.SetFloat(KEY_MUSIC, value);
        PlayerPrefs.Save();
    }

    // ---------------- SENSITIVITY ----------------

    public void SetMouseSensitivity(float value)
    {
        PlayerPrefs.SetFloat(KEY_MOUSE_SENSITIVITY, value);
        PlayerPrefs.Save();
    }

    public void SetGamepadSensitivity(float value)
    {
        PlayerPrefs.SetFloat(KEY_GAMEPAD_SENSITIVITY, value);
        PlayerPrefs.Save();
    }

    // ---------------- GRAPHICS ----------------

    public void SetFullscreen(bool fullscreen)
    {
        Screen.fullScreen = fullscreen;
        PlayerPrefs.SetInt(KEY_FULLSCREEN, fullscreen ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void SetVsync(int vsyncCount)
    {
        QualitySettings.vSyncCount = vsyncCount;
        PlayerPrefs.SetInt(KEY_VSYNC, vsyncCount);
        PlayerPrefs.Save();
    }

    public void SetQuality(int quality)
    {
        QualitySettings.SetQualityLevel(quality, true);
        PlayerPrefs.SetInt(KEY_QUALITY, quality);
        PlayerPrefs.Save();
    }

    // ---------------- LOAD ----------------

    private void LoadSettings()
    {
        // Audio (default = 1 = full volume)
        float masterVal = PlayerPrefs.GetFloat(KEY_MASTER, 1f);
        float sfxVal = PlayerPrefs.GetFloat(KEY_SFX, 1f);
        float musicVal = PlayerPrefs.GetFloat(KEY_MUSIC, 1f);

        if (master != null)
            master.SetValueWithoutNotify(masterVal);
        if (sfx != null)
            sfx.SetValueWithoutNotify(sfxVal);
        if (music != null)
            music.SetValueWithoutNotify(musicVal);

        SetMasterVolume(masterVal);
        SetSFXVolume(sfxVal);
        SetMusicVolume(musicVal);

        // Sensitivity (default = 2)
        float mouseVal = PlayerPrefs.GetFloat(KEY_MOUSE_SENSITIVITY, 2f);
        float gamepadVal = PlayerPrefs.GetFloat(KEY_GAMEPAD_SENSITIVITY, 150f);

        if (mouseSensitivity != null)
            mouseSensitivity.SetValueWithoutNotify(mouseVal);
        if (gamepadSensitivity != null)
            gamepadSensitivity.SetValueWithoutNotify(gamepadVal);

        // Graphics
        int quality = PlayerPrefs.GetInt(KEY_QUALITY, QualitySettings.GetQualityLevel());
        QualitySettings.SetQualityLevel(quality, true);

        if (graphicsDropdown != null)
        {
            graphicsDropdown.SetValueWithoutNotify(quality);
            graphicsDropdown.RefreshShownValue();
        }

        // Fullscreen
        bool fullscreen = PlayerPrefs.GetInt(KEY_FULLSCREEN, Screen.fullScreen ? 1 : 0) != 0;
        Screen.fullScreen = fullscreen;

        // VSync
        int vsync = PlayerPrefs.GetInt(KEY_VSYNC, QualitySettings.vSyncCount);
        QualitySettings.vSyncCount = vsync;

        if (vsyncDropdown != null)
        {
            vsyncDropdown.SetValueWithoutNotify(vsync);
            vsyncDropdown.RefreshShownValue();
        }

        // Initialize developer-exclusive category if player has the developer badge
        TryInitDevSettingsCategory();
    }

    // ---------------- DEVELOPER EXCLUSIVE CATEGORY ----------------

    private void TryInitDevSettingsCategory()
    {
        if (PlayfabManager.IsDeveloper)
        {
            BuildDevCategory();
        }
        else
        {
            PlayfabManager.OnDeveloperStatusChanged += OnDevStatusChanged;
            if (PlayfabManager.Instance != null)
            {
                PlayfabManager.Instance.CheckDeveloperStatus();
            }
        }
    }

    private void OnDevStatusChanged(bool isDev)
    {
        if (isDev)
        {
            BuildDevCategory();
        }
        else
        {
            RemoveDevCategory();
        }
    }

    private void BuildDevCategory()
    {
        Transform content = FindSettingsContent();
        if (content != null)
        {
            GameObject template = master != null && master.transform.parent != null
                ? master.transform.parent.gameObject
                : null;
            DevSettingsUIBuilder.BuildDevCategory(content, template);
        }
    }

    private void RemoveDevCategory()
    {
        Transform content = FindSettingsContent();
        if (content != null)
        {
            var toDestroy = new System.Collections.Generic.List<GameObject>();
            foreach (Transform child in content)
            {
                if (child.name.StartsWith("Dev_") || child.name == "DevPhysicsCategory")
                {
                    toDestroy.Add(child.gameObject);
                }
            }
            foreach (var go in toDestroy)
            {
                Destroy(go);
            }
        }
    }

    private Transform FindSettingsContent()
    {
        Transform content = transform.Find("Settings/Scroll View/Viewport/Content");
        if (content != null)
            return content;

        VerticalLayoutGroup[] vlgs = GetComponentsInChildren<VerticalLayoutGroup>(true);
        foreach (var vlg in vlgs)
        {
            if (vlg.gameObject.name == "Content")
                return vlg.transform;
        }

        return null;
    }

    private void OnDestroy()
    {
        PlayfabManager.OnDeveloperStatusChanged -= OnDevStatusChanged;
    }

    private void OnDisable()
    {
        PlayerPrefs.Save();
    }

    private void OnApplicationQuit()
    {
        PlayerPrefs.Save();
    }
}

