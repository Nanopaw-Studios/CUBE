using System;
using System.Collections.Generic;
using UnityEngine;

namespace CUBE.Achievements
{
    /// <summary>
    /// Implementation of <see cref="IRewardService"/>.
    /// Executes rewards through a single pipeline, dispatches events for UI notifications and telemetry.
    /// </summary>
    public class RewardService : MonoBehaviour
    {
        public static RewardService Instance { get; private set; }

        public event Action<AchievementReward, AchievementDefinition> OnRewardGranted;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void GrantRewards(IEnumerable<AchievementReward> rewards, AchievementDefinition definition)
        {
            if (rewards == null || definition == null) return;

            foreach (var reward in rewards)
            {
                if (reward == null) continue;
                try
                {
                    reward.Grant(definition);
                    OnRewardGranted?.Invoke(reward, definition);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }
    }
}
