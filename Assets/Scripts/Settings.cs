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

    // New, corrected keys
    private const string KEY_FULLSCREEN = "Fullscreen";
    private const string KEY_MASTER = "MasterVolume";
    private const string KEY_SFX = "SFXVolume";
    private const string KEY_MUSIC = "MusicVolume";
    private const string KEY_MOUSE_SENSITIVITY = "MouseSensitivity";
    private const string KEY_GAMEPAD_SENSITIVITY = "GamepadSensitivity";
    private const string KEY_VSYNC = "Vsync";
    private const string KEY_QUALITY = "Quality";

    // Legacy typo keys, kept for migration
    private const string OLD_KEY_MOUSE_SENSITIVITY = "MouseSensivity";
    private const string OLD_KEY_GAMEPAD_SENSITIVITY = "GamepadSensivity";

    private void Start()
    {
        LoadSettings();
    }

    public void SetFullscreen(bool fullscreen)
    {
        Screen.fullScreen = fullscreen;
        PlayerPrefs.SetInt(KEY_FULLSCREEN, fullscreen ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void SetMasterVolume(float volume)
    {
        if (mixer != null)
            mixer.SetFloat("masterVolume", volume);

        PlayerPrefs.SetFloat(KEY_MASTER, volume);
        PlayerPrefs.Save();
    }

    public void SetSFXVolume(float volume)
    {
        if (mixer != null)
            mixer.SetFloat("sfxVolume", volume);

        PlayerPrefs.SetFloat(KEY_SFX, volume);
        PlayerPrefs.Save();
    }

    public void SetMusicVolume(float volume)
    {
        if (mixer != null)
            mixer.SetFloat("musicVolume", volume);

        PlayerPrefs.SetFloat(KEY_MUSIC, volume);
        PlayerPrefs.Save();
    }

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

    public void SaveSettings()
    {
        PlayerPrefs.Save();
    }

    private void LoadSettings()
    {
        // Audio — default to -10 dB (near-max, not clamped at maximum)
        float masterVol = LoadFloatWithMigration(KEY_MASTER, null, -10f);
        if (mixer != null) mixer.SetFloat("masterVolume", masterVol);
        if (master != null) master.SetValueWithoutNotify(masterVol);

        float sfxVol = LoadFloatWithMigration(KEY_SFX, null, -10f);
        if (mixer != null) mixer.SetFloat("sfxVolume", sfxVol);
        if (sfx != null) sfx.SetValueWithoutNotify(sfxVol);

        float musicVol = LoadFloatWithMigration(KEY_MUSIC, null, -10f);
        if (mixer != null) mixer.SetFloat("musicVolume", musicVol);
        if (music != null) music.SetValueWithoutNotify(musicVol);

        // Sensitivity
        float savedMouseSens = LoadFloatWithMigration(KEY_MOUSE_SENSITIVITY, OLD_KEY_MOUSE_SENSITIVITY, 20f);
        if (mouseSensitivity != null) mouseSensitivity.SetValueWithoutNotify(savedMouseSens);

        float savedGamepadSens = LoadFloatWithMigration(KEY_GAMEPAD_SENSITIVITY, OLD_KEY_GAMEPAD_SENSITIVITY, 100f);
        if (gamepadSensitivity != null) gamepadSensitivity.SetValueWithoutNotify(savedGamepadSens);

        // Quality
        int quality = LoadIntWithMigration(KEY_QUALITY, null, QualitySettings.GetQualityLevel());
        QualitySettings.SetQualityLevel(quality, true);
        if (graphicsDropdown != null)
        {
            graphicsDropdown.SetValueWithoutNotify(quality);
            graphicsDropdown.RefreshShownValue();
        }

        // Fullscreen
        bool fullscreen = LoadIntWithMigration(KEY_FULLSCREEN, null, Screen.fullScreen ? 1 : 0) != 0;
        Screen.fullScreen = fullscreen;

        // VSync
        int vsync = LoadIntWithMigration(KEY_VSYNC, null, QualitySettings.vSyncCount);
        QualitySettings.vSyncCount = vsync;
        if (vsyncDropdown != null)
        {
            vsyncDropdown.SetValueWithoutNotify(vsync);
            vsyncDropdown.RefreshShownValue();
        }

        // Make sure defaults / migrated values are actually written out
        PlayerPrefs.Save();
    }

    private float LoadFloatWithMigration(string key, string oldKey, float defaultValue)
    {
        if (PlayerPrefs.HasKey(key))
            return PlayerPrefs.GetFloat(key);

        if (!string.IsNullOrEmpty(oldKey) && PlayerPrefs.HasKey(oldKey))
        {
            float migrated = PlayerPrefs.GetFloat(oldKey);
            PlayerPrefs.SetFloat(key, migrated);
            return migrated;
        }

        PlayerPrefs.SetFloat(key, defaultValue);
        return defaultValue;
    }

    private int LoadIntWithMigration(string key, string oldKey, int defaultValue)
    {
        if (PlayerPrefs.HasKey(key))
            return PlayerPrefs.GetInt(key);

        if (!string.IsNullOrEmpty(oldKey) && PlayerPrefs.HasKey(oldKey))
        {
            int migrated = PlayerPrefs.GetInt(oldKey);
            PlayerPrefs.SetInt(key, migrated);
            return migrated;
        }

        PlayerPrefs.SetInt(key, defaultValue);
        return defaultValue;
    }
}