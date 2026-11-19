using Photon.Pun;
using UnityEngine;

/// <summary>
/// Handles collision-based damage for the GlitchShard projectile.
/// Must be added at runtime by the GlitchShard ability after PhotonNetwork.Instantiate.
/// Only the owner processes collisions to avoid duplicate damage RPCs.
/// </summary>
[RequireComponent(typeof(PhotonView))]
[RequireComponent(typeof(CollisionCallback))]
public class GlitchShardProjectile : MonoBehaviourPun
{
    private const int SurvivorLayer = -1; // resolved via BasePlayer.PlayerType check

    /// <summary>Damage dealt to a survivor on contact.</summary>
    public int damage = 15;

    private CollisionCallback collisionCallback;
    private bool hasHit = false;

    private void Awake()
    {
        collisionCallback = GetComponent<CollisionCallback>();
    }

    private void Start()
    {
        // Only the owning client should process outgoing hits.
        if (photonView.IsMine)
        {
            if (collisionCallback != null)
            {
                collisionCallback.collisionEntered?.AddListener(OnCollisionEntered);
            }
            else
            {
                Debug.LogError("Missing CollisionCallback on GlitchShardProjectile!");
            }
        }
    }

    /// <summary>
    /// Fired by CollisionCallback on the owning client when the shard hits something.
    /// </summary>
    private void OnCollisionEntered(Collision collision)
    {
        if (hasHit)
            return;

        // Walk up the hierarchy to find a PlayerHitboxManager on the target.
        PlayerHitboxManager targetHitbox =
            collision.collider.GetComponentInParent<PlayerHitboxManager>();
        if (targetHitbox == null)
            return;

        // Never deal damage to the killer who threw the shard.
        PhotonView targetView = targetHitbox.photonView;
        if (targetView == null || targetView.IsMine)
            return;

        // Only damage survivors.
        if (
            targetHitbox.basePlayer == null
            || targetHitbox.basePlayer.GetPlayerType() != BasePlayer.PlayerType.Survivor
        )
            return;

        hasHit = true;

        // Cache health before RPC so we can check for killing blow locally.
        BasePlayerStats targetStats = targetHitbox.GetComponent<BasePlayerStats>();
        bool isKillingBlow =
            targetStats != null && targetStats.health - damage <= 0 && targetStats.health > 0;

        targetView.RPC(nameof(PlayerHitboxManager.RPC_OnHit), RpcTarget.All, damage);

        // Apply Glitch Distortion status effect (Tier 2, 5 seconds)
        StatusEffectManager targetFx = targetHitbox.GetComponent<StatusEffectManager>();
        if (targetFx != null)
        {
            targetView.RPC(nameof(StatusEffectManager.RPC_ApplyStatusEffect), RpcTarget.All, (byte)StatusEffectType.GlitchDistortion, (byte)2, 5f);
        }

        Debug.Log($"[GlitchShardProjectile] Hit {targetView.Owner.NickName} for {damage} damage and applied Glitch Distortion.");

        CubeCoinRewardManager.NotifyLocalSuccessfulAbility("glitch_shard_hit", 40);

        if (isKillingBlow)

        {
            // Reward the throwing killer — find their CubeCoinRewardManager via PhotonView owner.
            PhotonView[] allViews = FindObjectsByType<PhotonView>(FindObjectsSortMode.None);
            foreach (PhotonView view in allViews)
            {
                if (view.IsMine && view.Owner == photonView.Owner)
                {
                    CubeCoinRewardManager reward = view.GetComponent<CubeCoinRewardManager>();
                    reward?.OnKilledSurvivor();
                    break;
                }
            }
        }

        // Destroy the shard on all clients immediately after a successful hit.
        if (photonView.IsMine)
        {
            PhotonNetwork.Destroy(gameObject);
        }
    }

    private void OnDisable()
    {
        if (collisionCallback != null)
        {
            collisionCallback.collisionEntered?.RemoveListener(OnCollisionEntered);
        }
    }
}
