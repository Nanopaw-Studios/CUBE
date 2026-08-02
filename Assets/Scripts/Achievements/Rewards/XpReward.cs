using UnityEngine;

namespace CUBE.Achievements
{
    [CreateAssetMenu(fileName = "XpReward", menuName = "CUBE/Achievements/Rewards/XP Reward")]
    public class XpReward : AchievementReward
    {
        public int ExperienceAmount = 500;

        public override void Grant(AchievementDefinition definition)
        {
            Debug.Log($"[XpReward] Granted {ExperienceAmount} XP for '{definition.DisplayName}'");
        }

        public override string EditorSummary => $"{ExperienceAmount} XP";
    }
}
