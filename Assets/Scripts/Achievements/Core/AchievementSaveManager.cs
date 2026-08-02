using System;
using System.IO;
using UnityEngine;

namespace CUBE.Achievements
{
    /// <summary>
    /// Handles loading and saving achievement progress to a local JSON file.
    ///
    /// <para>File location:
    /// <c>Application.persistentDataPath/CUBEAchievements/{playerId}_achievements.json</c></para>
    ///
    /// All operations are synchronous and run on the main thread (file is tiny).
    /// For cloud backup, the caller can push <see cref="AchievementSaveData"/>
    /// to PlayFab UserData separately.
    /// </summary>
    public static class AchievementSaveManager
    {
        private const string FolderName = "CUBEAchievements";
        private const string FileSuffix = "_achievements.json";

        // ── API ───────────────────────────────────────────────────────

        /// <summary>
        /// Load achievement state from disk.  Returns a fresh
        /// <see cref="AchievementSaveData"/> if no file exists or if
        /// deserialization fails (defensive against corruption).
        /// </summary>
        public static AchievementSaveData Load(string playerId)
        {
            string path = GetFilePath(playerId);

            if (!File.Exists(path))
            {
                Debug.Log($"[AchievementSave] No save file found for '{playerId}'. Starting fresh.");
                return CreateFresh(playerId);
            }

            try
            {
                string json = File.ReadAllText(path);
                var data = JsonUtility.FromJson<AchievementSaveData>(json);

                if (data == null)
                {
                    Debug.LogWarning("[AchievementSave] Deserialized null. Starting fresh.");
                    return CreateFresh(playerId);
                }

                Debug.Log($"[AchievementSave] Loaded {data.States.Count} achievement states " +
                          $"for '{playerId}'.");
                return data;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AchievementSave] Failed to load save file: {ex.Message}");
                return CreateFresh(playerId);
            }
        }

        /// <summary>
        /// Persist achievement state to disk.
        /// </summary>
        public static void Save(string playerId, AchievementSaveData data)
        {
            if (data == null)
            {
                Debug.LogWarning("[AchievementSave] Null data — nothing to save.");
                return;
            }

            string path = GetFilePath(playerId);
            string dir = Path.GetDirectoryName(path);

            try
            {
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                string json = JsonUtility.ToJson(data, prettyPrint: true);
                File.WriteAllText(path, json);

                Debug.Log($"[AchievementSave] Saved {data.States.Count} states for '{playerId}'.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AchievementSave] Failed to save: {ex.Message}");
            }
        }

        /// <summary>
        /// Delete the save file (for debug/reset purposes).
        /// </summary>
        public static void Delete(string playerId)
        {
            string path = GetFilePath(playerId);
            if (File.Exists(path))
            {
                File.Delete(path);
                Debug.Log($"[AchievementSave] Deleted save for '{playerId}'.");
            }
        }

        /// <summary>
        /// Returns the raw JSON string for cloud backup (e.g. to PlayFab UserData).
        /// </summary>
        public static string ToJson(AchievementSaveData data)
        {
            return data != null ? JsonUtility.ToJson(data) : "{}";
        }

        /// <summary>
        /// Parses a JSON string from cloud backup into save data.
        /// Returns fresh data on failure.
        /// </summary>
        public static AchievementSaveData FromJson(string json, string playerId)
        {
            if (string.IsNullOrEmpty(json))
                return CreateFresh(playerId);

            try
            {
                var data = JsonUtility.FromJson<AchievementSaveData>(json);
                return data ?? CreateFresh(playerId);
            }
            catch
            {
                return CreateFresh(playerId);
            }
        }

        // ── Internals ─────────────────────────────────────────────────

        private static string GetFilePath(string playerId)
        {
            // Sanitise player ID for file system safety
            string safeId = string.IsNullOrEmpty(playerId) ? "local" : playerId;
            foreach (char c in Path.GetInvalidFileNameChars())
                safeId = safeId.Replace(c, '_');

            return Path.Combine(Application.persistentDataPath, FolderName,
                safeId + FileSuffix);
        }

        private static AchievementSaveData CreateFresh(string playerId)
        {
            return new AchievementSaveData
            {
                Version = 1,
                PlayerId = playerId
            };
        }
    }
}
