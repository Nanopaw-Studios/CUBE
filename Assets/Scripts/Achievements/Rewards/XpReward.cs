using UnityEngine;

namespace CUBE.Achievements
{
    public class XpReward : AchievementReward
    {
        public int ExperienceAmount = 500;

        public override void Grant(AchievementDefinition definition)
        {
            if (ExperienceAmount <= 0) return;

            ProgressionManager prog = ProgressionManager.EnsureExists();
            if (prog != null)
            {
                string achId = definition != null ? definition.AchievementId : "achievement";
                prog.AddAccountXP(ExperienceAmount, $"achievement_{achId}");
            }

            Debug.Log($"[XpReward] Granted {ExperienceAmount} XP for '{definition?.DisplayName}'");
        }

        public override string EditorSummary => $"{ExperienceAmount} XP";
    }
}

