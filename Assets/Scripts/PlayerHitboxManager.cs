using Photon.Pun;
using UnityEngine;

[RequireComponent(typeof(BasePlayer))]
[RequireComponent(typeof(BasePlayerStats))]
public class PlayerHitboxManager : MonoBehaviourPunCallbacks
{
    [Tooltip("Bone colliders that register hits. If empty, all CollisionCallback components in children are used automatically.")]
    public CollisionCallback[] damageColliders;

    public BasePlayer basePlayer;
    public BasePlayerStats stats;

    private static readonly int PlayerLayer = 3; // "Player" layer

    void Start()
    {
        if (basePlayer == null) basePlayer = GetComponent<BasePlayer>();
        if (stats == null) stats = GetComponent<BasePlayerStats>();

        // Auto-discover all CollisionCallback children when none are assigned in the inspector.
        if (damageColliders == null || damageColliders.Length == 0)
        {
            damageColliders = GetComponentsInChildren<CollisionCallback>(true);
            Debug.Log($"[HitboxManager] Auto-discovered {damageColliders.Length} CollisionCallback(s) on {gameObject.name}");
        }

        // Ensure hitbox collider GameObjects are on the Player layer so physics
        // collision matrix works and OverlapBox queries find them correctly.
        foreach (CollisionCallback callback in damageColliders)
        {
            if (callback == null) continue;
            callback.gameObject.layer = PlayerLayer;
        }

        // Only the local owner's hitboxes should fire outgoing hits.
        if (photonView.IsMine)
        {
            foreach (CollisionCallback callback in damageColliders)
            {
                if (callback != null)
                    callback.collisionEntered.AddListener(CollisionEntered);
            }
        }
    }

    void CollisionEntered(Collision collision)
    {
        // Only the killer can deal collision damage.
        if (basePlayer.GetPlayerType() != BasePlayer.PlayerType.Killer) return;

        KillerType killerType = basePlayer.GetEquippedKiller();
        if (killerType == null) return;

        // Walk up the collider's hierarchy to find the target PlayerHitboxManager.
        // Using GetComponentInParent covers bone-deep colliders attached to any
        // child of the target player's root.
        PlayerHitboxManager targetHitbox = collision.collider.GetComponentInParent<PlayerHitboxManager>();
        if (targetHitbox == null) return;

        // Never hit yourself.
        PhotonView targetView = targetHitbox.photonView;
        if (targetView == null || targetView.IsMine) return;

        // Guard: targets must be survivors.
        if (targetHitbox.basePlayer != null &&
            targetHitbox.basePlayer.GetPlayerType() != BasePlayer.PlayerType.Survivor) return;

        int damage = killerType.PunchDamage;
        targetView.RPC(nameof(RPC_OnHit), RpcTarget.All, damage);
        Debug.Log($"[HitboxManager] Collision hit: {targetView.Owner.NickName} for {damage}");
    }

    /// <summary>
    /// Called via RPC on all clients when this player is hit.
    /// </summary>
    [PunRPC]
    public void RPC_OnHit(int damage)
    {
        Debug.Log($"[HitboxManager] {gameObject.name} received {damage} damage.");
        stats.TakeDamage(damage);
    }

    new void OnDisable()
    {
        foreach (CollisionCallback callback in damageColliders)
        {
            if (callback != null)
                callback.collisionEntered.RemoveListener(CollisionEntered);
        }
    }
}

