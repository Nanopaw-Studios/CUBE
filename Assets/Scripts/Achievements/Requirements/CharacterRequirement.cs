using System;
using UnityEngine;

namespace CUBE.Achievements
{
    [Serializable]
    public class CharacterRequirement : AchievementRequirement
    {
        public CubePlayerBase RequiredCharacter;

        public override bool Evaluate(IRequirementContext context)
        {
            if (RequiredCharacter == null || context?.Context == null) return false;
            return context.Context.SourceCharacter == RequiredCharacter;
        }

        public override string EditorSummary => $"Character == {(RequiredCharacter != null ? RequiredCharacter._Name : "(null)")}";
    }
}
