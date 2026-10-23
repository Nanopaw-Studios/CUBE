using Photon.Pun;
using UnityEngine;

/// <summary>
/// Drives the WindSwipe blade projectile forward and handles survivor collision damage.
/// Added at runtime by the WindSwipe ability after PhotonNetwork.Instantiate.
/// Only the owning client processes physics and damage to avoid duplicate RPCs.
/// </summary>
[RequireComponent(typeof(PhotonView))]
public class WindSwipeProjectile : MonoBehaviourPun
{
    public int damage = 25;
    public float speed = 20f;

    private bool hasHit = false;
    private Rigidbody rb;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody>();

        rb.useGravity = false;
        rb.linearVelocity = transform.forward * speed;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!photonView.IsMine) return;
        if (hasHit) return;

        PlayerHitboxManager targetHitbox = collision.collider.GetComponentInParent<PlayerHitboxManager>();
        if (targetHitbox == null) return;

        PhotonView targetPV = targetHitbox.photonView;
        if (targetPV == null || targetPV.IsMine) return;

        // Only damage survivors
        if (targetHitbox.basePlayer == null ||
            targetHitbox.basePlayer.GetPlayerType() != BasePlayer.PlayerType.Survivor) return;

        hasHit = true;

        BasePlayerStats targetStats = targetHitbox.GetComponent<BasePlayerStats>();
        bool isKillingBlow = targetStats != null && targetStats.health - damage <= 0 && targetStats.health > 0;

        targetPV.RPC(nameof(PlayerHitboxManager.RPC_OnHit), RpcTarget.All, damage);

        // Apply WindStagger status effect (Tier 2, 3 seconds)
        StatusEffectManager targetFx = targetHitbox.GetComponent<StatusEffectManager>();
        if (targetFx != null)
        {
            targetPV.RPC(nameof(StatusEffectManager.RPC_ApplyStatusEffect), RpcTarget.All, (byte)StatusEffectType.WindStagger, (byte)2, 3f);
        }

        Debug.Log($"[WindSwipe] Hit {targetPV.Owner.NickName} for {damage} damage and applied WindStagger.");

        if (isKillingBlow)
        {
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

        PhotonNetwork.Destroy(gameObject);
    }
}
