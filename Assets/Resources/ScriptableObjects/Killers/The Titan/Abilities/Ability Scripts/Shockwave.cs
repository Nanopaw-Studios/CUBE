using Photon.Pun;
using UnityEngine;

[CreateAssetMenu(fileName = "Shockwave", menuName = "ScriptableObjects/Killer Abilities/Shockwave", order = 1)]
public class Shockwave : Ability
{
    [Tooltip("Damage dealt to any survivor touching the shockwave.")]
    public int damage = 15;

    [Tooltip("Radius of the shockwave in world units.")]
    public float radius = 6f;

    [Tooltip("Seconds the shockwave persists before disappearing.")]
    public float lifetime = 0.6f;

    public override void ActivateAbility(BasePlayer player)
    {
        base.ActivateAbility(player);

        ShockwaveRunner runner = player.GetComponent<ShockwaveRunner>();
        if (runner == null)
            runner = player.gameObject.AddComponent<ShockwaveRunner>();

        runner.Execute(player, damage, radius, lifetime);
    }
}
