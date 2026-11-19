using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

[Serializable]
public class LocalizationLanguageInfo
{
    public string code = "en";
    public string name = "English";
    public string nativeName = "English";
    public string author = "NanoDogs Studios";
    public string file = "en.json";
}

[Serializable]
public class LocalizationManifest
{
    public string defaultLanguage = "en";
    public List<LocalizationLanguageInfo> languages = new List<LocalizationLanguageInfo>();
}

[Serializable]
public class LocalizationEntry
{
    public string key;
    public string value;
}

[Serializable]
public class LocalizationEntryListWrapper
{
    public List<LocalizationEntry> entries = new List<LocalizationEntry>();
}

/// <summary>
/// GitHub-hosted JSON Localization Manager with local disk caching and bundled Resources fallback.
/// Fetches `manifest.json` and `{lang}.json` from the configured GitHub repository so community
/// contributors can submit PRs to add or update languages without requiring a new client build.
/// </summary>
public class LocalizationManager : MonoBehaviour
{
    public static LocalizationManager Instance { get; private set; }

    public const string PlayerPrefsKey = "Language";
    public const string DefaultLanguageCode = "en";

    [Header("GitHub Repository Configuration")]
    [Tooltip("Base raw URL of the GitHub folder containing manifest.json and <lang>.json files.")]
    public string githubRawBaseUrl =
        "https://raw.githubusercontent.com/NanoDogs-Studios/CUBE/refs/heads/main/localization";

    [Tooltip("Automatically fetch the latest manifest.json and active language JSON from GitHub on startup.")]
    public bool fetchFromGitHubOnStart = true;

    [Tooltip("Request timeout in seconds for GitHub raw fetches.")]
    public int requestTimeoutSeconds = 8;

    public static event Action OnLanguageChanged;
    public static event Action OnManifestUpdated;

    public string CurrentLanguageCode { get; private set; } = DefaultLanguageCode;

    private readonly List<LocalizationLanguageInfo> _availableLanguages = new List<LocalizationLanguageInfo>();
    public IReadOnlyList<LocalizationLanguageInfo> AvailableLanguages => _availableLanguages;

    private readonly Dictionary<string, string> _englishFallbackTable =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, string> _activeTable =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private bool _initialized;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoBootstrap()
    {
        EnsureExists();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
        OnLanguageChanged = null;
        OnManifestUpdated = null;
    }

    public static LocalizationManager EnsureExists()
    {
        if (Instance != null)
            return Instance;

        LocalizationManager existing = FindFirstObjectByType<LocalizationManager>();
        if (existing != null)
        {
            Instance = existing;
            existing.InitializeIfNeeded();
            return Instance;
        }

        GameObject servicesGO = GameObject.Find("[Services]");
        if (servicesGO != null)
        {
            Instance = servicesGO.AddComponent<LocalizationManager>();
            Instance.InitializeIfNeeded();
            return Instance;
        }

        GameObject go = new GameObject("[LocalizationManager]");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<LocalizationManager>();
        Instance.InitializeIfNeeded();
        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        if (transform.parent == null)
            DontDestroyOnLoad(gameObject);

        InitializeIfNeeded();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void InitializeIfNeeded()
    {
        if (_initialized)
            return;

        _initialized = true;

        // 1. Load bundled/cached manifest synchronously so language list is immediately populated
        LoadManifestLocalOrBundled();

        // 2. Load bundled/cached English fallback table
        LoadLanguageTableLocalOrBundled(DefaultLanguageCode, _englishFallbackTable);

        // 3. Determine saved or system language
        string savedLang = PlayerPrefs.GetString(PlayerPrefsKey, "");
        if (string.IsNullOrEmpty(savedLang))
        {
            savedLang = DetectSystemLanguageCode();
        }
        if (!IsLanguageSupported(savedLang))
        {
            savedLang = DefaultLanguageCode;
        }

        CurrentLanguageCode = savedLang;
        LoadLanguageTableLocalOrBundled(CurrentLanguageCode, _activeTable);
        OnLanguageChanged?.Invoke();

        // 4. Fetch latest manifest & active language JSON from GitHub asynchronously
        if (fetchFromGitHubOnStart)
        {
            StartCoroutine(RefreshFromGitHubCoroutine());
        }
    }

    // ── Public Static Lookup API ────────────────────────────────────────

    public static string Get(string key, string fallback = null)
    {
        if (string.IsNullOrEmpty(key))
            return fallback ?? string.Empty;

        LocalizationManager mgr = EnsureExists();
        if (mgr != null)
        {
            if (mgr._activeTable.TryGetValue(key, out string val) && !string.IsNullOrEmpty(val))
                return val;

            if (mgr._englishFallbackTable.TryGetValue(key, out string enVal) && !string.IsNullOrEmpty(enVal))
                return enVal;
        }

        return fallback ?? key;
    }

    public static string Format(string key, string fallbackFormat, params object[] args)
    {
        string pattern = Get(key, fallbackFormat);
        if (args == null || args.Length == 0)
            return pattern;

        try
        {
            return string.Format(pattern, args);
        }
        catch (FormatException)
        {
            return pattern;
        }
    }

    public static string ToSlug(string rawName)
    {
        if (string.IsNullOrEmpty(rawName))
            return "unknown";

        var sb = new StringBuilder(rawName.Length);
        for (int i = 0; i < rawName.Length; i++)
        {
            char c = char.ToLowerInvariant(rawName[i]);
            if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
                sb.Append(c);
            else if (c == ' ' || c == '-' || c == '_')
                sb.Append('_');
        }
        return sb.ToString().Trim('_');
    }

    /// <summary>
    /// Returns the localized display name of a character without mutating its internal _Name ID.
    /// </summary>
    public static string GetCharacterName(CubePlayerBase character)
    {
        if (character == null)
            return string.Empty;
        string slug = ToSlug(character._Name);
        return Get($"char.{slug}.name", character._Name);
    }

    public static string GetCharacterDescription(CubePlayerBase character)
    {
        if (character == null)
            return string.Empty;
        string slug = ToSlug(character._Name);
        return Get($"char.{slug}.desc", character.Description);
    }

    public static string GetAbilityName(Ability ability)
    {
        if (ability == null)
            return string.Empty;
        string slug = ToSlug(ability.AbilityName);
        return Get($"ability.{slug}.name", ability.AbilityName);
    }

    public static string GetAchievementTitle(CUBE.Achievements.AchievementDefinition def)
    {
        if (def == null)
            return string.Empty;
        return Get($"ach.{def.AchievementId}.title", def.DisplayName);
    }

    public static string GetAchievementDescription(CUBE.Achievements.AchievementDefinition def)
    {
        if (def == null)
            return string.Empty;
        return Get($"ach.{def.AchievementId}.desc", def.Description);
    }

    // ── Language Switching ──────────────────────────────────────────────

    public bool IsLanguageSupported(string code)
    {
        if (string.IsNullOrEmpty(code))
            return false;

        for (int i = 0; i < _availableLanguages.Count; i++)
        {
            if (string.Equals(_availableLanguages[i].code, code, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public int GetLanguageIndex(string code)
    {
        for (int i = 0; i < _availableLanguages.Count; i++)
        {
            if (string.Equals(_availableLanguages[i].code, code, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return 0;
    }

    public void SetLanguageByIndex(int index)
    {
        if (index < 0 || index >= _availableLanguages.Count)
            return;

        SetLanguage(_availableLanguages[index].code);
    }

    public void SetLanguage(string languageCode)
    {
        if (string.IsNullOrEmpty(languageCode))
            return;

        CurrentLanguageCode = languageCode;
        PlayerPrefs.SetString(PlayerPrefsKey, CurrentLanguageCode);
        PlayerPrefs.Save();

        // Immediately load local/bundled table for zero-latency switch
        LoadLanguageTableLocalOrBundled(CurrentLanguageCode, _activeTable);
        OnLanguageChanged?.Invoke();

        // Also fetch latest version of this language from GitHub in the background
        if (fetchFromGitHubOnStart && isActiveAndEnabled)
        {
            StartCoroutine(FetchLanguageFromGitHubCoroutine(CurrentLanguageCode, notifyOnSuccess: true));
        }

        Debug.Log($"[Localization] Switched active language to '{CurrentLanguageCode}' ({_activeTable.Count} keys).");
    }

    // ── GitHub Remote Sync & Local Caching ──────────────────────────────

    public void RefreshFromGitHub()
    {
        if (isActiveAndEnabled)
            StartCoroutine(RefreshFromGitHubCoroutine());
    }

    private IEnumerator RefreshFromGitHubCoroutine()
    {
        if (string.IsNullOrWhiteSpace(githubRawBaseUrl))
            yield break;

        string baseUrl = githubRawBaseUrl.TrimEnd('/');
        string manifestUrl = $"{baseUrl}/manifest.json";

        using (UnityWebRequest req = UnityWebRequest.Get(manifestUrl))
        {
            req.timeout = requestTimeoutSeconds;
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success && !string.IsNullOrWhiteSpace(req.downloadHandler.text))
            {
                string json = req.downloadHandler.text;
                if (TryParseAndApplyManifest(json))
                {
                    SaveCachedFile("manifest.json", json);
                    OnManifestUpdated?.Invoke();
                    Debug.Log($"[Localization] Synced manifest.json from GitHub ({_availableLanguages.Count} languages).");
                }
            }
        }

        // Refresh English fallback table from GitHub
        yield return FetchLanguageFromGitHubCoroutine(DefaultLanguageCode, notifyOnSuccess: false);

        // Refresh active language table if different from English
        if (!string.Equals(CurrentLanguageCode, DefaultLanguageCode, StringComparison.OrdinalIgnoreCase))
        {
            yield return FetchLanguageFromGitHubCoroutine(CurrentLanguageCode, notifyOnSuccess: true);
        }
        else
        {
            LoadLanguageTableLocalOrBundled(DefaultLanguageCode, _activeTable);
            OnLanguageChanged?.Invoke();
        }
    }

    private IEnumerator FetchLanguageFromGitHubCoroutine(string langCode, bool notifyOnSuccess)
    {
        if (string.IsNullOrWhiteSpace(githubRawBaseUrl) || string.IsNullOrWhiteSpace(langCode))
            yield break;

        string fileName = $"{langCode}.json";
        for (int i = 0; i < _availableLanguages.Count; i++)
        {
            if (string.Equals(_availableLanguages[i].code, langCode, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrEmpty(_availableLanguages[i].file))
            {
                fileName = _availableLanguages[i].file;
                break;
            }
        }

        string url = $"{githubRawBaseUrl.TrimEnd('/')}/{fileName}";
        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            req.timeout = requestTimeoutSeconds;
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success && !string.IsNullOrWhiteSpace(req.downloadHandler.text))
            {
                string json = req.downloadHandler.text;
                var parsed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                ParseLanguageJsonIntoDictionary(json, parsed);

                if (parsed.Count > 0)
                {
                    SaveCachedFile(fileName, json);

                    if (string.Equals(langCode, DefaultLanguageCode, StringComparison.OrdinalIgnoreCase))
                    {
                        _englishFallbackTable.Clear();
                        foreach (var kvp in parsed)
                            _englishFallbackTable[kvp.Key] = kvp.Value;
                    }

                    if (string.Equals(langCode, CurrentLanguageCode, StringComparison.OrdinalIgnoreCase))
                    {
                        _activeTable.Clear();
                        foreach (var kvp in parsed)
                            _activeTable[kvp.Key] = kvp.Value;

                        if (notifyOnSuccess)
                            OnLanguageChanged?.Invoke();
                    }

                    Debug.Log($"[Localization] Synced '{fileName}' from GitHub ({parsed.Count} keys).");
                }
            }
        }
    }

    // ── Local Cache & Bundled Resources Loading ─────────────────────────

    private void LoadManifestLocalOrBundled()
    {
        string cached = LoadCachedFile("manifest.json");
        if (!string.IsNullOrEmpty(cached) && TryParseAndApplyManifest(cached))
            return;

        TextAsset bundled = Resources.Load<TextAsset>("Localization/manifest");
        if (bundled != null && !string.IsNullOrEmpty(bundled.text) && TryParseAndApplyManifest(bundled.text))
            return;

        // Default built-in language roster if no manifest file exists
        _availableLanguages.Clear();
        _availableLanguages.Add(new LocalizationLanguageInfo { code = "en", name = "English", nativeName = "English", file = "en.json" });
        _availableLanguages.Add(new LocalizationLanguageInfo { code = "es", name = "Spanish", nativeName = "Español", file = "es.json" });
        _availableLanguages.Add(new LocalizationLanguageInfo { code = "fr", name = "French", nativeName = "Français", file = "fr.json" });
        _availableLanguages.Add(new LocalizationLanguageInfo { code = "de", name = "German", nativeName = "Deutsch", file = "de.json" });
        _availableLanguages.Add(new LocalizationLanguageInfo { code = "pt-BR", name = "Portuguese (Brazil)", nativeName = "Português (Brasil)", file = "pt-BR.json" });
        _availableLanguages.Add(new LocalizationLanguageInfo { code = "ru", name = "Russian", nativeName = "Русский", file = "ru.json" });
    }

    private bool TryParseAndApplyManifest(string json)
    {
        try
        {
            var manifest = JsonUtility.FromJson<LocalizationManifest>(json);
            if (manifest != null && manifest.languages != null && manifest.languages.Count > 0)
            {
                _availableLanguages.Clear();
                _availableLanguages.AddRange(manifest.languages);
                return true;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Localization] Failed to parse manifest.json: {ex.Message}");
        }
        return false;
    }

    private void LoadLanguageTableLocalOrBundled(string langCode, Dictionary<string, string> targetDict)
    {
        targetDict.Clear();

        string fileName = $"{langCode}.json";
        for (int i = 0; i < _availableLanguages.Count; i++)
        {
            if (string.Equals(_availableLanguages[i].code, langCode, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrEmpty(_availableLanguages[i].file))
            {
                fileName = _availableLanguages[i].file;
                break;
            }
        }

        // First load bundled Resources/<langCode> as baseline
        string resourceName = Path.GetFileNameWithoutExtension(fileName);
        TextAsset bundled = Resources.Load<TextAsset>($"Localization/{resourceName}");
        if (bundled != null && !string.IsNullOrEmpty(bundled.text))
        {
            ParseLanguageJsonIntoDictionary(bundled.text, targetDict);
        }

        // Then overlay cached GitHub file if present
        string cached = LoadCachedFile(fileName);
        if (!string.IsNullOrEmpty(cached))
        {
            ParseLanguageJsonIntoDictionary(cached, targetDict);
        }
    }

    private static string GetCacheDir()
    {
        return Path.Combine(Application.persistentDataPath, "Localization");
    }

    private static string LoadCachedFile(string fileName)
    {
        try
        {
            string path = Path.Combine(GetCacheDir(), fileName);
            if (File.Exists(path))
                return File.ReadAllText(path, Encoding.UTF8);
        }
        catch (Exception) { }
        return null;
    }

    private static void SaveCachedFile(string fileName, string content)
    {
        try
        {
            string dir = GetCacheDir();
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string path = Path.Combine(dir, fileName);
            File.WriteAllText(path, content, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Localization] Failed to cache '{fileName}': {ex.Message}");
        }
    }

    private static string DetectSystemLanguageCode()
    {
        return Application.systemLanguage switch
        {
            SystemLanguage.Spanish => "es",
            SystemLanguage.French => "fr",
            SystemLanguage.German => "de",
            SystemLanguage.Portuguese => "pt-BR",
            SystemLanguage.Russian => "ru",
            _ => DefaultLanguageCode
        };
    }

    // ── Flat JSON & Entry List Parser ───────────────────────────────────

    /// <summary>
    /// Supports both standard flat JSON objects:
    ///   { "menu.play": "Play", "menu.quit": "Quit" }
    /// and Unity JsonUtility entry lists:
    ///   { "entries": [ { "key": "menu.play", "value": "Play" } ] }
    /// </summary>
    public static void ParseLanguageJsonIntoDictionary(string json, Dictionary<string, string> dict)
    {
        if (string.IsNullOrWhiteSpace(json) || dict == null)
            return;

        string trimmed = json.Trim().TrimStart('\uFEFF');

        // Check if structured as { "entries": [ ... ] }
        if (trimmed.Contains("\"entries\""))
        {
            try
            {
                var wrapper = JsonUtility.FromJson<LocalizationEntryListWrapper>(trimmed);
                if (wrapper != null && wrapper.entries != null && wrapper.entries.Count > 0)
                {
                    for (int i = 0; i < wrapper.entries.Count; i++)
                    {
                        var e = wrapper.entries[i];
                        if (e != null && !string.IsNullOrEmpty(e.key))
                            dict[e.key] = e.value ?? string.Empty;
                    }
                    return;
                }
            }
            catch (Exception) { }
        }

        // Parse flat JSON object { "key": "value", ... }
        int iPos = 0;
        int len = trimmed.Length;

        while (iPos < len)
        {
            // Find opening quote of key
            int keyStart = FindNextUnescapedQuote(trimmed, iPos);
            if (keyStart < 0)
                break;

            string key = ReadJsonString(trimmed, keyStart + 1, out int afterKeyQuote);
            if (afterKeyQuote < 0)
                break;

            // Find colon ':'
            int colon = trimmed.IndexOf(':', afterKeyQuote);
            if (colon < 0)
                break;

            // Find opening quote of value
            int valStart = FindNextUnescapedQuote(trimmed, colon + 1);
            if (valStart < 0)
                break;

            string val = ReadJsonString(trimmed, valStart + 1, out int afterValQuote);
            if (afterValQuote < 0)
                break;

            if (!string.IsNullOrEmpty(key) && !key.StartsWith("_comment", StringComparison.OrdinalIgnoreCase))
            {
                dict[key] = val;
            }

            iPos = afterValQuote + 1;
        }
    }

    private static int FindNextUnescapedQuote(string s, int startIndex)
    {
        for (int i = startIndex; i < s.Length; i++)
        {
            if (s[i] == '"')
                return i;
        }
        return -1;
    }

    private static string ReadJsonString(string s, int startAfterQuote, out int closingQuoteIndex)
    {
        var sb = new StringBuilder();
        closingQuoteIndex = -1;

        for (int i = startAfterQuote; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '\\' && i + 1 < s.Length)
            {
                char next = s[++i];
                switch (next)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    default: sb.Append(next); break;
                }
            }
            else if (c == '"')
            {
                closingQuoteIndex = i;
                return sb.ToString();
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }
}
