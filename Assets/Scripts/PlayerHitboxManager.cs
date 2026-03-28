using Photon.Pun;
using UnityEngine;

[RequireComponent(typeof(BasePlayer))]
[RequireComponent(typeof(BasePlayerStats))]
public class PlayerHitboxManager : MonoBehaviourPunCallbacks
{
    public CollisionCallback[] damageColliders;

    public BasePlayer basePlayer;
    public BasePlayerStats stats;

    void Awake()
    {
        if(basePlayer == null || stats == null)
        {
            basePlayer = GetComponent<BasePlayer>();
            stats = GetComponent<BasePlayerStats>();
        }
        foreach(CollisionCallback callback in damageColliders)
        {
            callback.collisionEntered.AddListener(CollisionEntered);
        }
    }

    void CollisionEntered(Collision collision)
    {
        if (!photonView.IsMine) return;

        PhotonView targetView = collision.collider.GetComponentInParent<PhotonView>();

        if (targetView != null && !targetView.IsMine)
        {
            int damage = basePlayer.GetEquippedKiller().PunchDamage;

            targetView.RPC(
                "RPC_OnHit",
                RpcTarget.All,
                damage
            );

            Debug.Log($"Hit player {targetView.Owner.NickName} for {damage}");
        }
    }

    [PunRPC]
    void RPC_OnHit(int damage)
    {
        Debug.Log($"Damage received: {damage}");
        
        stats.health -= damage;
    }

    new void OnDisable()
    {
        foreach(CollisionCallback callback in damageColliders)
        {
            callback.collisionEntered.RemoveListener(CollisionEntered);
        }
    }
}
