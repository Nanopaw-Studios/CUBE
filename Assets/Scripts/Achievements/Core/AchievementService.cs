using System;
using System.Collections.Generic;
using UnityEngine;

namespace CUBE.Achievements
{
    /// <summary>
    /// Core engine implementation of <see cref="IAchievementService"/> and <see cref="IPlatformRegistry"/>.
    /// Handles evaluation modes, multi-event subscriptions, requirement query context, and delegates rewards to <see cref="IRewardService"/>.
    /// </summary>
    public class AchievementService : MonoBehaviour, IAchievementService
    {
        public static AchievementService Instance { get; private set; }

        public AchievementDatabase Database;
        public bool IncludeDeveloperAchievements;
        public float AutoSaveInterval = 30f;
        public bool EnableDebugLogs = true;

        public event Action<AchievementUnlockedEventArgs> OnAchievementUnlocked;
        public event Action<AchievementProgressEventArgs> OnProgressUpdated;
        public event Action<RequirementFailedEventArgs> OnRequirementFailed;
        public event Action<AchievementDefinition> OnAchievementEvaluated;

        private readonly Dictionary<string, AchievementRuntimeState> _states = new();
        private readonly List<IAchievementPlatform> _platforms = new();
        private readonly HashSet<string> _pendingEndOfRound = new();
        private readonly HashSet<string> _pendingEndOfMatch = new();

        private AchievementSaveData _saveData;
        private string _playerId;
        private float _autoSaveTimer;
        private bool _dirty;
        private bool _initialized;

        public IReadOnlyList<IAchievementPlatform> RegisteredPlatforms => _platforms;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start() => Initialize();

        private void OnDestroy()
        {
            if (Instance == this)
            {
                GameplayEventBus.OnAnyGameplayEvent -= OnGameplayEvent;
                GameplayEventBus.UnsubscribeStatChanged(OnStatChanged);

                if (PlayfabManager.Instance != null)
                {
                    PlayfabManager.Instance.OnLoginSuccessEvent -= OnPlayFabLoginSuccess;
                }

                if (_dirty) FlushSave();

                for (int i = 0; i < _platforms.Count; i++)
                {
                    try { _platforms[i].Shutdown(); }
                    catch (Exception ex) { Debug.LogException(ex); }
                }

                Instance = null;
            }
        }

        private void Update()
        {
            if (!_initialized || AutoSaveInterval <= 0f || !_dirty) return;
            _autoSaveTimer += Time.unscaledDeltaTime;
            if (_autoSaveTimer >= AutoSaveInterval)
            {
                _autoSaveTimer = 0f;
                FlushSave();
            }
        }

        private void Initialize()
        {
            if (_initialized) return;

            if (Database == null)
            {
                Database = Resources.Load<AchievementDatabase>("CUBE Achievements");

#if UNITY_EDITOR
                if (Database == null)
                {
                    string[] dbGuids = UnityEditor.AssetDatabase.FindAssets("t:AchievementDatabase");
                    if (dbGuids.Length > 0)
                    {
                        string dbPath = UnityEditor.AssetDatabase.GUIDToAssetPath(dbGuids[0]);
                        Database = UnityEditor.AssetDatabase.LoadAssetAtPath<AchievementDatabase>(dbPath);
                    }
                }
#endif
            }

            if (Database == null)
            {
                Debug.LogError("[CUBE Achievements] AchievementDatabase not found! Assign it in the Inspector or ensure 'CUBE Achievements' asset exists.");
                return;
            }

#if UNITY_EDITOR
            if (Database.Achievements == null || Database.Achievements.Length == 0)
            {
                string[] defGuids = UnityEditor.AssetDatabase.FindAssets("t:AchievementDefinition", new[] { "Assets/Settings/Achievements" });
                var defs = new List<AchievementDefinition>();
                foreach (var g in defGuids)
                {
                    var d = UnityEditor.AssetDatabase.LoadAssetAtPath<AchievementDefinition>(UnityEditor.AssetDatabase.GUIDToAssetPath(g));
                    if (d != null) defs.Add(d);
                }
                if (defs.Count > 0)
                {
                    Database.Achievements = defs.ToArray();
                    UnityEditor.EditorUtility.SetDirty(Database);
                }
            }
#endif

            _playerId = PlayfabManager.GetPlayerName() ?? SystemInfo.deviceUniqueIdentifier;
            _saveData = AchievementSaveManager.Load(_playerId);
            var savedStates = _saveData.ToDictionary();

            Database.RebuildLookup();

            if (Database.Achievements != null)
            {
                for (int i = 0; i < Database.Achievements.Length; i++)
                {
                    var def = Database.Achievements[i];
                    if (def == null || string.IsNullOrEmpty(def.AchievementId)) continue;
                    if (!IncludeDeveloperAchievements && def.IsDeveloperOnly) continue;

                    if (savedStates.TryGetValue(def.AchievementId, out var state))
                    {
                        // Version check / migration hook
                        if (state.SavedVersion < def.Version)
                        {
                            state.SavedVersion = def.Version;
                            _dirty = true;
                        }
                        _states[def.AchievementId] = state;
                    }
                    else
                    {
                        _states[def.AchievementId] = new AchievementRuntimeState
                        {
                            AchievementId = def.AchievementId,
                            GuidString = def.GuidString,
                            SavedVersion = def.Version,
                            IsUnlocked = false,
                            CurrentProgress = 0f
                        };
                    }
                }
            }

            GameplayEventBus.OnAnyGameplayEvent += OnGameplayEvent;
            GameplayEventBus.SubscribeStatChanged(OnStatChanged);

            if (PlayfabManager.Instance != null)
            {
                PlayfabManager.Instance.OnLoginSuccessEvent += OnPlayFabLoginSuccess;
                if (PlayfabManager.Instance.IsLoggedIn)
                {
                    OnPlayFabLoginSuccess();
                }
            }

            _initialized = true;

            if (EnableDebugLogs)
                Debug.Log($"[CUBE Achievements] AchievementService initialized with {_states.Count} active achievement definitions.");
        }

        private void OnPlayFabLoginSuccess()
        {
            _playerId = PlayfabManager.GetPlayerName() ?? SystemInfo.deviceUniqueIdentifier;
            PlayFabAchievementSync.LoadAchievementsFromPlayFab(_playerId, cloudData =>
            {
                if (cloudData == null) return;
                _saveData = PlayFabAchievementSync.MergeAchievements(_saveData, cloudData, _playerId);
                var mergedStates = _saveData.ToDictionary();

                foreach (var kvp in mergedStates)
                {
                    if (kvp.Value == null) continue;
                    if (_states.TryGetValue(kvp.Key, out var existing))
                    {
                        existing.IsUnlocked = existing.IsUnlocked || kvp.Value.IsUnlocked;
                        existing.CurrentProgress = Mathf.Max(existing.CurrentProgress, kvp.Value.CurrentProgress);
                        existing.UnlockCount = Mathf.Max(existing.UnlockCount, kvp.Value.UnlockCount);
                        if (string.IsNullOrEmpty(existing.UnlockedTimestamp))
                            existing.UnlockedTimestamp = kvp.Value.UnlockedTimestamp;
                    }
                    else
                    {
                        _states[kvp.Key] = kvp.Value;
                    }
                }

                _dirty = true;
                FlushSave();
                if (EnableDebugLogs)
                    Debug.Log($"[CUBE Achievements] PlayFab achievement cloud sync complete for '{_playerId}'.");
            });
        }

        public void TriggerCloudSync()
        {
            if (_saveData != null)
            {
                _saveData.FromDictionary(_states);
                PlayFabAchievementSync.SaveAchievementsToPlayFab(_saveData);
            }
        }

        public void RegisterPlatform(IAchievementPlatform platform)
        {
            if (platform == null) return;
            _platforms.Add(platform);
            platform.Initialize();
        }

        public void UnregisterPlatform(IAchievementPlatform platform) => _platforms.Remove(platform);

        public void Unlock(string achievementId)
        {
            var def = Database.GetById(achievementId);
            if (def != null && _states.TryGetValue(achievementId, out var state))
                DoUnlock(def, state);
        }

        public void Unlock(SerializableGuid guid)
        {
            var def = Database.GetByGuid(guid);
            if (def != null && _states.TryGetValue(def.AchievementId, out var state))
                DoUnlock(def, state);
        }

        public void AddProgress(string achievementId, float amount)
        {
            var def = Database.GetById(achievementId);
            if (def != null && _states.TryGetValue(achievementId, out var state))
            {
                if (state.IsUnlocked && !def.IsRepeatable) return;
                float old = state.CurrentProgress;
                state.Increment(amount, def.TargetValue);

                if (state.CurrentProgress != old)
                {
                    _dirty = true;
                    if (EnableDebugLogs || def.EnableDebugLog)
                        Debug.Log($"[CUBE Achievements] Progress updated: '{def.DisplayName}' ({def.AchievementId}) -> {state.CurrentProgress}/{def.TargetValue}");

                    ReportProgressToPlatforms(def, state);
                    OnProgressUpdated?.Invoke(new AchievementProgressEventArgs
                    {
                        Definition = def, CurrentProgress = state.CurrentProgress, TargetValue = def.TargetValue
                    });
                }

                if (state.HasReachedTarget(def.TargetValue))
                    DoUnlock(def, state);
            }
        }

        public void SetProgress(string achievementId, float value)
        {
            var def = Database.GetById(achievementId);
            if (def != null && _states.TryGetValue(achievementId, out var state))
            {
                if (state.IsUnlocked && !def.IsRepeatable) return;
                float old = state.CurrentProgress;
                state.SetValue(value, def.TargetValue);

                if (state.CurrentProgress != old)
                {
                    _dirty = true;
                    if (EnableDebugLogs || def.EnableDebugLog)
                        Debug.Log($"[CUBE Achievements] Progress updated: '{def.DisplayName}' ({def.AchievementId}) -> {state.CurrentProgress}/{def.TargetValue}");

                    ReportProgressToPlatforms(def, state);
                    OnProgressUpdated?.Invoke(new AchievementProgressEventArgs
                    {
                        Definition = def, CurrentProgress = state.CurrentProgress, TargetValue = def.TargetValue
                    });
                }

                if (state.HasReachedTarget(def.TargetValue))
                    DoUnlock(def, state);
            }
        }

        public AchievementRuntimeState GetState(string achievementId) =>
            _states.TryGetValue(achievementId, out var s) ? s : null;

        public AchievementRuntimeState GetState(SerializableGuid guid)
        {
            var def = Database.GetByGuid(guid);
            return def != null ? GetState(def.AchievementId) : null;
        }

        public bool IsUnlocked(string achievementId) =>
            _states.TryGetValue(achievementId, out var s) && s.IsUnlocked;

        public void EvaluatePending(EvaluationMode mode)
        {
            var pendingSet = mode == EvaluationMode.EndOfRound ? _pendingEndOfRound : _pendingEndOfMatch;
            if (pendingSet.Count == 0) return;

            var copy = new List<string>(pendingSet);
            pendingSet.Clear();

            foreach (var id in copy)
            {
                var def = Database.GetById(id);
                if (def != null && _states.TryGetValue(id, out var state))
                {
                    var reqContext = new RequirementContext(new GameplayEvent(GameplayEventType.None), state);
                    EvaluateDefinition(def, state, reqContext);
                }
            }
        }

        private void OnGameplayEvent(GameplayEvent evt)
        {
            if (!_initialized) return;

            if (evt.Type == GameplayEventType.RoundEnded)
                EvaluatePending(EvaluationMode.EndOfRound);

            var defs = Database.GetByTrigger(evt.Type);
            if (defs.Count == 0) return;

            for (int i = 0; i < defs.Count; i++)
            {
                var def = defs[i];
                if (def == null || !_states.TryGetValue(def.AchievementId, out var state)) continue;
                if (state.IsUnlocked && !def.IsRepeatable) continue;

                var reqContext = new RequirementContext(evt, state);

                if (def.Mode == EvaluationMode.EndOfRound)
                {
                    _pendingEndOfRound.Add(def.AchievementId);
                    continue;
                }
                if (def.Mode == EvaluationMode.EndOfMatch)
                {
                    _pendingEndOfMatch.Add(def.AchievementId);
                    continue;
                }

                EvaluateDefinition(def, state, reqContext);
            }
        }

        private void OnStatChanged(StatChangedEvent evt)
        {
            if (!_initialized) return;

            // Check stat-linked achievements
            if (Database.Achievements == null) return;
            for (int i = 0; i < Database.Achievements.Length; i++)
            {
                var def = Database.Achievements[i];
                if (def == null || def.LinkedStat != evt.Stat || def.LinkedStatScope != evt.Scope) continue;
                if (!_states.TryGetValue(def.AchievementId, out var state)) continue;
                if (state.IsUnlocked && !def.IsRepeatable) continue;

                SetProgress(def.AchievementId, evt.NewValue);
            }
        }

        private void EvaluateDefinition(AchievementDefinition def, AchievementRuntimeState state, IRequirementContext context)
        {
            bool passed = def.EvaluateRequirements(context);
            OnAchievementEvaluated?.Invoke(def);

            if (!passed)
            {
                OnRequirementFailed?.Invoke(new RequirementFailedEventArgs
                {
                    Definition = def, FailedRequirement = (object)def.InlineRequirement ?? def.RequirementAssetOverride, Reason = "Requirement evaluation returned false"
                });
                return;
            }

            if (def.Type == AchievementType.OneShot)
            {
                DoUnlock(def, state);
            }
            else if (def.Type == AchievementType.Progressive)
            {
                if (def.LinkedStat != StatId.None)
                {
                    float statVal = context.GetStat(def.LinkedStat, def.LinkedStatScope);
                    SetProgress(def.AchievementId, statVal);
                }
            }
        }

        private void DoUnlock(AchievementDefinition def, AchievementRuntimeState state)
        {
            if (state.IsUnlocked && !def.IsRepeatable) return;

            state.IsUnlocked = true;
            state.CurrentProgress = def.TargetValue;
            state.UnlockedTimestamp = DateTime.UtcNow.ToString("o");
            state.UnlockCount++;
            _dirty = true;

            Debug.Log($"<color=#55FF55><b>[CUBE Achievements] UNLOCKED!</b></color> '{def.DisplayName}' ({def.AchievementId}) — \"{def.Description}\"");

            // Platforms
            for (int i = 0; i < _platforms.Count; i++)
            {
                try
                {
                    string platId = def.GetPlatformId(_platforms[i].PlatformName);
                    if (!string.IsNullOrEmpty(platId)) _platforms[i].Unlock(platId);
                }
                catch (Exception ex) { Debug.LogException(ex); }
            }

            // Reward Pipeline (Inline Rewards)
            if (def.InlineRewards != null && def.InlineRewards.Count > 0)
            {
                if (RewardService.Instance != null)
                    RewardService.Instance.GrantRewards(def.InlineRewards, def);
            }

            // Lifecycle Events
            OnAchievementUnlocked?.Invoke(new AchievementUnlockedEventArgs
            {
                Definition = def, State = state, UnlockTimestamp = state.UnlockedTimestamp
            });

            FlushSave();
        }

        private void ReportProgressToPlatforms(AchievementDefinition def, AchievementRuntimeState state)
        {
            for (int i = 0; i < _platforms.Count; i++)
            {
                try
                {
                    string platId = def.GetPlatformId(_platforms[i].PlatformName);
                    if (!string.IsNullOrEmpty(platId))
                        _platforms[i].ReportProgress(platId, state.CurrentProgress, def.TargetValue);
                }
                catch (Exception ex) { Debug.LogException(ex); }
            }
        }

        private void FlushSave()
        {
            if (_saveData == null) return;
            _saveData.FromDictionary(_states);
            AchievementSaveManager.Save(_playerId, _saveData);
            TriggerCloudSync();
            _dirty = false;
        }

        // Inner RequirementContext implementation
        private class RequirementContext : IRequirementContext
        {
            public GameplayEvent CurrentEvent { get; }
            public GameplayContext Context => CurrentEvent.Context;
            public AchievementRuntimeState RuntimeState { get; }
            public int LocalActorNumber => AchievementHelpers.GetLocalActorNumber();

            public RequirementContext(GameplayEvent evt, AchievementRuntimeState state = null)
            {
                CurrentEvent = evt;
                RuntimeState = state;
            }

            public float GetStat(StatId statId, StatScope scope)
            {
                return StatisticsService.Instance != null
                    ? StatisticsService.Instance.GetStat(statId, scope)
                    : 0f;
            }

            public bool TryGetCustomValue<T>(string key, out T value)
            {
                if (Context != null) return Context.TryGetCustomValue(key, out value);
                value = default;
                return false;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }
    }
}
