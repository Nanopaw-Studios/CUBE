using System;
using UnityEngine;

namespace CUBE.Achievements
{
    /// <summary>
    /// Abstract base class for all reusable or inline achievement requirements.
    /// Derived types can be instantiated inline inside <see cref="AchievementDefinition"/> via <c>[SerializeReference]</c>
    /// or wrapped inside ScriptableObject assets when reuse across achievements is desired.
    /// </summary>
    [Serializable]
    public abstract class AchievementRequirement
    {
        public abstract bool Evaluate(IRequirementContext context);
        public virtual string EditorSummary => GetType().Name;
    }
}
