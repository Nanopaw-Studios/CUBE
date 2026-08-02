using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace CUBE.Achievements.Editor
{
    public class AchievementValidatorWindow : EditorWindow
    {
        public enum DiagnosticSeverity { Error, Warning, Info }

        public class DiagnosticItem
        {
            public DiagnosticSeverity Severity;
            public string Title;
            public string Description;
            public string ResolutionGuide;
            public UnityEngine.Object TargetAsset;
            public bool IsAutoFixable;
        }

        private AchievementDatabase _targetDatabase;
        private List<DiagnosticItem> _diagnostics = new();
        private Vector2 _scrollPos;

        [MenuItem("Tools/Achievements/Validate Database")]
        public static void OpenWindow()
        {
            var window = GetWindow<AchievementValidatorWindow>("Achievement Validator");
            window.minSize = new Vector2(560, 420);
            window.Show();
            window.FindDefaultDatabase();
            window.RunValidation();
        }

        private void FindDefaultDatabase()
        {
            string[] guids = AssetDatabase.FindAssets("t:AchievementDatabase");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                _targetDatabase = AssetDatabase.LoadAssetAtPath<AchievementDatabase>(path);
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("CUBE Achievement Database Validator", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            _targetDatabase = (AchievementDatabase)EditorGUILayout.ObjectField("Database", _targetDatabase, typeof(AchievementDatabase), false);

            if (GUILayout.Button("Run Validation", GUILayout.Width(120), GUILayout.Height(22)))
            {
                RunValidation();
            }

            GUI.backgroundColor = new Color(0.3f, 0.8f, 0.4f);
            if (GUILayout.Button("Fix Common Issues", GUILayout.Width(140), GUILayout.Height(22)))
            {
                AutoFixCommonIssues();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);

            if (_diagnostics == null || _diagnostics.Count == 0)
            {
                EditorGUILayout.HelpBox("No validation issues found! Database is ready for production.", MessageType.Info);
                return;
            }

            int errors = 0, warnings = 0, infos = 0;
            foreach (var diag in _diagnostics)
            {
                if (diag.Severity == DiagnosticSeverity.Error) errors++;
                else if (diag.Severity == DiagnosticSeverity.Warning) warnings++;
                else infos++;
            }

            EditorGUILayout.LabelField($"Summary: {errors} Errors | {warnings} Warnings | {infos} Infos", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            foreach (var item in _diagnostics)
            {
                MessageType msgType = item.Severity switch
                {
                    DiagnosticSeverity.Error => MessageType.Error,
                    DiagnosticSeverity.Warning => MessageType.Warning,
                    _ => MessageType.Info
                };

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.LabelField($"[{item.Severity.ToString().ToUpper()}] {item.Title}", EditorStyles.boldLabel);

                if (item.TargetAsset != null)
                {
                    if (GUILayout.Button("Select Asset", GUILayout.Width(90)))
                    {
                        Selection.activeObject = item.TargetAsset;
                        EditorGUIUtility.PingObject(item.TargetAsset);
                    }
                }

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.LabelField(item.Description, EditorStyles.wordWrappedLabel);

                if (!string.IsNullOrEmpty(item.ResolutionGuide))
                {
                    EditorGUILayout.Space(2);
                    var style = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.35f, 0.65f, 1f) } };
                    EditorGUILayout.LabelField($"Fix: {item.ResolutionGuide}", style);
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(4);
            }
            EditorGUILayout.EndScrollView();
        }

        public void RunValidation()
        {
            _diagnostics.Clear();

            if (_targetDatabase == null)
            {
                _diagnostics.Add(new DiagnosticItem
                {
                    Severity = DiagnosticSeverity.Error,
                    Title = "Missing Database Reference",
                    Description = "No AchievementDatabase asset assigned.",
                    ResolutionGuide = "Assign an AchievementDatabase asset in the inspector or create one via Assets menu."
                });
                return;
            }

            var achievements = _targetDatabase.Achievements;
            if (achievements == null || achievements.Length == 0)
            {
                _diagnostics.Add(new DiagnosticItem
                {
                    Severity = DiagnosticSeverity.Warning,
                    Title = "Empty Database",
                    Description = "The database contains zero achievements.",
                    TargetAsset = _targetDatabase,
                    ResolutionGuide = "Add AchievementDefinition assets to the database array."
                });
                return;
            }

            var seenIds = new Dictionary<string, AchievementDefinition>();
            var seenGuids = new Dictionary<string, AchievementDefinition>();
            var seenSteam = new Dictionary<string, AchievementDefinition>();
            var seenXbox = new Dictionary<string, AchievementDefinition>();
            var seenPs = new Dictionary<string, AchievementDefinition>();
            var seenEpic = new Dictionary<string, AchievementDefinition>();

            for (int i = 0; i < achievements.Length; i++)
            {
                var def = achievements[i];
                if (def == null)
                {
                    _diagnostics.Add(new DiagnosticItem
                    {
                        Severity = DiagnosticSeverity.Error,
                        Title = "Null Definition Entry",
                        Description = $"Database contains a null entry at index [{i}].",
                        TargetAsset = _targetDatabase,
                        ResolutionGuide = "Click 'Fix Common Issues' to automatically remove null entries.",
                        IsAutoFixable = true
                    });
                    continue;
                }

                // ID Check
                if (string.IsNullOrEmpty(def.AchievementId))
                {
                    _diagnostics.Add(new DiagnosticItem
                    {
                        Severity = DiagnosticSeverity.Error,
                        Title = "Missing Achievement ID",
                        Description = $"Achievement asset '{def.name}' has no string ID.",
                        TargetAsset = def,
                        ResolutionGuide = "Provide a unique string ID (e.g. 'first_kill')."
                    });
                }
                else if (seenIds.TryGetValue(def.AchievementId, out var existingIdDef))
                {
                    _diagnostics.Add(new DiagnosticItem
                    {
                        Severity = DiagnosticSeverity.Error,
                        Title = "Duplicate Achievement ID",
                        Description = $"ID '{def.AchievementId}' is used by both '{def.name}' and '{existingIdDef.name}'.",
                        TargetAsset = def,
                        ResolutionGuide = "Ensure every achievement has a distinct string ID."
                    });
                }
                else
                {
                    seenIds[def.AchievementId] = def;
                }

                // GUID Check
                if (string.IsNullOrEmpty(def.GuidString))
                {
                    _diagnostics.Add(new DiagnosticItem
                    {
                        Severity = DiagnosticSeverity.Warning,
                        Title = "Missing GUID",
                        Description = $"Achievement '{def.name}' has no GuidString.",
                        TargetAsset = def,
                        ResolutionGuide = "Click 'Fix Common Issues' to automatically generate a fresh GUID.",
                        IsAutoFixable = true
                    });
                }
                else if (seenGuids.TryGetValue(def.GuidString, out var existingGuidDef))
                {
                    _diagnostics.Add(new DiagnosticItem
                    {
                        Severity = DiagnosticSeverity.Error,
                        Title = "Duplicate GuidString",
                        Description = $"GUID '{def.GuidString}' is duplicated on '{def.name}' and '{existingGuidDef.name}'.",
                        TargetAsset = def,
                        ResolutionGuide = "Click 'Fix Common Issues' to regenerate duplicate GUIDs.",
                        IsAutoFixable = true
                    });
                }
                else
                {
                    seenGuids[def.GuidString] = def;
                }

                // Whitespace Check
                if (!string.IsNullOrEmpty(def.DisplayName) && Regex.IsMatch(def.DisplayName, @"\s{2,}|^\s|\s$"))
                {
                    _diagnostics.Add(new DiagnosticItem
                    {
                        Severity = DiagnosticSeverity.Warning,
                        Title = "Unnormalized Whitespace in DisplayName",
                        Description = $"DisplayName '{def.DisplayName}' contains leading/trailing or multiple consecutive spaces.",
                        TargetAsset = def,
                        ResolutionGuide = "Click 'Fix Common Issues' to trim and normalize whitespace.",
                        IsAutoFixable = true
                    });
                }

                // Platform ID Duplicate Checks
                CheckPlatformDup(def, def.SteamId, "Steam", seenSteam);
                CheckPlatformDup(def, def.XboxId, "Xbox", seenXbox);
                CheckPlatformDup(def, def.PlayStationId, "PlayStation", seenPs);
                CheckPlatformDup(def, def.EpicId, "Epic", seenEpic);

                // Relevant Events Check
                if (def.RelevantEvents == null || def.RelevantEvents.Length == 0)
                {
                    _diagnostics.Add(new DiagnosticItem
                    {
                        Severity = DiagnosticSeverity.Warning,
                        Title = "No Relevant Events",
                        Description = $"'{def.DisplayName ?? def.name}' has no RelevantEvents specified.",
                        TargetAsset = def,
                        ResolutionGuide = "Add at least one GameplayEventType to RelevantEvents so the system can evaluate it automatically."
                    });
                }

                // Progressive checks
                if (def.Type == AchievementType.Progressive)
                {
                    if (def.TargetValue <= 0f)
                    {
                        _diagnostics.Add(new DiagnosticItem
                        {
                            Severity = DiagnosticSeverity.Error,
                            Title = "Invalid Target Value",
                            Description = $"Progressive achievement '{def.name}' has TargetValue <= 0 ({def.TargetValue}).",
                            TargetAsset = def,
                            ResolutionGuide = "Set TargetValue to a positive number (e.g. 100)."
                        });
                    }
                }

                // Metadata checks
                if (def.Icon == null)
                {
                    _diagnostics.Add(new DiagnosticItem
                    {
                        Severity = DiagnosticSeverity.Warning,
                        Title = "Missing Icon",
                        Description = $"Achievement '{def.DisplayName ?? def.name}' has no Icon sprite assigned.",
                        TargetAsset = def,
                        ResolutionGuide = "Assign an Icon sprite for UI rendering."
                    });
                }

                if (def.InlineRequirement == null && def.RequirementAssetOverride == null)
                {
                    _diagnostics.Add(new DiagnosticItem
                    {
                        Severity = DiagnosticSeverity.Info,
                        Title = "Unconditional Achievement",
                        Description = $"'{def.name}' has no requirements. It will unlock on any assigned event.",
                        TargetAsset = def,
                        ResolutionGuide = "Add inline or asset requirements if this achievement should be conditional."
                    });
                }

                // Inline Rewards Null Check
                if (def.InlineRewards != null && def.InlineRewards.Contains(null))
                {
                    _diagnostics.Add(new DiagnosticItem
                    {
                        Severity = DiagnosticSeverity.Warning,
                        Title = "Null Inline Reward Entry",
                        Description = $"Achievement '{def.name}' contains a null entry in InlineRewards.",
                        TargetAsset = def,
                        ResolutionGuide = "Click 'Fix Common Issues' to remove null reward entries.",
                        IsAutoFixable = true
                    });
                }
            }
        }

        public void AutoFixCommonIssues()
        {
            if (_targetDatabase == null) return;

            int fixedCount = 0;

            // 1. Fix Database array nulls
            if (_targetDatabase.Achievements != null)
            {
                var cleanedList = new List<AchievementDefinition>();
                bool dbDirty = false;
                for (int i = 0; i < _targetDatabase.Achievements.Length; i++)
                {
                    if (_targetDatabase.Achievements[i] != null)
                    {
                        cleanedList.Add(_targetDatabase.Achievements[i]);
                    }
                    else
                    {
                        dbDirty = true;
                        fixedCount++;
                    }
                }

                if (dbDirty)
                {
                    Undo.RecordObject(_targetDatabase, "Clean Database Null Entries");
                    _targetDatabase.Achievements = cleanedList.ToArray();
                    EditorUtility.SetDirty(_targetDatabase);
                }
            }

            // 2. Fix Definition issues
            var seenGuids = new HashSet<string>();

            if (_targetDatabase.Achievements != null)
            {
                foreach (var def in _targetDatabase.Achievements)
                {
                    if (def == null) continue;
                    bool modified = false;

                    // Missing or duplicate GUID
                    if (string.IsNullOrEmpty(def.GuidString) || seenGuids.Contains(def.GuidString))
                    {
                        Undo.RecordObject(def, "Generate Fresh GUID");
                        def.GuidString = Guid.NewGuid().ToString("N");
                        modified = true;
                        fixedCount++;
                    }
                    seenGuids.Add(def.GuidString);

                    // DisplayName Whitespace Normalization
                    if (!string.IsNullOrEmpty(def.DisplayName))
                    {
                        string trimmed = def.DisplayName.Trim();
                        string normalized = Regex.Replace(trimmed, @"\s{2,}", " ");
                        if (normalized != def.DisplayName)
                        {
                            Undo.RecordObject(def, "Normalize DisplayName Whitespace");
                            def.DisplayName = normalized;
                            modified = true;
                            fixedCount++;
                        }
                    }

                    // Platform IDs Whitespace Trim
                    if (!string.IsNullOrEmpty(def.SteamId) && def.SteamId != def.SteamId.Trim())
                    {
                        Undo.RecordObject(def, "Trim SteamId");
                        def.SteamId = def.SteamId.Trim();
                        modified = true;
                        fixedCount++;
                    }
                    if (!string.IsNullOrEmpty(def.XboxId) && def.XboxId != def.XboxId.Trim())
                    {
                        Undo.RecordObject(def, "Trim XboxId");
                        def.XboxId = def.XboxId.Trim();
                        modified = true;
                        fixedCount++;
                    }
                    if (!string.IsNullOrEmpty(def.PlayStationId) && def.PlayStationId != def.PlayStationId.Trim())
                    {
                        Undo.RecordObject(def, "Trim PlayStationId");
                        def.PlayStationId = def.PlayStationId.Trim();
                        modified = true;
                        fixedCount++;
                    }
                    if (!string.IsNullOrEmpty(def.EpicId) && def.EpicId != def.EpicId.Trim())
                    {
                        Undo.RecordObject(def, "Trim EpicId");
                        def.EpicId = def.EpicId.Trim();
                        modified = true;
                        fixedCount++;
                    }

                    // Null Reward Cleanup
                    if (def.InlineRewards != null && def.InlineRewards.Contains(null))
                    {
                        Undo.RecordObject(def, "Remove Null Rewards");
                        def.InlineRewards.RemoveAll(r => r == null);
                        modified = true;
                        fixedCount++;
                    }

                    if (modified)
                    {
                        EditorUtility.SetDirty(def);
                    }
                }
            }

            AssetDatabase.SaveAssets();
            RunValidation();

            ShowNotification(new GUIContent($"Fixed {fixedCount} issues automatically!"));
            Debug.Log($"[AchievementValidator] Auto-fixed {fixedCount} common issues.");
        }

        private void CheckPlatformDup(AchievementDefinition def, string platformId, string platformName, Dictionary<string, AchievementDefinition> seen)
        {
            if (string.IsNullOrEmpty(platformId)) return;
            if (seen.TryGetValue(platformId, out var existing))
            {
                _diagnostics.Add(new DiagnosticItem
                {
                    Severity = DiagnosticSeverity.Error,
                    Title = $"Duplicate {platformName} ID",
                    Description = $"{platformName} ID '{platformId}' is assigned to both '{def.name}' and '{existing.name}'.",
                    TargetAsset = def,
                    ResolutionGuide = $"Ensure platform achievement IDs are unique across all {platformName} assets."
                });
            }
            else
            {
                seen[platformId] = def;
            }
        }
    }
}
