using System;
using UnityEngine;

namespace CUBE.Achievements
{
    [Serializable]
    public class EventPropertyRequirement : AchievementRequirement
    {
        public enum TargetProperty { DamageAmount, HealAmount, Distance, Duration, AirTime, CoinAmount, MasteryLevel, Health, Speed }
        public enum ComparisonOp { GreaterThanOrEqual, LessThanOrEqual, Equal, NotEqual }

        public TargetProperty Property = TargetProperty.DamageAmount;
        public ComparisonOp Operator = ComparisonOp.GreaterThanOrEqual;
        public float Threshold;

        public override bool Evaluate(IRequirementContext context)
        {
            if (context?.Context == null) return false;

            float val = Property switch
            {
                TargetProperty.DamageAmount => context.Context.DamageAmount,
                TargetProperty.HealAmount   => context.Context.HealAmount,
                TargetProperty.Distance     => context.Context.Distance,
                TargetProperty.Duration     => context.Context.Duration,
                TargetProperty.AirTime      => context.Context.AirTime,
                TargetProperty.CoinAmount   => context.Context.CoinAmount,
                TargetProperty.MasteryLevel => context.Context.MasteryLevel,
                TargetProperty.Health       => context.Context.Health,
                TargetProperty.Speed        => context.Context.Speed,
                _                           => 0f
            };

            return Operator switch
            {
                ComparisonOp.GreaterThanOrEqual => val >= Threshold,
                ComparisonOp.LessThanOrEqual    => val <= Threshold,
                ComparisonOp.Equal              => Mathf.Approximately(val, Threshold),
                ComparisonOp.NotEqual           => !Mathf.Approximately(val, Threshold),
                _                               => false
            };
        }

        public override string EditorSummary => $"Context.{Property} {Operator} {Threshold}";
    }
}
