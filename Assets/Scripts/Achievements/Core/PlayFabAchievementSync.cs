using System;
using System.Collections.Generic;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

namespace CUBE.Achievements
{
    /// <summary>
    /// Helper class providing PlayFab API cloud storage and synchronization for
    /// Achievement and Statistics data using PlayFab UserData (Player Data) and
    /// Player Statistics APIs.
    /// </summary>
    public static class PlayFabAchievementSync
    {
        public const string AchievementDataKey = "CUBE_Achievements";
        public const string StatisticsDataKey = "CUBE_Statistics";

        // ── Cloud Achievement Data ──────────────────────────────────────

        /// <summary>
        /// Saves AchievementSaveData to PlayFab UserData under key 'CUBE_Achievements'.
        /// </summary>
        public static void SaveAchievementsToPlayFab(AchievementSaveData data, Action onSuccess = null, Action<PlayFabError> onError = null)
        {
            if (!PlayFabClientAPI.IsClientLoggedIn()) return;
            if (data == null) return;

            string json = AchievementSaveManager.ToJson(data);

            var request = new UpdateUserDataRequest
            {
                Data = new Dictionary<string, string>
                {
                    { AchievementDataKey, json }
                }
            };

            PlayFabClientAPI.UpdateUserData(
                request,
                result =>
                {
                    Debug.Log("[PlayFabAchievementSync] Successfully saved achievements to PlayFab UserData.");
                    onSuccess?.Invoke();
                },
                error =>
                {
                    Debug.LogError($"[PlayFabAchievementSync] Failed to save achievements to PlayFab: {error.GenerateErrorReport()}");
                    onError?.Invoke(error);
                }
            );
        }

        /// <summary>
        /// Loads AchievementSaveData from PlayFab UserData.
        /// </summary>
        public static void LoadAchievementsFromPlayFab(string playerId, Action<AchievementSaveData> onComplete)
        {
            if (!PlayFabClientAPI.IsClientLoggedIn())
            {
                onComplete?.Invoke(null);
                return;
            }

            var request = new GetUserDataRequest
            {
                Keys = new List<string> { AchievementDataKey }
            };

            PlayFabClientAPI.GetUserData(
                request,
                result =>
                {
                    if (result.Data != null && result.Data.TryGetValue(AchievementDataKey, out var record) && !string.IsNullOrEmpty(record.Value))
                    {
                        var data = AchievementSaveManager.FromJson(record.Value, playerId);
                        Debug.Log($"[PlayFabAchievementSync] Loaded cloud achievements for '{playerId}'. States count: {data?.States?.Count ?? 0}");
                        onComplete?.Invoke(data);
                    }
                    else
                    {
                        Debug.Log($"[PlayFabAchievementSync] No cloud achievements found for '{playerId}'.");
                        onComplete?.Invoke(null);
                    }
                },
                error =>
                {
                    Debug.LogWarning($"[PlayFabAchievementSync] Failed to load cloud achievements: {error.GenerateErrorReport()}");
                    onComplete?.Invoke(null);
                }
            );
        }

        // ── Cloud Statistics Data ───────────────────────────────────────

        /// <summary>
        /// Saves StatisticsSaveData to PlayFab UserData AND updates PlayFab Player Statistics.
        /// </summary>
        public static void SaveStatisticsToPlayFab(StatisticsSaveData data, Action onSuccess = null, Action<PlayFabError> onError = null)
        {
            if (!PlayFabClientAPI.IsClientLoggedIn()) return;
            if (data == null) return;

            string json = JsonUtility.ToJson(data);

            var userDataRequest = new UpdateUserDataRequest
            {
                Data = new Dictionary<string, string>
                {
                    { StatisticsDataKey, json }
                }
            };

            PlayFabClientAPI.UpdateUserData(
                userDataRequest,
                result => Debug.Log("[PlayFabAchievementSync] Saved statistics to PlayFab UserData."),
                error => Debug.LogError($"[PlayFabAchievementSync] Failed to save statistics UserData: {error.GenerateErrorReport()}")
            );

            // Also sync numeric lifetime statistics to PlayFab Player Statistics API
            var statUpdates = new List<StatisticUpdate>();
            var dict = data.ToDictionary();

            foreach (var kvp in dict)
            {
                if (kvp.Key == StatId.None) continue;
                statUpdates.Add(new StatisticUpdate
                {
                    StatisticName = kvp.Key.ToString(),
                    Value = Mathf.RoundToInt(kvp.Value)
                });
            }

            if (statUpdates.Count > 0)
            {
                var statRequest = new UpdatePlayerStatisticsRequest
                {
                    Statistics = statUpdates
                };

                PlayFabClientAPI.UpdatePlayerStatistics(
                    statRequest,
                    result =>
                    {
                        Debug.Log($"[PlayFabAchievementSync] Successfully updated {statUpdates.Count} PlayFab Player Statistics.");
                        onSuccess?.Invoke();
                    },
                    error =>
                    {
                        Debug.LogWarning($"[PlayFabAchievementSync] Failed to update PlayFab Player Statistics: {error.GenerateErrorReport()}");
                        onError?.Invoke(error);
                    }
                );
            }
            else
            {
                onSuccess?.Invoke();
            }
        }

        /// <summary>
        /// Loads StatisticsSaveData from PlayFab UserData and Player Statistics.
        /// </summary>
        public static void LoadStatisticsFromPlayFab(string playerId, Action<StatisticsSaveData> onComplete)
        {
            if (!PlayFabClientAPI.IsClientLoggedIn())
            {
                onComplete?.Invoke(null);
                return;
            }

            var request = new GetUserDataRequest
            {
                Keys = new List<string> { StatisticsDataKey }
            };

            PlayFabClientAPI.GetUserData(
                request,
                result =>
                {
                    StatisticsSaveData cloudData = null;
                    if (result.Data != null && result.Data.TryGetValue(StatisticsDataKey, out var record) && !string.IsNullOrEmpty(record.Value))
                    {
                        try
                        {
                            cloudData = JsonUtility.FromJson<StatisticsSaveData>(record.Value);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[PlayFabAchievementSync] Failed parsing cloud stats JSON: {ex.Message}");
                        }
                    }

                    if (cloudData == null)
                    {
                        cloudData = new StatisticsSaveData { Version = 1, PlayerId = playerId };
                    }

                    // Query PlayFab Player Statistics to complement/merge
                    PlayFabClientAPI.GetPlayerStatistics(
                        new GetPlayerStatisticsRequest(),
                        statResult =>
                        {
                            var dict = cloudData.ToDictionary();
                            if (statResult.Statistics != null)
                            {
                                foreach (var stat in statResult.Statistics)
                                {
                                    if (Enum.TryParse<StatId>(stat.StatisticName, out var statId))
                                    {
                                        dict.TryGetValue(statId, out float existing);
                                        dict[statId] = Mathf.Max(existing, stat.Value);
                                    }
                                }
                            }
                            cloudData.FromDictionary(dict);
                            Debug.Log($"[PlayFabAchievementSync] Loaded cloud statistics for '{playerId}'. Entries: {cloudData.Entries.Count}");
                            onComplete?.Invoke(cloudData);
                        },
                        statError =>
                        {
                            // Return cloud UserData statistics if Player Statistics call fails
                            onComplete?.Invoke(cloudData);
                        }
                    );
                },
                error =>
                {
                    Debug.LogWarning($"[PlayFabAchievementSync] Failed to load cloud statistics: {error.GenerateErrorReport()}");
                    onComplete?.Invoke(null);
                }
            );
        }

        // ── Merge Strategies ────────────────────────────────────────────

        /// <summary>
        /// Merges local and cloud achievement state taking highest progress and unlock status.
        /// </summary>
        public static AchievementSaveData MergeAchievements(AchievementSaveData local, AchievementSaveData cloud, string playerId)
        {
            if (local == null) return cloud ?? new AchievementSaveData { PlayerId = playerId };
            if (cloud == null) return local;

            var localDict = local.ToDictionary();
            var cloudDict = cloud.ToDictionary();
            var mergedDict = new Dictionary<string, AchievementRuntimeState>(localDict);

            foreach (var kvp in cloudDict)
            {
                string id = kvp.Key;
                var cloudState = kvp.Value;

                if (!mergedDict.TryGetValue(id, out var localState))
                {
                    mergedDict[id] = cloudState;
                }
                else
                {
                    var mergedState = new AchievementRuntimeState
                    {
                        AchievementId = id,
                        GuidString = !string.IsNullOrEmpty(localState.GuidString) ? localState.GuidString : cloudState.GuidString,
                        SavedVersion = Mathf.Max(localState.SavedVersion, cloudState.SavedVersion),
                        IsUnlocked = localState.IsUnlocked || cloudState.IsUnlocked,
                        CurrentProgress = Mathf.Max(localState.CurrentProgress, cloudState.CurrentProgress),
                        UnlockCount = Mathf.Max(localState.UnlockCount, cloudState.UnlockCount),
                        UnlockedTimestamp = !string.IsNullOrEmpty(localState.UnlockedTimestamp)
                            ? localState.UnlockedTimestamp
                            : cloudState.UnlockedTimestamp
                    };

                    mergedDict[id] = mergedState;
                }
            }

            var mergedData = new AchievementSaveData
            {
                Version = Mathf.Max(local.Version, cloud.Version),
                PlayerId = playerId
            };
            mergedData.FromDictionary(mergedDict);
            return mergedData;
        }

        /// <summary>
        /// Merges local and cloud statistics by taking the maximum lifetime value per stat.
        /// </summary>
        public static StatisticsSaveData MergeStatistics(StatisticsSaveData local, StatisticsSaveData cloud, string playerId)
        {
            if (local == null) return cloud ?? new StatisticsSaveData { PlayerId = playerId };
            if (cloud == null) return local;

            var localDict = local.ToDictionary();
            var cloudDict = cloud.ToDictionary();
            var mergedDict = new Dictionary<StatId, float>(localDict);

            foreach (var kvp in cloudDict)
            {
                StatId id = kvp.Key;
                float cloudValue = kvp.Value;

                if (mergedDict.TryGetValue(id, out float localValue))
                {
                    mergedDict[id] = Mathf.Max(localValue, cloudValue);
                }
                else
                {
                    mergedDict[id] = cloudValue;
                }
            }

            var mergedData = new StatisticsSaveData
            {
                Version = Mathf.Max(local.Version, cloud.Version),
                PlayerId = playerId
            };
            mergedData.FromDictionary(mergedDict);
            return mergedData;
        }
    }
}
