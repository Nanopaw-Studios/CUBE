using Photon.Pun;
using UnityEngine;

[RequireComponent(typeof(BasePlayer))]
[RequireComponent(typeof(BasePlayerStats))]
public class PlayerHitboxManager : MonoBehaviourPunCallbacks
{
    public CollisionCallback[] damageColliders;

    public BasePlayer basePlayer;
    public BasePlayerStats stats;

    private static readonly int PlayerLayer = 3; // "Player" layer

    void Start()
    {
        if (basePlayer == null) basePlayer = GetComponent<BasePlayer>();
        if (stats == null) stats = GetComponent<BasePlayerStats>();

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
        // Only the killer can deal collision damage
        if (basePlayer.GetPlayerType() != BasePlayer.PlayerType.Killer) return;

        KillerType killerType = basePlayer.GetEquippedKiller();
        if (killerType == null) return;

        PlayerHitboxManager targetHitbox = collision.collider.GetComponentInParent<PlayerHitboxManager>();
        if (targetHitbox == null) return;

        PhotonView targetView = targetHitbox.photonView;
        if (targetView == null || targetView.IsMine) return;

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
