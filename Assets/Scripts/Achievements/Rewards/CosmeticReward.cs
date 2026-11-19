using UnityEngine;

namespace CUBE.Achievements
{
    public class CosmeticReward : AchievementReward
    {
        public string ItemCatalogId;

        public override void Grant(AchievementDefinition definition)
        {
            Debug.Log($"[CosmeticReward] Granted cosmetic item '{ItemCatalogId}' for '{definition.DisplayName}'");
        }

        public override string EditorSummary => $"Cosmetic: {ItemCatalogId}";
    }
}
