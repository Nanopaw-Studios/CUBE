using System.Collections.Generic;

namespace CUBE.Achievements
{
    /// <summary>
    /// Serialisable container for all lifetime statistics.
    /// Written to / read from JSON by <see cref="StatisticsSaveManager"/>.
    /// </summary>
    [System.Serializable]
    public class StatisticsSaveData
    {
        /// <summary>Schema version.</summary>
        public int Version = 1;

        /// <summary>Player identifier.</summary>
        public string PlayerId;

        /// <summary>
        /// All stat entries.  Stored as a list for JSON serialisation;
        /// converted to a dictionary at runtime.
        /// </summary>
        public List<StatEntry> Entries = new List<StatEntry>();

        [System.Serializable]
        public class StatEntry
        {
            public StatId Id;
            public float Value;

            public StatEntry() { }
            public StatEntry(StatId id, float value)
            {
                Id = id;
                Value = value;
            }
        }

        // ── Conversion ────────────────────────────────────────────────

        public Dictionary<StatId, float> ToDictionary()
        {
            var dict = new Dictionary<StatId, float>();
            if (Entries == null) return dict;

            for (int i = 0; i < Entries.Count; i++)
            {
                var e = Entries[i];
                if (e == null) continue;
                dict[e.Id] = e.Value;
            }
            return dict;
        }

        public void FromDictionary(Dictionary<StatId, float> dict)
        {
            Entries.Clear();
            foreach (var kvp in dict)
                Entries.Add(new StatEntry(kvp.Key, kvp.Value));
        }
    }
}
