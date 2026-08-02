using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CUBE.Achievements.Editor
{
    [CustomEditor(typeof(AchievementDefinition))]
    public class AchievementDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var def = (AchievementDefinition)target;

            EditorGUILayout.Space(4);
            var titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter
            };

            string title = string.IsNullOrEmpty(def.DisplayName) ? "(Unnamed Achievement)" : def.DisplayName;
            EditorGUILayout.LabelField(title, titleStyle);

            var guidStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.gray }
            };
            EditorGUILayout.LabelField($"GUID: {def.GuidString}", guidStyle);
            EditorGUILayout.Space(8);

            // Validation hints
            if (string.IsNullOrEmpty(def.AchievementId))
                EditorGUILayout.HelpBox("Achievement ID is empty!", MessageType.Error);

            if (def.RelevantEvents == null || def.RelevantEvents.Length == 0)
                EditorGUILayout.HelpBox("No RelevantEvents assigned.", MessageType.Warning);

            DrawDefaultInspector();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Inline Authoring Helper", EditorStyles.boldLabel);

            // Add inline requirement button
            if (GUILayout.Button("+ Set Inline Requirement", GUILayout.Height(24)))
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("Role Requirement"), false, () => SetRequirement(def, new RoleRequirement()));
                menu.AddItem(new GUIContent("Character Requirement"), false, () => SetRequirement(def, new CharacterRequirement()));
                menu.AddItem(new GUIContent("Stat Requirement"), false, () => SetRequirement(def, new StatRequirement()));
                menu.AddItem(new GUIContent("Event Property Requirement"), false, () => SetRequirement(def, new EventPropertyRequirement()));
                menu.AddItem(new GUIContent("Context Value Requirement"), false, () => SetRequirement(def, new ContextValueRequirement()));
                menu.AddItem(new GUIContent("Inline Requirement Group (ALL/ANY/NONE)"), false, () => SetRequirement(def, new RequirementGroupInline()));
                menu.ShowAsContext();
            }

            // Add inline reward button
            if (GUILayout.Button("+ Add Inline Reward", GUILayout.Height(24)))
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("Cube Coins Reward"), false, () => AddReward(def, new CubeCoinsReward()));
                menu.AddItem(new GUIContent("XP Reward"), false, () => AddReward(def, new XpReward()));
                menu.AddItem(new GUIContent("Cosmetic Reward"), false, () => AddReward(def, new CosmeticReward()));
                menu.ShowAsContext();
            }

            if (Application.isPlaying && AchievementService.Instance != null)
            {
                EditorGUILayout.Space(12);
                EditorGUILayout.LabelField("Play Mode Debug", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("Force Unlock"))
                    AchievementService.Instance.Unlock(def.AchievementId);

                if (def.Type == AchievementType.Progressive)
                {
                    if (GUILayout.Button("+10 Progress"))
                        AchievementService.Instance.AddProgress(def.AchievementId, 10f);
                }

                EditorGUILayout.EndHorizontal();

                var state = AchievementService.Instance.GetState(def.AchievementId);
                if (state != null)
                {
                    EditorGUILayout.LabelField("Unlocked", state.IsUnlocked ? "YES" : "NO");
                    EditorGUILayout.LabelField("Progress", $"{state.CurrentProgress} / {def.TargetValue}");
                }
            }
        }

        private void SetRequirement(AchievementDefinition def, AchievementRequirement req)
        {
            Undo.RecordObject(def, "Set Inline Requirement");
            def.InlineRequirement = req;
            EditorUtility.SetDirty(def);
        }

        private void AddReward(AchievementDefinition def, AchievementReward reward)
        {
            Undo.RecordObject(def, "Add Inline Reward");
            def.InlineRewards ??= new List<AchievementReward>();
            def.InlineRewards.Add(reward);
            EditorUtility.SetDirty(def);
        }
    }
}
