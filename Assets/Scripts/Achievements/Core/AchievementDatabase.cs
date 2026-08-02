using System.Collections.Generic;
using UnityEngine;

namespace CUBE.Achievements
{
    /// <summary>
    /// Central registry of achievement definitions.
    /// Builds O(1) indices by String ID, GUID, and multiple relevant event types.
    /// </summary>
    [CreateAssetMenu(fileName = "AchievementDatabase", menuName = "CUBE/Achievements/Achievement Database")]
    public class AchievementDatabase : ScriptableObject
    {
        public AchievementDefinition[] Achievements;

        private Dictionary<string, AchievementDefinition> _byId;
        private Dictionary<SerializableGuid, AchievementDefinition> _byGuid;
        private Dictionary<GameplayEventType, List<AchievementDefinition>> _byTrigger;
        private bool _built;

        public AchievementDefinition GetById(string id)
        {
            EnsureBuilt();
            if (string.IsNullOrEmpty(id)) return null;
            _byId.TryGetValue(id, out var def);
            return def;
        }

        public AchievementDefinition GetByGuid(SerializableGuid guid)
        {
            EnsureBuilt();
            _byGuid.TryGetValue(guid, out var def);
            return def;
        }

        public List<AchievementDefinition> GetByTrigger(GameplayEventType type)
        {
            EnsureBuilt();
            if (_byTrigger.TryGetValue(type, out var list))
                return list;
            return _emptyList;
        }

        public int Count
        {
            get { EnsureBuilt(); return _byId.Count; }
        }

        public void RebuildLookup()
        {
            _built = false;
            EnsureBuilt();
        }

        private static readonly List<AchievementDefinition> _emptyList = new();

        private void EnsureBuilt()
        {
            if (_built) return;

            _byId = new Dictionary<string, AchievementDefinition>();
            _byGuid = new Dictionary<SerializableGuid, AchievementDefinition>();
            _byTrigger = new Dictionary<GameplayEventType, List<AchievementDefinition>>();

            if (Achievements == null)
            {
                _built = true;
                return;
            }

            for (int i = 0; i < Achievements.Length; i++)
            {
                var def = Achievements[i];
                if (def == null || string.IsNullOrEmpty(def.AchievementId))
                    continue;

                _byId[def.AchievementId] = def;
                _byGuid[def.InternalGuid] = def;

                if (def.RelevantEvents != null)
                {
                    for (int j = 0; j < def.RelevantEvents.Length; j++)
                    {
                        var evtType = def.RelevantEvents[j];
                        if (evtType == GameplayEventType.None) continue;

                        if (!_byTrigger.TryGetValue(evtType, out var list))
                        {
                            list = new List<AchievementDefinition>();
                            _byTrigger[evtType] = list;
                        }
                        if (!list.Contains(def))
                            list.Add(def);
                    }
                }
            }

            _built = true;
        }

        private void OnEnable() => _built = false;
    }
}
