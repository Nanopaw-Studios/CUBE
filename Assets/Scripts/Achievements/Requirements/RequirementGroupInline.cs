using System;
using System.Collections.Generic;
using UnityEngine;

namespace CUBE.Achievements
{
    [Serializable]
    public class RequirementGroupInline : AchievementRequirement
    {
        public enum GroupOperator { All, Any, None }

        public GroupOperator Operator = GroupOperator.All;

        [SerializeReference]
        public List<AchievementRequirement> Requirements = new();

        public override bool Evaluate(IRequirementContext context)
        {
            if (Requirements == null || Requirements.Count == 0) return true;

            switch (Operator)
            {
                case GroupOperator.All:
                    for (int i = 0; i < Requirements.Count; i++)
                    {
                        if (Requirements[i] == null) continue;
                        if (!Requirements[i].Evaluate(context)) return false;
                    }
                    return true;

                case GroupOperator.Any:
                    for (int i = 0; i < Requirements.Count; i++)
                    {
                        if (Requirements[i] == null) continue;
                        if (Requirements[i].Evaluate(context)) return true;
                    }
                    return false;

                case GroupOperator.None:
                    for (int i = 0; i < Requirements.Count; i++)
                    {
                        if (Requirements[i] == null) continue;
                        if (Requirements[i].Evaluate(context)) return false;
                    }
                    return true;

                default:
                    return false;
            }
        }

        public override string EditorSummary => $"{Operator.ToString().ToUpper()} Group ({Requirements?.Count ?? 0} inline items)";
    }
}
