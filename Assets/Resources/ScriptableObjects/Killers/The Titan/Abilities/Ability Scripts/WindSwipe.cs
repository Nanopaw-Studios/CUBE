using Photon.Pun;
using UnityEngine;

[CreateAssetMenu(fileName = "WindSwipe", menuName = "ScriptableObjects/Killer Abilities/WindSwipe", order = 1)]
public class WindSwipe : Ability
{
    [Tooltip("Damage dealt to a survivor on contact with the blade projectile.")]
    public int damage = 25;

    [Tooltip("Launch speed of the wind blade in world units per second.")]
    public float speed = 20f;

    [Tooltip("Seconds before the blade is destroyed if it hits nothing.")]
    public float lifetime = 4f;

    public override void ActivateAbility(BasePlayer player)
    {
        base.ActivateAbility(player);
        LaunchBlade(player);
    }

    private void LaunchBlade(BasePlayer player)
    {
        GameObject cam = player.transform.Find("CameraHead").Find("Cam").Find("ShakePivot").Find("C").gameObject;
        Vector3 spawnPos = cam.transform.position + cam.transform.forward * 1.5f;

        GameObject blade = PhotonNetwork.Instantiate("WindSwipePrefab", spawnPos, cam.transform.rotation);

        WindSwipeProjectile projectile = blade.AddComponent<WindSwipeProjectile>();
        projectile.damage = damage;
        projectile.speed = speed;

        PhotonAutoDestroy autoDestroy = blade.AddComponent<PhotonAutoDestroy>();
        autoDestroy.lifetime = lifetime;
    }
}
