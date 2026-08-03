using System;
using System.Collections.Generic;
using UnityEngine;

namespace CUBE.Achievements
{
    /// <summary>
    /// Decoupled implementation of <see cref="IStatisticsService"/>.
    /// Listens to <see cref="GameplayEventBus"/> and updates Lifetime and Session statistics.
    /// Publishes <see cref="StatChangedEvent"/> back to <see cref="GameplayEventBus"/> on every change.
    /// Knows NOTHING about achievements.
    /// </summary>
    public class StatisticsService : MonoBehaviour, IStatisticsService
    {
        public static StatisticsService Instance { get; private set; }

        public event Action<StatId, StatScope, float> OnStatChanged;

        private readonly Dictionary<StatId, float> _lifetimeStats = new();
        private readonly Dictionary<StatId, float> _sessionStats = new();

        private StatisticsSaveData _saveData;
        private string _playerId;
        private bool _dirty;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            Initialize();
        }

        private float _secondAccumulator;

        private void Update()
        {
            _secondAccumulator += Time.unscaledDeltaTime;
            if (_secondAccumulator >= 1.0f)
            {
                float elapsedSeconds = Mathf.Floor(_secondAccumulator);
                _secondAccumulator -= elapsedSeconds;

                float currentSeconds = GetStat(StatId.TimePlayedSeconds, StatScope.Lifetime) + elapsedSeconds;
                _lifetimeStats[StatId.TimePlayedSeconds] = currentSeconds;
                _lifetimeStats[StatId.HoursPlayed] = currentSeconds / 3600.0f;
                _dirty = true;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                GameplayEventBus.OnAnyGameplayEvent -= OnGameplayEvent;
                if (PlayfabManager.Instance != null)
                {
                    PlayfabManager.Instance.OnLoginSuccessEvent -= OnPlayFabLoginSuccess;
                }
                if (_dirty) SaveLifetimeStats();
                Instance = null;
            }
        }

        private void Initialize()
        {
            _playerId = PlayfabManager.GetPlayerName() ?? SystemInfo.deviceUniqueIdentifier;
            _saveData = StatisticsSaveManager.Load(_playerId);
            _lifetimeStats.Clear();

            foreach (var entry in _saveData.ToDictionary())
                _lifetimeStats[entry.Key] = entry.Value;

            GameplayEventBus.OnAnyGameplayEvent += OnGameplayEvent;

            if (PlayfabManager.Instance != null)
            {
                PlayfabManager.Instance.OnLoginSuccessEvent += OnPlayFabLoginSuccess;
                if (PlayfabManager.Instance.IsLoggedIn)
                {
                    OnPlayFabLoginSuccess();
                }
            }
        }

        private void OnPlayFabLoginSuccess()
        {
            _playerId = PlayfabManager.GetPlayerName() ?? SystemInfo.deviceUniqueIdentifier;
            PlayFabAchievementSync.LoadStatisticsFromPlayFab(_playerId, cloudData =>
            {
                if (cloudData == null) return;
                _saveData = PlayFabAchievementSync.MergeStatistics(_saveData, cloudData, _playerId);

                foreach (var entry in _saveData.ToDictionary())
                {
                    StatId id = entry.Key;
                    float value = entry.Value;
                    _lifetimeStats.TryGetValue(id, out float oldVal);
                    if (value != oldVal)
                    {
                        _lifetimeStats[id] = value;
                        OnStatChanged?.Invoke(id, StatScope.Lifetime, value);
                        GameplayEventBus.PublishStatChanged(new StatChangedEvent(id, StatScope.Lifetime, value, value - oldVal));
                    }
                }

                _dirty = true;
                SaveLifetimeStats();
                Debug.Log($"[StatisticsService] PlayFab statistics cloud sync complete for '{_playerId}'.");
            });
        }

        public float GetStat(StatId id, StatScope scope)
        {
            var dict = scope == StatScope.Lifetime ? _lifetimeStats : _sessionStats;
            dict.TryGetValue(id, out float val);
            return val;
        }

        public void SetStat(StatId id, StatScope scope, float value)
        {
            if (id == StatId.None) return;

            var dict = scope == StatScope.Lifetime ? _lifetimeStats : _sessionStats;
            dict.TryGetValue(id, out float oldVal);
            float delta = value - oldVal;

            dict[id] = value;
            if (scope == StatScope.Lifetime) _dirty = true;

            OnStatChanged?.Invoke(id, scope, value);
            GameplayEventBus.PublishStatChanged(new StatChangedEvent(id, scope, value, delta));
        }

        public void IncrementStat(StatId id, StatScope scope, float delta)
        {
            if (id == StatId.None || delta == 0f) return;
            float current = GetStat(id, scope);
            SetStat(id, scope, current + delta);
        }

        public void ResetSessionStats(ResetTiming timing)
        {
            _sessionStats.Clear();
            Debug.Log($"[StatisticsService] Reset session statistics ({timing})");
        }

        public void SaveLifetimeStats()
        {
            if (_saveData == null) return;
            _saveData.FromDictionary(_lifetimeStats);
            StatisticsSaveManager.Save(_playerId, _saveData);
            PlayFabAchievementSync.SaveStatisticsToPlayFab(_saveData);
            _dirty = false;
        }

        private void OnGameplayEvent(GameplayEvent evt)
        {
            int localActor = AchievementHelpers.GetLocalActorNumber();
            if (localActor < 0) return;

            bool isLocalSource = evt.Context.SourceActorNumber == localActor;
            bool isLocalTarget = evt.Context.TargetActorNumber == localActor;
            bool isGlobal = evt.Context.SourceActorNumber == -1;

            if (!isLocalSource && !isLocalTarget && !isGlobal) return;

            switch (evt.Type)
            {
                case GameplayEventType.PlayerDied:
                    IncrementStat(StatId.Deaths, StatScope.Lifetime, 1f);
                    IncrementStat(StatId.DeathsThisSession, StatScope.Session, 1f);
                    break;
                case GameplayEventType.PlayerKilledEnemy:
                    IncrementStat(StatId.PlayersEliminated, StatScope.Lifetime, 1f);
                    IncrementStat(StatId.EliminationsThisRound, StatScope.Session, 1f);
                    break;
                case GameplayEventType.PlayerWonRound:
                    IncrementStat(StatId.LifetimeWins, StatScope.Lifetime, 1f);
                    break;
                case GameplayEventType.PlayerLostRound:
                    IncrementStat(StatId.LifetimeLosses, StatScope.Lifetime, 1f);
                    break;
                case GameplayEventType.RoundStarted:
                    ResetSessionStats(ResetTiming.RoundStart);
                    IncrementStat(StatId.MatchesPlayed, StatScope.Lifetime, 1f);
                    break;
                case GameplayEventType.PlayerSurvivedRound:
                    IncrementStat(StatId.RoundsSurvived, StatScope.Lifetime, 1f);
                    break;
                case GameplayEventType.PlayerTookDamage:
                    IncrementStat(StatId.DamageTaken, StatScope.Lifetime, evt.Context.DamageAmount);
                    IncrementStat(StatId.DamageTakenThisRound, StatScope.Session, evt.Context.DamageAmount);
                    break;
                case GameplayEventType.PlayerDamaged:
                    IncrementStat(StatId.DamageDealt, StatScope.Lifetime, evt.Context.DamageAmount);
                    IncrementStat(StatId.DamageDealtThisRound, StatScope.Session, evt.Context.DamageAmount);
                    break;
                case GameplayEventType.PlayerDistanceMoved:
                    IncrementStat(StatId.DistanceWalked, StatScope.Lifetime, evt.Context.Distance);
                    IncrementStat(StatId.DistanceThisRound, StatScope.Session, evt.Context.Distance);
                    break;
                case GameplayEventType.PlayerJumped:
                    IncrementStat(StatId.JumpCount, StatScope.Lifetime, 1f);
                    IncrementStat(StatId.JumpCountThisSession, StatScope.Session, 1f);
                    break;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }
    }
}
