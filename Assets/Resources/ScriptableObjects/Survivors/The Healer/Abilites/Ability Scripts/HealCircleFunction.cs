using Photon.Pun;
using UnityEngine;

public class HealCircleFunction : MonoBehaviourPunCallbacks
{
    public BasePlayer creator;
    public float healAmount = 2f;
    public float healInterval = 1f;
    public float radius = 5f;
    public float lifetime = 15f;

    private int totalRestoredHp = 0;
    private bool rewardedForThisCircle = false;


    private void Start()
    {
        // Only the owner ticks and destroys
        if (!photonView.IsMine) return;

        InvokeRepeating(nameof(HealSurvivors), healInterval, healInterval);
        Invoke(nameof(NetworkDestroy), lifetime);
    }

    /// <summary>
    /// Scans for survivors in range and heals them via RPC on their own PhotonView.
    /// Only runs on the owner client.
    /// </summary>
    public void HealSurvivors()
    {
        if (!photonView.IsMine) return;

        Collider[] hitColliders = Physics.OverlapSphere(transform.position, radius);
        foreach (var hitCollider in hitColliders)
        {
            BasePlayer player = hitCollider.GetComponentInParent<BasePlayer>();
            if (player == null) continue;
            if (player.GetPlayerType() != BasePlayer.PlayerType.Survivor) continue;

            // Never heal a dead survivor: they remain collapsed/spectating until
            // intermission, and restoring health would prevent the killer's
            // elimination win condition from ever succeeding.
            BasePlayerStats targetStats = player.GetComponent<BasePlayerStats>();
            if (targetStats == null || targetStats.health <= 0) continue;

            PhotonView targetPV = player.gameObject.GetPhotonView();
            if (targetPV == null) continue;

            int missingHp = Mathf.Max(0, targetStats.maxHealth - targetStats.health);
            int actualRestored = Mathf.Min(missingHp, (int)healAmount);

            // Call Heal RPC on the target player's BasePlayerStats
            targetPV.RPC(nameof(BasePlayerStats.Heal), RpcTarget.All, (int)healAmount);

            if (actualRestored > 0 && !rewardedForThisCircle)
            {
                totalRestoredHp += actualRestored;
                if (totalRestoredHp >= 10)
                {
                    rewardedForThisCircle = true;
                    CubeCoinRewardManager.NotifyLocalSuccessfulAbility("heal_circle_restored", 35);
                }
            }


            // Grant Regeneration and Sanctuary status effects to survivors in the circle
            StatusEffectManager targetFx = player.GetComponent<StatusEffectManager>();
            if (targetFx != null)
            {
                targetPV.RPC(nameof(StatusEffectManager.RPC_ApplyStatusEffect), RpcTarget.All, (byte)StatusEffectType.Regeneration, (byte)1, 3f);
                targetPV.RPC(nameof(StatusEffectManager.RPC_ApplyStatusEffect), RpcTarget.All, (byte)StatusEffectType.Sanctuary, (byte)1, 3f);
            }
        }
    }

    private void NetworkDestroy()
    {
        if (photonView.IsMine)
            PhotonNetwork.Destroy(gameObject);
    }
}