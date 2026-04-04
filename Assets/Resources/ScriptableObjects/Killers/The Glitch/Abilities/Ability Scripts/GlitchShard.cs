using Photon.Pun;
using UnityEngine;

[CreateAssetMenu(fileName = "GlitchShard", menuName = "ScriptableObjects/Killer Abilities/GlitchShard", order = 1)]
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
        GameObject shard = PhotonNetwork.Instantiate("GlitchShard", Vector3.zero, Quaternion.identity);

        // Add the auto-destroy component so the shard is cleaned up via
        // PhotonNetwork.Destroy on all clients after its lifetime expires.
        // Using plain Destroy() only removes it locally on the throwing client.
        PhotonAutoDestroy autoDestroy = shard.AddComponent<PhotonAutoDestroy>();
        autoDestroy.lifetime = shardLifetime;

        Rigidbody rb = shard.AddComponent<Rigidbody>();
        rb.sleepThreshold = 0f; // prevent the rigidbody from sleeping mid-flight

        GameObject cam = playerObj.transform.Find("CameraHead").Find("Cam").Find("C").gameObject;

        shard.transform.position = cam.transform.position + cam.transform.forward * 1.5f;
        rb.AddForce(cam.transform.forward * force, ForceMode.Impulse);
        rb.useGravity = false;
    }
}
