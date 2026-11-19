using System;
using System.Collections.Generic;
using System.IO;
using CUBE.Achievements;
using ExitGames.Client.Photon;
using Photon.Pun;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

[Serializable]
public class CharacterMasteryEntry
{
    public string CharacterName;
    public int Level = 1;
    public int CurrentXP = 0;
    public int TotalXP = 0;

    public CharacterMasteryEntry() { }

    public CharacterMasteryEntry(string characterName)
    {
        CharacterName = characterName;
        Level = 1;
        CurrentXP = 0;
        TotalXP = 0;
    }
}

[Serializable]
public class CharacterMasteryCosmeticEntry
{
    public string CharacterName;

    // -1 = automatic (highest unlocked tier), 0 = original look, 1-4 = a specific mastery skin.
    public int Tier = -1;

    public CharacterMasteryCosmeticEntry() { }

    public CharacterMasteryCosmeticEntry(string characterName, int tier)
    {
        CharacterName = characterName;
        Tier = tier;
    }
}

[Serializable]
public class ProgressionSaveData
{
    public int Version = 1;
    public string PlayerId;
    public int AccountLevel = 1;
    public int AccountXP = 0;
    public int TotalAccountXP = 0;
    public List<CharacterMasteryEntry> CharacterMasteries = new List<CharacterMasteryEntry>();
    public List<CharacterMasteryCosmeticEntry> CharacterMasteryCosmetics =
        new List<CharacterMasteryCosmeticEntry>();
}

/// <summary>
/// Persistent Meta-Progression Service managing Account Levelling & XP and per-character Mastery Levels & XP.
/// Saves locally to JSON and synchronizes with PlayFab UserData and PlayFab Player Statistics.
/// </summary>
public class ProgressionManager : MonoBehaviourPunCallbacks
{
    public static ProgressionManager Instance { get; private set; }

    public const string PlayFabProgressionKey = "CUBE_Progression";
    public const string PropAccountLevel = "lvl";
    public const int MaxMasteryLevel = 100;

    // Cosmetic tiers unlocked by character mastery.
    public const int MaxMasteryCosmeticTier = 3;
    public const int MasteryCosmeticTier1Level = 25;
    public const int MasteryCosmeticTier2Level = 50;
    public const int MasteryCosmeticTier3Level = 100;

    /// <summary>No explicit skin choice: use the highest mastery cosmetic unlocked.</summary>
    public const int MasteryCosmeticTierAuto = -1;

    private const string SaveFolderName = "CUBEAchievements";
    private const string SaveFileSuffix = "_progression.json";

    // Account Level XP curve constants
    private const int BaseAccountXP = 500;
    private const int AccountXPStepPerLevel = 150;
    private const int MaxAccountXPPerLevel = 5000;

    // Account Level-Up CC Rewards
    private const int AccountLevelUpBaseCC = 50;
    private const int AccountLevelUpMilestone5BonusCC = 50;
    private const int AccountLevelUpMilestone10BonusCC = 150;

    // Character Mastery XP curve constants
    private const int BaseMasteryXP = 250;
    private const int MasteryXPStepPerLevel = 25;
    private const int MaxMasteryXPPerLevel = 2500;

    // Character Mastery Level-Up CC Rewards
    private const int MasteryLevelUpBaseCC = 25;
    private const int MasteryMilestoneBonusCC = 75;
    private const int MasteryMaxLevelBonusCC = 475;

    public int AccountLevel { get; private set; } = 1;
    public int AccountXP { get; private set; } = 0;
    public int TotalAccountXP { get; private set; } = 0;

    public int AccountXPForNextLevel => GetAccountXPRequiredForLevel(AccountLevel);
    public float AccountLevelProgress01 =>
        AccountXPForNextLevel > 0 ? Mathf.Clamp01((float)AccountXP / AccountXPForNextLevel) : 1f;

    public event Action<int, int, int> OnAccountXPChanged; // (level, currentXP, requiredXP)
    public event Action<int, int, int> OnAccountLevelUp; // (oldLevel, newLevel, ccReward)
    public event Action<string, int, int, int> OnCharacterMasteryChanged; // (charName, level, currentXP, requiredXP)
    public event Action<string, int, int, int> OnCharacterMasteryLevelUp; // (charName, oldLevel, newLevel, ccReward)
    public event Action OnProgressionSynced;

    private readonly Dictionary<string, CharacterMasteryEntry> _masteryMap =
        new Dictionary<string, CharacterMasteryEntry>(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, CharacterMasteryCosmeticEntry> _cosmeticSelectionMap =
        new Dictionary<string, CharacterMasteryCosmeticEntry>(StringComparer.OrdinalIgnoreCase);

    private ProgressionSaveData _saveData;
    private string _playerId;
    private bool _initialized;
    private bool _dirty;
    private float _autoSaveTimer;
    private const float AutoSaveInterval = 20f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoBootstrap()
    {
        EnsureExists();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    public static ProgressionManager EnsureExists()
    {
        if (Instance != null)
            return Instance;

        ProgressionManager existing = FindFirstObjectByType<ProgressionManager>();
        if (existing != null)
        {
            Instance = existing;
            return Instance;
        }

        GameObject servicesGO = GameObject.Find("[Services]");
        if (servicesGO != null)
        {
            Instance = servicesGO.AddComponent<ProgressionManager>();
            return Instance;
        }

        GameObject go = new GameObject("[ProgressionManager]");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<ProgressionManager>();
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

        Initialize();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            if (PlayfabManager.Instance != null)
                PlayfabManager.Instance.OnLoginSuccessEvent -= OnPlayFabLoginSuccess;

            if (_dirty)
                SaveProgression(syncCloud: true);

            Instance = null;
        }
    }


    private void Update()
    {
        if (!_initialized || !_dirty)
            return;

        _autoSaveTimer += Time.unscaledDeltaTime;
        if (_autoSaveTimer >= AutoSaveInterval)
        {
            _autoSaveTimer = 0f;
            SaveProgression(syncCloud: true);
        }
    }

    public override void OnJoinedRoom()
    {
        SyncPhotonProperties();
    }

    private void Initialize()
    {
        if (_initialized)
            return;

        _playerId = PlayfabManager.GetPlayerName() ?? SystemInfo.deviceUniqueIdentifier;
        _saveData = LoadLocal(_playerId);
        ApplySaveDataToRuntime(_saveData);
        EnsureAllDatabaseCharactersRegistered();

        if (PlayfabManager.Instance != null)
        {
            PlayfabManager.Instance.OnLoginSuccessEvent += OnPlayFabLoginSuccess;
            if (PlayfabManager.Instance.IsLoggedIn)
            {
                OnPlayFabLoginSuccess();
            }
        }

        _initialized = true;
        NotifyAllProgressionListeners();
    }

    private void EnsureAllDatabaseCharactersRegistered()
    {
        var allChars = CharacterDatabase.GetAllCharacters();
        for (int i = 0; i < allChars.Count; i++)
        {
            var c = allChars[i];
            if (c == null || string.IsNullOrEmpty(c._Name))
                continue;

            if (!_masteryMap.ContainsKey(c._Name))
            {
                _masteryMap[c._Name] = new CharacterMasteryEntry(c._Name);
            }
        }
    }

    // ── Account Level & XP Math ─────────────────────────────────────────

    public static int GetAccountXPRequiredForLevel(int level)
    {
        int clampedLevel = Mathf.Max(1, level);
        return Mathf.Min(MaxAccountXPPerLevel, BaseAccountXP + (clampedLevel - 1) * AccountXPStepPerLevel);
    }

    public static int GetTotalAccountXPForLevel(int targetLevel)
    {
        int total = 0;
        for (int lvl = 1; lvl < targetLevel; lvl++)
        {
            total += GetAccountXPRequiredForLevel(lvl);
        }
        return total;
    }

    public static void CalculateAccountLevelFromTotalXP(int totalXP, out int level, out int currentLevelXP)
    {
        totalXP = Mathf.Max(0, totalXP);
        level = 1;
        int remaining = totalXP;

        while (true)
        {
            int req = GetAccountXPRequiredForLevel(level);
            if (remaining >= req)
            {
                remaining -= req;
                level++;
            }
            else
            {
                break;
            }
        }

        currentLevelXP = remaining;
    }

    public static int CalculateAccountLevelUpCCReward(int oldLevel, int newLevel)
    {
        int totalCC = 0;
        for (int lvl = oldLevel + 1; lvl <= newLevel; lvl++)
        {
            totalCC += AccountLevelUpBaseCC;
            if (lvl % 5 == 0)
                totalCC += AccountLevelUpMilestone5BonusCC;
            if (lvl % 10 == 0)
                totalCC += AccountLevelUpMilestone10BonusCC;
        }
        return totalCC;
    }

    // ── Character Mastery Math ──────────────────────────────────────────

    public static int GetMasteryXPRequiredForLevel(int level)
    {
        if (level >= MaxMasteryLevel)
            return 0;

        int clampedLevel = Mathf.Max(1, level);
        return Mathf.Min(MaxMasteryXPPerLevel, BaseMasteryXP + (clampedLevel - 1) * MasteryXPStepPerLevel);
    }

    public static int GetTotalMasteryXPForLevel(int targetLevel)
    {
        int clampedTarget = Mathf.Clamp(targetLevel, 1, MaxMasteryLevel);
        int total = 0;
        for (int lvl = 1; lvl < clampedTarget; lvl++)
        {
            total += GetMasteryXPRequiredForLevel(lvl);
        }
        return total;
    }

    public static void CalculateMasteryLevelFromTotalXP(int totalXP, out int level, out int currentLevelXP)
    {
        totalXP = Mathf.Max(0, totalXP);
        level = 1;
        int remaining = totalXP;

        while (level < MaxMasteryLevel)
        {
            int req = GetMasteryXPRequiredForLevel(level);
            if (remaining >= req)
            {
                remaining -= req;
                level++;
            }
            else
            {
                break;
            }
        }

        if (level >= MaxMasteryLevel)
        {
            level = MaxMasteryLevel;
            currentLevelXP = 0;
        }
        else
        {
            currentLevelXP = remaining;
        }
    }

    public static int CalculateMasteryLevelUpCCReward(int oldLevel, int newLevel)
    {
        int totalCC = 0;
        for (int lvl = oldLevel + 1; lvl <= newLevel; lvl++)
        {
            totalCC += MasteryLevelUpBaseCC;
            if (lvl == 10 || lvl == 25 || lvl == 50 || lvl == 75)
                totalCC += MasteryMilestoneBonusCC;
            else if (lvl == MaxMasteryLevel)
                totalCC += MasteryMaxLevelBonusCC;
        }
        return totalCC;
    }

    /// <summary>
    /// Maps a character mastery level to its cosmetic tier (0–3).
    /// The visual milestones are 25, 50 and 100.
    /// </summary>
    public static int GetMasteryCosmeticTier(int masteryLevel)
    {
        if (masteryLevel >= MasteryCosmeticTier3Level)
            return 3;
        if (masteryLevel >= MasteryCosmeticTier2Level)
            return 2;
        if (masteryLevel >= MasteryCosmeticTier1Level)
            return 1;
        return 0;
    }

    public int GetCharacterMasteryCosmeticTier(string characterName) =>
        GetMasteryCosmeticTier(GetCharacterMasteryLevel(characterName));

    /// <summary>
    /// Returns the stored skin choice for a character, or -1 when the player has
    /// not picked one and the highest unlocked tier should be used automatically.
    /// </summary>
    public int GetSelectedMasteryCosmeticTier(string characterName)
    {
        if (string.IsNullOrEmpty(characterName))
            return MasteryCosmeticTierAuto;

        if (
            _cosmeticSelectionMap.TryGetValue(characterName, out CharacterMasteryCosmeticEntry entry)
            && entry != null
        )
            return entry.Tier;

        return MasteryCosmeticTierAuto;
    }

    /// <summary>
    /// Returns the tier that should actually be worn, clamped to the highest tier
    /// the character has unlocked. This is what the local player publishes to Photon
    /// and what the mastery screen highlights as selected.
    /// </summary>
    public int GetActiveMasteryCosmeticTier(string characterName)
    {
        int highestUnlocked = GetCharacterMasteryCosmeticTier(characterName);
        int selected = GetSelectedMasteryCosmeticTier(characterName);

        if (selected < 0)
            return highestUnlocked;

        return Mathf.Clamp(selected, 0, highestUnlocked);
    }

    /// <summary>
    /// Stores a per-character skin choice. Tier 0 is the original look; tiers 1-4
    /// are the mastery milestones and must already be unlocked.
    /// </summary>
    public void SetSelectedMasteryCosmeticTier(string characterName, int tier)
    {
        if (string.IsNullOrEmpty(characterName))
            return;

        int highestUnlocked = GetCharacterMasteryCosmeticTier(characterName);
        int clampedTier = Mathf.Clamp(tier, 0, highestUnlocked);

        if (!_cosmeticSelectionMap.TryGetValue(characterName, out var entry) || entry == null)
        {
            entry = new CharacterMasteryCosmeticEntry(characterName, clampedTier);
            _cosmeticSelectionMap[characterName] = entry;
        }
        else
        {
            if (entry.Tier == clampedTier)
                return;
            entry.Tier = clampedTier;
        }

        _dirty = true;
        SaveProgression(syncCloud: true);
    }

    // ── Public API: Account XP & Level ──────────────────────────────────

    public void AddAccountXP(int amount, string reason = "", bool showStatusToast = true)
    {
        if (amount <= 0)
            return;

        int oldLevel = AccountLevel;
        TotalAccountXP += amount;
        CalculateAccountLevelFromTotalXP(TotalAccountXP, out int newLevel, out int newCurrentXP);

        AccountLevel = newLevel;
        AccountXP = newCurrentXP;
        _dirty = true;

        if (showStatusToast)
        {
            string reasonSuffix = string.IsNullOrEmpty(reason) ? "" : $" ({reason.Replace('_', ' ')})";
            CUBEStatusText.CreateText($"<color=#55DDFF>+{amount} XP</color>{reasonSuffix}");
        }

        OnAccountXPChanged?.Invoke(AccountLevel, AccountXP, AccountXPForNextLevel);

        if (newLevel > oldLevel)
        {
            int ccReward = CalculateAccountLevelUpCCReward(oldLevel, newLevel);
            if (ccReward > 0 && PlayfabManager.Instance != null)
            {
                PlayfabManager.Instance.AddCubeCoins(ccReward, $"account_lvl_{newLevel}");
            }

            CUBEStatusText.CreateText(
                $"<color=#00FFFF><b>ACCOUNT LEVEL UP!</b></color> LVL {newLevel} <color=yellow>(+{ccReward} CC)</color>"
            );

            Debug.Log($"[Progression] Account Level Up! {oldLevel} -> {newLevel} (+{ccReward} CC)");
            OnAccountLevelUp?.Invoke(oldLevel, newLevel, ccReward);
            SyncPhotonProperties();
            SaveProgression(syncCloud: true);
        }
    }

    public void SetAccountLevel(int targetLevel)
    {
        targetLevel = Mathf.Max(1, targetLevel);
        int oldLevel = AccountLevel;
        TotalAccountXP = GetTotalAccountXPForLevel(targetLevel);
        AccountLevel = targetLevel;
        AccountXP = 0;
        _dirty = true;

        OnAccountXPChanged?.Invoke(AccountLevel, AccountXP, AccountXPForNextLevel);
        if (targetLevel > oldLevel)
        {
            int ccReward = CalculateAccountLevelUpCCReward(oldLevel, targetLevel);
            OnAccountLevelUp?.Invoke(oldLevel, targetLevel, ccReward);
        }

        SyncPhotonProperties();
        SaveProgression(syncCloud: true);
    }

    // ── Public API: Character Mastery XP & Level ────────────────────────

    public CharacterMasteryEntry GetCharacterMastery(string characterName)
    {
        if (string.IsNullOrEmpty(characterName))
            return new CharacterMasteryEntry("Unknown");

        if (!_masteryMap.TryGetValue(characterName, out var entry))
        {
            entry = new CharacterMasteryEntry(characterName);
            _masteryMap[characterName] = entry;
        }
        return entry;
    }

    public int GetCharacterMasteryLevel(string characterName) => GetCharacterMastery(characterName).Level;
    public int GetCharacterMasteryXP(string characterName) => GetCharacterMastery(characterName).CurrentXP;
    public int GetCharacterMasteryTotalXP(string characterName) => GetCharacterMastery(characterName).TotalXP;
    public int GetCharacterMasteryXPRequired(string characterName) =>
        GetMasteryXPRequiredForLevel(GetCharacterMasteryLevel(characterName));

    public float GetCharacterMasteryProgress01(string characterName)
    {
        var entry = GetCharacterMastery(characterName);
        if (entry.Level >= MaxMasteryLevel)
            return 1f;
        int req = GetMasteryXPRequiredForLevel(entry.Level);
        return req > 0 ? Mathf.Clamp01((float)entry.CurrentXP / req) : 1f;
    }

    public void AddCharacterMasteryXP(CubePlayerBase character, int amount, string reason = "", bool showStatusToast = false)
    {
        if (character == null || string.IsNullOrEmpty(character._Name))
            return;

        AddCharacterMasteryXP(character._Name, amount, reason, showStatusToast);
    }

    public void AddCharacterMasteryXP(string characterName, int amount, string reason = "", bool showStatusToast = false)
    {
        if (string.IsNullOrEmpty(characterName) || amount <= 0)
            return;

        var entry = GetCharacterMastery(characterName);
        if (entry.Level >= MaxMasteryLevel)
            return;

        int oldLevel = entry.Level;
        int maxTotalXP = GetTotalMasteryXPForLevel(MaxMasteryLevel);
        entry.TotalXP = Mathf.Min(maxTotalXP, entry.TotalXP + amount);

        CalculateMasteryLevelFromTotalXP(entry.TotalXP, out int newLevel, out int newCurrentXP);
        entry.Level = newLevel;
        entry.CurrentXP = newCurrentXP;
        _dirty = true;

        if (showStatusToast)
        {
            CUBEStatusText.CreateText($"<color=#FF99FF>+{amount} {characterName} Mastery XP</color>");
        }

        int reqXP = GetMasteryXPRequiredForLevel(entry.Level);
        OnCharacterMasteryChanged?.Invoke(entry.CharacterName, entry.Level, entry.CurrentXP, reqXP);

        if (newLevel > oldLevel)
        {
            HandleCharacterMasteryLevelUp(entry, oldLevel, newLevel);
        }
    }

    public void SetCharacterMasteryLevel(string characterName, int targetLevel)
    {
        if (string.IsNullOrEmpty(characterName))
            return;

        targetLevel = Mathf.Clamp(targetLevel, 1, MaxMasteryLevel);
        var entry = GetCharacterMastery(characterName);
        int oldLevel = entry.Level;

        entry.TotalXP = GetTotalMasteryXPForLevel(targetLevel);
        entry.Level = targetLevel;
        entry.CurrentXP = 0;
        _dirty = true;

        int reqXP = GetMasteryXPRequiredForLevel(entry.Level);
        OnCharacterMasteryChanged?.Invoke(entry.CharacterName, entry.Level, entry.CurrentXP, reqXP);

        if (targetLevel > oldLevel)
        {
            HandleCharacterMasteryLevelUp(entry, oldLevel, targetLevel);
        }
        else
        {
            SaveProgression(syncCloud: true);
        }
    }

    private void HandleCharacterMasteryLevelUp(CharacterMasteryEntry entry, int oldLevel, int newLevel)
    {
        int levelsGained = newLevel - oldLevel;
        if (StatisticsService.Instance != null)
        {
            StatisticsService.Instance.IncrementStat(StatId.MasteryLevelsGained, StatScope.Lifetime, levelsGained);
        }

        int ccReward = CalculateMasteryLevelUpCCReward(oldLevel, newLevel);
        if (ccReward > 0 && PlayfabManager.Instance != null)
        {
            PlayfabManager.Instance.AddCubeCoins(ccReward, $"mastery_{entry.CharacterName}_lvl_{newLevel}");
        }

        CUBEStatusText.CreateText(
            $"<color=#FF77FF><b>{entry.CharacterName.ToUpper()} MASTERY LVL {newLevel}!</b></color> <color=yellow>(+{ccReward} CC)</color>"
        );

        int oldCosmeticTier = GetMasteryCosmeticTier(oldLevel);
        int newCosmeticTier = GetMasteryCosmeticTier(newLevel);
        if (newCosmeticTier > oldCosmeticTier)
        {
            CubePlayerBase charAsset = CharacterDatabase.GetCharacterByName(entry.CharacterName);
            MasteryCosmeticUnlock unlock = MasteryCosmeticUnlock.GetFor(
                charAsset != null ? charAsset.character : null,
                newCosmeticTier
            );
            string unlockName =
                unlock != null && !string.IsNullOrEmpty(unlock.DisplayName)
                    ? unlock.DisplayName
                    : "Mastery Cosmetic";

            CUBEStatusText.CreateText(
                $"<color=#FFD700><b>MASTERY COSMETIC UNLOCKED!</b></color> {entry.CharacterName} — {unlockName}"
            );
            Debug.Log(
                $"[Progression] Cosmetic unlocked: {entry.CharacterName} tier {newCosmeticTier} ({unlockName})"
            );
        }

        Debug.Log($"[Progression] Character Mastery Level Up: {entry.CharacterName} {oldLevel} -> {newLevel} (+{ccReward} CC)");
        OnCharacterMasteryLevelUp?.Invoke(entry.CharacterName, oldLevel, newLevel, ccReward);

        PublishMasteryAchievementEvents(entry.CharacterName, newLevel);
        SaveProgression(syncCloud: true);
    }

    public bool AreAllCharactersMastery100()
    {
        EnsureAllDatabaseCharactersRegistered();
        var allChars = CharacterDatabase.GetAllCharacters();
        if (allChars.Count == 0)
            return false;

        for (int i = 0; i < allChars.Count; i++)
        {
            var c = allChars[i];
            if (c == null || string.IsNullOrEmpty(c._Name))
                continue;

            if (GetCharacterMasteryLevel(c._Name) < MaxMasteryLevel)
                return false;
        }
        return true;
    }

    private void PublishMasteryAchievementEvents(string characterName, int newLevel)
    {
        CubePlayerBase charAsset = CharacterDatabase.GetCharacterByName(characterName);
        BasePlayer.PlayerType role = charAsset is KillerType
            ? BasePlayer.PlayerType.Killer
            : BasePlayer.PlayerType.Survivor;

        bool allMaxed = AreAllCharactersMastery100();

        var ctx = new GameplayContextBuilder()
            .SetSourceActor(AchievementHelpers.GetLocalActorNumber())
            .SetSourceCharacter(charAsset)
            .SetSourceRole(role)
            .SetMastery(newLevel)
            .AddCustomMetadata("AllMastery100", allMaxed ? 1f : 0f)
            .Build();

        GameplayEventBus.Publish(new GameplayEvent(GameplayEventType.PlayerMasteryLevelChanged, ctx));

        if (allMaxed && AchievementService.Instance != null)
        {
            AchievementService.Instance.Unlock("mastery_all_100");
        }
    }

    /// <summary>
    /// Awards both Account XP and Character Mastery XP for the local player's active character.
    /// </summary>
    public void AwardGameplayXP(CubePlayerBase activeCharacter, int accountXP, int masteryXP, string reason = "")
    {
        if (accountXP > 0)
            AddAccountXP(accountXP, reason, showStatusToast: true);

        if (masteryXP > 0 && activeCharacter != null)
            AddCharacterMasteryXP(activeCharacter, masteryXP, reason, showStatusToast: false);
    }

    public void ResetAllProgression()
    {
        AccountLevel = 1;
        AccountXP = 0;
        TotalAccountXP = 0;
        _masteryMap.Clear();
        _cosmeticSelectionMap.Clear();
        EnsureAllDatabaseCharactersRegistered();
        _dirty = true;
        SaveProgression(syncCloud: true);
        NotifyAllProgressionListeners();
    }

    // ── Persistence & PlayFab Cloud Sync ────────────────────────────────

    public void SaveProgression(bool syncCloud = true)
    {
        if (_saveData == null)
            _saveData = new ProgressionSaveData();

        _playerId = PlayfabManager.GetPlayerName() ?? _playerId ?? SystemInfo.deviceUniqueIdentifier;
        _saveData.PlayerId = _playerId;
        _saveData.AccountLevel = AccountLevel;
        _saveData.AccountXP = AccountXP;
        _saveData.TotalAccountXP = TotalAccountXP;
        _saveData.CharacterMasteries.Clear();

        foreach (var kvp in _masteryMap)
        {
            if (kvp.Value != null)
                _saveData.CharacterMasteries.Add(kvp.Value);
        }

        if (_saveData.CharacterMasteryCosmetics == null)
            _saveData.CharacterMasteryCosmetics = new List<CharacterMasteryCosmeticEntry>();
        _saveData.CharacterMasteryCosmetics.Clear();

        foreach (var kvp in _cosmeticSelectionMap)
        {
            if (kvp.Value != null && !string.IsNullOrEmpty(kvp.Value.CharacterName))
                _saveData.CharacterMasteryCosmetics.Add(kvp.Value);
        }

        SaveLocal(_playerId, _saveData);
        _dirty = false;

        if (syncCloud)
            SaveToPlayFab(_saveData);
    }

    private void OnPlayFabLoginSuccess()
    {
        _playerId = PlayfabManager.GetPlayerName() ?? SystemInfo.deviceUniqueIdentifier;

        // Re-check local save under the authenticated player name if different
        var namedLocal = LoadLocal(_playerId);
        if (namedLocal != null)
        {
            _saveData = MergeProgressionData(_saveData, namedLocal, _playerId);
            ApplySaveDataToRuntime(_saveData);
        }

        LoadFromPlayFab(_playerId, cloudData =>
        {
            if (cloudData != null)
            {
                _saveData = MergeProgressionData(_saveData, cloudData, _playerId);
                ApplySaveDataToRuntime(_saveData);
            }

            EnsureAllDatabaseCharactersRegistered();
            SaveProgression(syncCloud: true);
            SyncPhotonProperties();
            NotifyAllProgressionListeners();

            // Re-evaluate mastery achievements in case cloud sync restored high mastery levels
            foreach (var kvp in _masteryMap)
            {
                if (kvp.Value != null && kvp.Value.Level > 1)
                {
                    PublishMasteryAchievementEvents(kvp.Value.CharacterName, kvp.Value.Level);
                }
            }

            OnProgressionSynced?.Invoke();
            Debug.Log($"[Progression] Cloud sync complete for '{_playerId}' — Account LVL {AccountLevel} ({AccountXP}/{AccountXPForNextLevel} XP)");
        });
    }

    private void ApplySaveDataToRuntime(ProgressionSaveData data)
    {
        if (data == null)
            return;

        int totalAcctXP = Mathf.Max(data.TotalAccountXP, GetTotalAccountXPForLevel(data.AccountLevel) + data.AccountXP);
        TotalAccountXP = totalAcctXP;
        CalculateAccountLevelFromTotalXP(TotalAccountXP, out int acctLvl, out int acctCurXP);
        AccountLevel = acctLvl;
        AccountXP = acctCurXP;

        if (data.CharacterMasteries != null)
        {
            for (int i = 0; i < data.CharacterMasteries.Count; i++)
            {
                var entry = data.CharacterMasteries[i];
                if (entry == null || string.IsNullOrEmpty(entry.CharacterName))
                    continue;

                int totalCharXP = Mathf.Max(entry.TotalXP, GetTotalMasteryXPForLevel(entry.Level) + entry.CurrentXP);
                CalculateMasteryLevelFromTotalXP(totalCharXP, out int charLvl, out int charCurXP);

                _masteryMap[entry.CharacterName] = new CharacterMasteryEntry(entry.CharacterName)
                {
                    TotalXP = totalCharXP,
                    Level = charLvl,
                    CurrentXP = charCurXP
                };
            }
        }

        _cosmeticSelectionMap.Clear();
        if (data.CharacterMasteryCosmetics != null)
        {
            for (int i = 0; i < data.CharacterMasteryCosmetics.Count; i++)
            {
                var entry = data.CharacterMasteryCosmetics[i];
                if (entry == null || string.IsNullOrEmpty(entry.CharacterName))
                    continue;

                _cosmeticSelectionMap[entry.CharacterName] =
                    new CharacterMasteryCosmeticEntry(entry.CharacterName, entry.Tier);
            }
        }
    }

    public static ProgressionSaveData MergeProgressionData(
        ProgressionSaveData a,
        ProgressionSaveData b,
        string playerId
    )
    {
        if (a == null) return b ?? new ProgressionSaveData { PlayerId = playerId };
        if (b == null) return a;

        int totalA = Mathf.Max(a.TotalAccountXP, GetTotalAccountXPForLevel(a.AccountLevel) + a.AccountXP);
        int totalB = Mathf.Max(b.TotalAccountXP, GetTotalAccountXPForLevel(b.AccountLevel) + b.AccountXP);
        int mergedTotalAccountXP = Mathf.Max(totalA, totalB);
        CalculateAccountLevelFromTotalXP(mergedTotalAccountXP, out int mergedAcctLvl, out int mergedAcctCurXP);

        var merged = new ProgressionSaveData
        {
            Version = Mathf.Max(a.Version, b.Version),
            PlayerId = playerId,
            TotalAccountXP = mergedTotalAccountXP,
            AccountLevel = mergedAcctLvl,
            AccountXP = mergedAcctCurXP
        };

        var map = new Dictionary<string, CharacterMasteryEntry>(StringComparer.OrdinalIgnoreCase);

        void MergeList(List<CharacterMasteryEntry> list)
        {
            if (list == null) return;
            for (int i = 0; i < list.Count; i++)
            {
                var item = list[i];
                if (item == null || string.IsNullOrEmpty(item.CharacterName))
                    continue;

                int itemTotal = Mathf.Max(item.TotalXP, GetTotalMasteryXPForLevel(item.Level) + item.CurrentXP);
                if (!map.TryGetValue(item.CharacterName, out var existing))
                {
                    CalculateMasteryLevelFromTotalXP(itemTotal, out int lvl, out int cur);
                    map[item.CharacterName] = new CharacterMasteryEntry(item.CharacterName)
                    {
                        TotalXP = itemTotal,
                        Level = lvl,
                        CurrentXP = cur
                    };
                }
                else
                {
                    int bestTotal = Mathf.Max(existing.TotalXP, itemTotal);
                    CalculateMasteryLevelFromTotalXP(bestTotal, out int lvl, out int cur);
                    existing.TotalXP = bestTotal;
                    existing.Level = lvl;
                    existing.CurrentXP = cur;
                }
            }
        }

        MergeList(a.CharacterMasteries);
        MergeList(b.CharacterMasteries);

        foreach (var kvp in map)
            merged.CharacterMasteries.Add(kvp.Value);

        // Skin choices are preferences rather than progress, so the local save (a)
        // wins when both sides have one. Cloud data only fills in missing characters.
        var cosmeticMap = new Dictionary<string, CharacterMasteryCosmeticEntry>(
            StringComparer.OrdinalIgnoreCase
        );

        void MergeCosmetics(List<CharacterMasteryCosmeticEntry> list)
        {
            if (list == null)
                return;

            for (int i = 0; i < list.Count; i++)
            {
                var item = list[i];
                if (item == null || string.IsNullOrEmpty(item.CharacterName))
                    continue;

                if (!cosmeticMap.ContainsKey(item.CharacterName))
                {
                    cosmeticMap[item.CharacterName] = new CharacterMasteryCosmeticEntry(
                        item.CharacterName,
                        item.Tier
                    );
                }
            }
        }

        MergeCosmetics(a.CharacterMasteryCosmetics);
        MergeCosmetics(b.CharacterMasteryCosmetics);

        foreach (var kvp in cosmeticMap)
            merged.CharacterMasteryCosmetics.Add(kvp.Value);

        return merged;
    }

    private static string GetLocalFilePath(string playerId)
    {
        string safeId = string.IsNullOrEmpty(playerId) ? "local" : playerId;
        foreach (char c in Path.GetInvalidFileNameChars())
            safeId = safeId.Replace(c, '_');

        return Path.Combine(Application.persistentDataPath, SaveFolderName, safeId + SaveFileSuffix);
    }

    private static ProgressionSaveData LoadLocal(string playerId)
    {
        string path = GetLocalFilePath(playerId);
        if (!File.Exists(path))
        {
            return new ProgressionSaveData { PlayerId = playerId };
        }

        try
        {
            string json = File.ReadAllText(path);
            var data = JsonUtility.FromJson<ProgressionSaveData>(json);
            return data ?? new ProgressionSaveData { PlayerId = playerId };
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Progression] Failed to load local progression save: {ex.Message}");
            return new ProgressionSaveData { PlayerId = playerId };
        }
    }

    private static void SaveLocal(string playerId, ProgressionSaveData data)
    {
        if (data == null) return;
        string path = GetLocalFilePath(playerId);
        string dir = Path.GetDirectoryName(path);

        try
        {
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string json = JsonUtility.ToJson(data, prettyPrint: true);
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Progression] Failed to save local progression file: {ex.Message}");
        }
    }

    private static void SaveToPlayFab(ProgressionSaveData data)
    {
        if (!PlayFabClientAPI.IsClientLoggedIn() || data == null)
            return;

        string json = JsonUtility.ToJson(data);
        var userDataReq = new UpdateUserDataRequest
        {
            Data = new Dictionary<string, string>
            {
                { PlayFabProgressionKey, json }
            }
        };

        PlayFabClientAPI.UpdateUserData(
            userDataReq,
            _ => Debug.Log("[Progression] Saved progression to PlayFab UserData."),
            err => Debug.LogWarning($"[Progression] PlayFab UserData save failed: {err.GenerateErrorReport()}")
        );

        var statUpdates = new List<StatisticUpdate>
        {
            new StatisticUpdate { StatisticName = "AccountLevel", Value = data.AccountLevel },
            new StatisticUpdate { StatisticName = "TotalAccountXP", Value = data.TotalAccountXP }
        };

        if (data.CharacterMasteries != null)
        {
            for (int i = 0; i < data.CharacterMasteries.Count; i++)
            {
                var cm = data.CharacterMasteries[i];
                if (cm == null || string.IsNullOrEmpty(cm.CharacterName))
                    continue;
                string safeName = cm.CharacterName.Replace(" ", "_");
                statUpdates.Add(new StatisticUpdate
                {
                    StatisticName = $"MasteryLevel_{safeName}",
                    Value = cm.Level
                });
            }
        }

        PlayFabClientAPI.UpdatePlayerStatistics(
            new UpdatePlayerStatisticsRequest { Statistics = statUpdates },
            _ => { },
            err => Debug.LogWarning($"[Progression] PlayFab Statistics update warning: {err.GenerateErrorReport()}")
        );
    }

    private static void LoadFromPlayFab(string playerId, Action<ProgressionSaveData> onComplete)
    {
        if (!PlayFabClientAPI.IsClientLoggedIn())
        {
            onComplete?.Invoke(null);
            return;
        }

        var req = new GetUserDataRequest
        {
            Keys = new List<string> { PlayFabProgressionKey }
        };

        PlayFabClientAPI.GetUserData(
            req,
            result =>
            {
                if (
                    result.Data != null
                    && result.Data.TryGetValue(PlayFabProgressionKey, out var record)
                    && !string.IsNullOrEmpty(record.Value)
                )
                {
                    try
                    {
                        var cloudData = JsonUtility.FromJson<ProgressionSaveData>(record.Value);
                        onComplete?.Invoke(cloudData);
                        return;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[Progression] Failed parsing PlayFab progression JSON: {ex.Message}");
                    }
                }
                onComplete?.Invoke(null);
            },
            err =>
            {
                Debug.LogWarning($"[Progression] Failed loading PlayFab progression: {err.GenerateErrorReport()}");
                onComplete?.Invoke(null);
            }
        );
    }

    private void SyncPhotonProperties()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.LocalPlayer == null)
            return;

        var props = new Hashtable
        {
            { PropAccountLevel, AccountLevel }
        };
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
    }

    private void NotifyAllProgressionListeners()
    {
        OnAccountXPChanged?.Invoke(AccountLevel, AccountXP, AccountXPForNextLevel);
        foreach (var kvp in _masteryMap)
        {
            var e = kvp.Value;
            if (e != null)
            {
                OnCharacterMasteryChanged?.Invoke(
                    e.CharacterName,
                    e.Level,
                    e.CurrentXP,
                    GetMasteryXPRequiredForLevel(e.Level)
                );
            }
        }
    }
}
