using System;
using UnityEngine;

namespace CUBE.Achievements
{
    /// <summary>
    /// Abstract base class for all inline or asset-based achievement rewards.
    /// Can be created inline directly on <see cref="AchievementDefinition"/> via <c>[SerializeReference]</c>.
    /// </summary>
    [Serializable]
    public abstract class AchievementReward
    {
        public abstract void Grant(AchievementDefinition definition);
        public virtual string EditorSummary => GetType().Name;
    }
}
