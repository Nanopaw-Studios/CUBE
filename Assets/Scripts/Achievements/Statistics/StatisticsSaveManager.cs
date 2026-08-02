using System;
using System.IO;
using UnityEngine;

namespace CUBE.Achievements
{
    /// <summary>
    /// Handles loading and saving lifetime statistics to a local JSON file.
    ///
    /// <para>File location:
    /// <c>Application.persistentDataPath/CUBEAchievements/{playerId}_statistics.json</c></para>
    /// </summary>
    public static class StatisticsSaveManager
    {
        private const string FolderName = "CUBEAchievements";
        private const string FileSuffix = "_statistics.json";

        public static StatisticsSaveData Load(string playerId)
        {
            string path = GetFilePath(playerId);

            if (!File.Exists(path))
            {
                Debug.Log($"[StatisticsSave] No save file found for '{playerId}'. Starting fresh.");
                return CreateFresh(playerId);
            }

            try
            {
                string json = File.ReadAllText(path);
                var data = JsonUtility.FromJson<StatisticsSaveData>(json);

                if (data == null)
                {
                    Debug.LogWarning("[StatisticsSave] Deserialized null. Starting fresh.");
                    return CreateFresh(playerId);
                }

                Debug.Log($"[StatisticsSave] Loaded {data.Entries.Count} stat entries " +
                          $"for '{playerId}'.");
                return data;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[StatisticsSave] Failed to load: {ex.Message}");
                return CreateFresh(playerId);
            }
        }

        public static void Save(string playerId, StatisticsSaveData data)
        {
            if (data == null) return;

            string path = GetFilePath(playerId);
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
                Debug.LogError($"[StatisticsSave] Failed to save: {ex.Message}");
            }
        }

        public static void Delete(string playerId)
        {
            string path = GetFilePath(playerId);
            if (File.Exists(path))
            {
                File.Delete(path);
                Debug.Log($"[StatisticsSave] Deleted save for '{playerId}'.");
            }
        }

        private static string GetFilePath(string playerId)
        {
            string safeId = string.IsNullOrEmpty(playerId) ? "local" : playerId;
            foreach (char c in Path.GetInvalidFileNameChars())
                safeId = safeId.Replace(c, '_');

            return Path.Combine(Application.persistentDataPath, FolderName,
                safeId + FileSuffix);
        }

        private static StatisticsSaveData CreateFresh(string playerId)
        {
            return new StatisticsSaveData
            {
                Version = 1,
                PlayerId = playerId
            };
        }
    }
}
