using System;
using UnityEngine;

namespace CUBE.Achievements
{
    /// <summary>
    /// Decoupled ScriptableObject mapping platform-specific achievement/trophy IDs to an achievement.
    /// Allows <see cref="AchievementDefinition"/> assets to remain 100% platform-agnostic.
    /// </summary>
    [CreateAssetMenu(fileName = "NewPlatformMapping", menuName = "CUBE/Achievements/Platform Mapping")]
    public class PlatformMappingAsset : ScriptableObject
    {
        [Tooltip("Steam achievement API name (e.g. ACH_FIRST_KILL).")]
        public string SteamId;

        [Tooltip("Xbox Live achievement ID.")]
        public string XboxId;

        [Tooltip("PlayStation trophy ID.")]
        public string PlayStationId;

        [Tooltip("Epic Online Services achievement ID.")]
        public string EpicId;

        [Tooltip("PlayFab achievement ID.")]
        public string PlayFabId;

        public string GetPlatformId(string platformName)
        {
            return platformName switch
            {
                "Steam"       => SteamId,
                "Xbox"        => XboxId,
                "PlayStation" => PlayStationId,
                "Epic"        => EpicId,
                "PlayFab"     => PlayFabId,
                _             => null
            };
        }
    }
}
