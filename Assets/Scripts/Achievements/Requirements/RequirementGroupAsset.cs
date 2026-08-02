using System.Collections.Generic;
using UnityEngine;

namespace CUBE.Achievements
{
    /// <summary>
    /// Reusable ScriptableObject asset representing a logical group of requirements.
    /// Supports ALL, ANY, and NONE evaluation modes with hierarchical nesting.
    /// Can be saved as a project asset and assigned across multiple <see cref="AchievementDefinition"/>s.
    /// </summary>
    [CreateAssetMenu(fileName = "NewRequirementGroup", menuName = "CUBE/Achievements/Requirements/Requirement Group Asset")]
    public class RequirementGroupAsset : ScriptableObject
    {
        public enum GroupOperator
        {
            All,  // AND
            Any,  // OR
            None  // NOT
        }

        [Tooltip("Logical operator applied to the child requirements in this group.")]
        public GroupOperator Operator = GroupOperator.All;

        [Tooltip("Child requirements or nested requirement groups contained in this group.")]
        [SerializeReference]
        public List<AchievementRequirement> SubRequirements = new();

        public bool Evaluate(IRequirementContext context)
        {
            if (SubRequirements == null || SubRequirements.Count == 0)
                return true;

            switch (Operator)
            {
                case GroupOperator.All:
                    for (int i = 0; i < SubRequirements.Count; i++)
                    {
                        if (SubRequirements[i] == null) continue;
                        if (!SubRequirements[i].Evaluate(context)) return false;
                    }
                    return true;

                case GroupOperator.Any:
                    for (int i = 0; i < SubRequirements.Count; i++)
                    {
                        if (SubRequirements[i] == null) continue;
                        if (SubRequirements[i].Evaluate(context)) return true;
                    }
                    return false;

                case GroupOperator.None:
                    for (int i = 0; i < SubRequirements.Count; i++)
                    {
                        if (SubRequirements[i] == null) continue;
                        if (SubRequirements[i].Evaluate(context)) return false;
                    }
                    return true;

                default:
                    return false;
            }
        }

        public string EditorSummary => $"{Operator.ToString().ToUpper()} Group ({SubRequirements?.Count ?? 0} items)";
    }
}
