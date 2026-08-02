using System;
using UnityEngine;

namespace CUBE.Achievements
{
    [Serializable]
    public class ContextValueRequirement : AchievementRequirement
    {
        public string MetadataKey;
        public float ExpectedValue;

        public override bool Evaluate(IRequirementContext context)
        {
            if (context == null || string.IsNullOrEmpty(MetadataKey)) return false;
            if (context.TryGetCustomValue<float>(MetadataKey, out float val))
                return Mathf.Approximately(val, ExpectedValue);
            return false;
        }

        public override string EditorSummary => $"Custom['{MetadataKey}'] == {ExpectedValue}";
    }
}
