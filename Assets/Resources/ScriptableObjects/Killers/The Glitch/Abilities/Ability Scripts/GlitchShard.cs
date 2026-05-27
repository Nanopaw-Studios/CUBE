using Photon.Pun;
using UnityEngine;

[CreateAssetMenu(
    fileName = "GlitchShard",
    menuName = "ScriptableObjects/Killer Abilities/GlitchShard",
    order = 1
)]
public class GlitchShard : Ability
{
    public Mesh cubeMesh;

    public float force = 500f;
    public int damage = 15;

    /// <summary>Lifetime in seconds before the shard is destroyed on all clients.</summary>
    public float shardLifetime = 5f;

    GameObject playerObj;

    public override void ActivateAbility(BasePlayer player)
    {
        base.ActivateAbility(player);
        playerObj = player.gameObject;
        ThrowShard();
    }

    public void ThrowShard()
    {
        GameObject shard = PhotonNetwork.Instantiate(
            "GlitchShard",
            Vector3.zero,
            Quaternion.identity
        );

        PhotonAutoDestroy autoDestroy = shard.AddComponent<PhotonAutoDestroy>();
        autoDestroy.lifetime = shardLifetime;

        CollisionCallback collision = shard.AddComponent<CollisionCallback>();

        GlitchShardProjectile projectile = shard.AddComponent<GlitchShardProjectile>();
        projectile.damage = damage;

        Rigidbody rb = shard.AddComponent<Rigidbody>();
        rb.sleepThreshold = 0f;
        rb.useGravity = false;

        Transform c = playerObj
            .transform.Find("CameraHead")
            ?.Find("Cam")
            ?.Find("ShakePivot")
            ?.Find("C");

        if (c == null)
        {
            Debug.LogError("[GlitchShard] Camera transform not found!");
            PhotonNetwork.Destroy(shard);
            return;
        }

        shard.transform.position = c.position + c.forward * 1.5f;

        rb.AddForce(c.forward * force, ForceMode.Impulse);
    }
}
