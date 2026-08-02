using System.Collections.Generic;

namespace CUBE.Achievements
{
    [System.Serializable]
    public class AchievementSaveData
    {
        public int Version = 1;
        public string PlayerId;
        public List<AchievementRuntimeState> States = new();

        public Dictionary<string, AchievementRuntimeState> ToDictionary()
        {
            var dict = new Dictionary<string, AchievementRuntimeState>();
            if (States == null) return dict;

            for (int i = 0; i < States.Count; i++)
            {
                var s = States[i];
                if (s == null || string.IsNullOrEmpty(s.AchievementId)) continue;
                dict[s.AchievementId] = s;
            }
            return dict;
        }

        public void FromDictionary(Dictionary<string, AchievementRuntimeState> dict)
        {
            States.Clear();
            foreach (var kvp in dict)
                States.Add(kvp.Value);
        }
    }
}
