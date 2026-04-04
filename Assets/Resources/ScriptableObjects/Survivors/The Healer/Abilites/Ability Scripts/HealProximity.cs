using Photon.Pun;
using UnityEngine;

[CreateAssetMenu(fileName = "HealProximity", menuName = "ScriptableObjects/Survivor Abilities/HealProximity", order = 1)]
public class HealProximity : Ability
{
    [Tooltip("Amount of health restored to the closest survivor.")]
    public int healAmount = 20;

    [Tooltip("Seconds of startup delay before the heal is applied, during which the killer can cancel it.")]
    public float startupDuration = 2f;

    public override void ActivateAbility(BasePlayer player)
    {
        base.ActivateAbility(player);

        HealProximityRunner runner = player.GetComponent<HealProximityRunner>();
        if (runner == null)
            runner = player.gameObject.AddComponent<HealProximityRunner>();

        runner.Execute(healAmount, startupDuration, player);
    }
}
