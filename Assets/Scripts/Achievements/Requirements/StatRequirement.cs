using System;
using UnityEngine;

namespace CUBE.Achievements
{
    [Serializable]
    public class StatRequirement : AchievementRequirement
    {
        public enum ComparisonOp { GreaterThanOrEqual, LessThanOrEqual, Equal, NotEqual }

        public StatId Stat = StatId.None;
        public StatScope Scope = StatScope.Lifetime;
        public ComparisonOp Operator = ComparisonOp.GreaterThanOrEqual;
        public float Threshold;

        public override bool Evaluate(IRequirementContext context)
        {
            if (Stat == StatId.None || context == null) return false;
            float val = context.GetStat(Stat, Scope);

            return Operator switch
            {
                ComparisonOp.GreaterThanOrEqual => val >= Threshold,
                ComparisonOp.LessThanOrEqual    => val <= Threshold,
                ComparisonOp.Equal              => Mathf.Approximately(val, Threshold),
                ComparisonOp.NotEqual           => !Mathf.Approximately(val, Threshold),
                _                               => false
            };
        }

        public override string EditorSummary => $"{Stat} ({Scope}) {Operator} {Threshold}";
    }
}
