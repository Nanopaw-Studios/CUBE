using System;
using UnityEngine;

namespace CUBE.Achievements
{
    [Serializable]
    public class RoleRequirement : AchievementRequirement
    {
        public BasePlayer.PlayerType RequiredRole = BasePlayer.PlayerType.Survivor;

        public override bool Evaluate(IRequirementContext context)
        {
            if (context?.Context == null) return false;
            return context.Context.SourceRole == RequiredRole;
        }

        public override string EditorSummary => $"Role == {RequiredRole}";
    }
}
