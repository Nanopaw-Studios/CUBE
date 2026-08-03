#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CUBE.Achievements.Editor
{
    public static class AchievementDatabaseAutoPopulator
    {
        [MenuItem("Tools/Achievements/Rebuild Database")]
        public static void RebuildDatabase()
        {
            string[] dbGuids = AssetDatabase.FindAssets("t:AchievementDatabase");
            if (dbGuids.Length == 0)
            {
                Debug.LogError("[AchievementDatabaseAutoPopulator] No AchievementDatabase asset found.");
                return;
            }

            string dbPath = AssetDatabase.GUIDToAssetPath(dbGuids[0]);
            var db = AssetDatabase.LoadAssetAtPath<AchievementDatabase>(dbPath);
            if (db == null)
            {
                Debug.LogError($"[AchievementDatabaseAutoPopulator] Failed to load database at {dbPath}");
                return;
            }

            string[] defGuids = AssetDatabase.FindAssets("t:AchievementDefinition", new[] { "Assets/Settings/Achievements" });
            var definitions = new List<AchievementDefinition>();

            foreach (var guid in defGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var def = AssetDatabase.LoadAssetAtPath<AchievementDefinition>(path);
                if (def != null)
                {
                    definitions.Add(def);
                }
            }

            Undo.RecordObject(db, "Rebuild Achievement Database");
            db.Achievements = definitions.ToArray();
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();

            Debug.Log($"[AchievementDatabaseAutoPopulator] Rebuilt '{db.name}' with {definitions.Count} achievement definitions.");
        }
    }
}
#endif
