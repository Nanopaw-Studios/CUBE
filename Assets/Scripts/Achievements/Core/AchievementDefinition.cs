using System;
using System.Collections.Generic;
using UnityEngine;

namespace CUBE.Achievements
{
    /// <summary>
    /// Data-driven ScriptableObject for an achievement.
    /// Supports 1-file single-asset authoring: platform IDs, requirements, and rewards can all be authored directly inline.
    /// Reusable assets (RequirementGroupAsset / PlatformMappingAsset) remain supported when sharing across achievements is explicitly desired.
    /// </summary>
    [CreateAssetMenu(fileName = "New Achievement", menuName = "CUBE/Achievements/Achievement Definition")]
    public class AchievementDefinition : ScriptableObject
    {
        [Header("Identity & Stable Versioning")]
        [Tooltip("Stable 128-bit internal GUID.")]
        public string GuidString = System.Guid.NewGuid().ToString("N");

        [Tooltip("Unique string ID (e.g. 'untouchable').")]
        public string AchievementId;

        [Tooltip("Schema version for definition evolution.")]
        public int Version = 1;

        public string DisplayName;
        [TextArea(2, 5)] public string Description;
        public Sprite Icon;
        public bool IsHidden;
        public AchievementCategory Category = AchievementCategory.General;

        [Header("Evaluation & Triggers")]
        public AchievementType Type = AchievementType.OneShot;
        public EvaluationMode Mode = EvaluationMode.Immediate;

        [Tooltip("All gameplay events that trigger evaluation.")]
        public GameplayEventType[] RelevantEvents;

        public float TargetValue = 1f;
        public StatId LinkedStat = StatId.None;
        public StatScope LinkedStatScope = StatScope.Lifetime;

        [Header("Inline Platform Mapping")]
        public string SteamId;
        public string XboxId;
        public string PlayStationId;
        public string EpicId;
        public string PlayFabId;

        [Tooltip("Optional shared platform mapping asset. Overrides inline IDs if assigned.")]
        public PlatformMappingAsset PlatformMappingAssetOverride;

        [Header("Inline Requirements Engine")]
        [SerializeReference]
        public AchievementRequirement InlineRequirement;

        [Tooltip("Optional shared requirement group asset. Evaluated alongside InlineRequirement if assigned.")]
        public RequirementGroupAsset RequirementAssetOverride;

        [Header("Inline Rewards")]
        [SerializeReference]
        public List<AchievementReward> InlineRewards = new();

        [Header("Flags & Debug")]
        public bool IsRepeatable;
        public bool IsDeveloperOnly;
        public bool EnableDebugLog;

        public SerializableGuid InternalGuid => SerializableGuid.Parse(GuidString);

        public string GetPlatformId(string platformName)
        {
            if (PlatformMappingAssetOverride != null)
                return PlatformMappingAssetOverride.GetPlatformId(platformName);

            return platformName switch
            {
                "Steam"       => SteamId,
                "Xbox"        => XboxId,
                "PlayStation" => PlayStationId,
                "Epic"        => EpicId,
                "PlayFab"     => !string.IsNullOrEmpty(PlayFabId) ? PlayFabId : AchievementId,
                _             => AchievementId
            };
        }

        public bool EvaluateRequirements(IRequirementContext context)
        {
            if (InlineRequirement != null && !InlineRequirement.Evaluate(context))
                return false;

            if (RequirementAssetOverride != null && !RequirementAssetOverride.Evaluate(context))
                return false;

            return true;
        }

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(GuidString))
                GuidString = System.Guid.NewGuid().ToString("N");
        }
    }
}
