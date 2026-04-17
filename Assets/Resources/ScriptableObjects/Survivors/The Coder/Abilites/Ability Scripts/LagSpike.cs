using Photon.Pun;
using UnityEngine;

[CreateAssetMenu(fileName = "LagSpike", menuName = "ScriptableObjects/Survivor Abilities/LagSpike", order = 1)]
public class LagSpike : Ability
{
    public float force = 400f;
    public float shardLifetime = 5f;
    public int damage = 15;

    public override void ActivateAbility(BasePlayer player)
    {
        base.ActivateAbility(player);
        ThrowShard(player);
    }

    private void ThrowShard(BasePlayer player)
    {
        GameObject cam = player.transform.Find("CameraHead").Find("Cam").Find("C").gameObject;

        GameObject shard = PhotonNetwork.Instantiate("GlitchShard", cam.transform.position + cam.transform.forward * 1.5f, Quaternion.identity);

        // Wire up network auto-destroy so it cleans up on all clients.
        PhotonAutoDestroy autoDestroy = shard.AddComponent<PhotonAutoDestroy>();
        autoDestroy.lifetime = shardLifetime;

        // Wire up collision and damage — mirrors GlitchShard ability setup.
        shard.AddComponent<CollisionCallback>();
        GlitchShardProjectile projectile = shard.AddComponent<GlitchShardProjectile>();
        projectile.damage = damage;

        Rigidbody rb = shard.AddComponent<Rigidbody>();
        rb.sleepThreshold = 0f;
        rb.useGravity = false;
        rb.AddForce(cam.transform.forward * force, ForceMode.Impulse);
    }
}
