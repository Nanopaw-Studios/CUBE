using UnityEngine;

namespace CUBE.Achievements
{
    [CreateAssetMenu(fileName = "CubeCoinsReward", menuName = "CUBE/Achievements/Rewards/Cube Coins")]
    public class CubeCoinsReward : AchievementReward
    {
        [Tooltip("Amount of Cube Coins to grant.")]
        public int Amount = 100;

        public override void Grant(AchievementDefinition definition)
        {
            if (Amount <= 0) return;
            if (PlayfabManager.Instance != null)
            {
                PlayfabManager.Instance.AddCubeCoins(Amount, $"achievement_{definition.AchievementId}");
                Debug.Log($"[CubeCoinsReward] Granted {Amount} Cube Coins for '{definition.DisplayName}'");
            }
        }

        public override string EditorSummary => $"{Amount} Cube Coins";
    }
}
