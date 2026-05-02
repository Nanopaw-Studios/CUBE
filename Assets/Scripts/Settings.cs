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

    private void Start()
    {
        LoadSettings();

        // Hook up slider listeners (important)
        if (master != null) master.onValueChanged.AddListener(SetMasterVolume);
        if (sfx != null) sfx.onValueChanged.AddListener(SetSFXVolume);
        if (music != null) music.onValueChanged.AddListener(SetMusicVolume);

        if (mouseSensitivity != null) mouseSensitivity.onValueChanged.AddListener(SetMouseSensitivity);
        if (gamepadSensitivity != null) gamepadSensitivity.onValueChanged.AddListener(SetGamepadSensitivity);
    }

    // ---------------- AUDIO ----------------

    public void SetMasterVolume(float value)
    {
        Debug.Log("Master changed to: " + value);
        if (mixer != null)
            mixer.SetFloat("MasterVolume", value);

        PlayerPrefs.SetFloat(KEY_MASTER, value);
    }

    public void SetSFXVolume(float value) 
    {
        Debug.Log("SFX changed to: " + value);
        if (mixer != null)
            mixer.SetFloat("SFXVolume", value);

        PlayerPrefs.SetFloat(KEY_SFX, value);
    }

    public void SetMusicVolume(float value)
    {
        Debug.Log("Music changed to: " + value);
        if (mixer != null)         
            mixer.SetFloat("MusicVolume", value);

        PlayerPrefs.SetFloat(KEY_MUSIC, value);
    }

    // ---------------- SENSITIVITY ----------------

    public void SetMouseSensitivity(float value)
    {
        PlayerPrefs.SetFloat(KEY_MOUSE_SENSITIVITY, value);
    }

    public void SetGamepadSensitivity(float value)
    {
        PlayerPrefs.SetFloat(KEY_GAMEPAD_SENSITIVITY, value);
    }

    // ---------------- GRAPHICS ----------------

    public void SetFullscreen(bool fullscreen)
    {
        Screen.fullScreen = fullscreen;
        PlayerPrefs.SetInt(KEY_FULLSCREEN, fullscreen ? 1 : 0);
    }

    public void SetVsync(int vsyncCount)
    {
        QualitySettings.vSyncCount = vsyncCount;
        PlayerPrefs.SetInt(KEY_VSYNC, vsyncCount);
    }

    public void SetQuality(int quality)
    {
        QualitySettings.SetQualityLevel(quality, true);
        PlayerPrefs.SetInt(KEY_QUALITY, quality);
    }

    // ---------------- LOAD ----------------

    private void LoadSettings()
    {
        // Audio (default = 1 = full volume)
        float masterVal = PlayerPrefs.GetFloat(KEY_MASTER, 1f);
        float sfxVal = PlayerPrefs.GetFloat(KEY_SFX, 1f);
        float musicVal = PlayerPrefs.GetFloat(KEY_MUSIC, 1f);

        if (master != null) master.SetValueWithoutNotify(masterVal);
        if (sfx != null) sfx.SetValueWithoutNotify(sfxVal);
        if (music != null) music.SetValueWithoutNotify(musicVal);

        SetMasterVolume(masterVal);
        SetSFXVolume(sfxVal);
        SetMusicVolume(musicVal);

        // Sensitivity
        float mouseVal = PlayerPrefs.GetFloat(KEY_MOUSE_SENSITIVITY, 20f);
        float gamepadVal = PlayerPrefs.GetFloat(KEY_GAMEPAD_SENSITIVITY, 100f);

        if (mouseSensitivity != null) mouseSensitivity.SetValueWithoutNotify(mouseVal);
        if (gamepadSensitivity != null) gamepadSensitivity.SetValueWithoutNotify(gamepadVal);

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
    }

    private void OnApplicationQuit()
    {
        PlayerPrefs.Save();
    }
}