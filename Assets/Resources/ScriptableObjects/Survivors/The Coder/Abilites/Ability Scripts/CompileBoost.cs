using Photon.Pun;
using UnityEngine;

[CreateAssetMenu(fileName = "CompileBoost", menuName = "ScriptableObjects/Survivor Abilities/CompileBoost", order = 1)]
public class CompileBoost : Ability
{
    [Tooltip("How long the player is frozen before the boost activates (seconds).")]
    public float freezeDuration = 1f;

    [Tooltip("How long the speed boost lasts after the freeze (seconds).")]
    public float boostDuration = 4f;

    [Tooltip("Multiplier applied to MovementSC.Force during the boost phase.")]
    public float boostMultiplier = 2f;

    public override void ActivateAbility(BasePlayer player)
    {
        base.ActivateAbility(player);

        // Reuse an existing runner if the ability fires again before it finishes.
        CompileBoostRunner runner = player.GetComponent<CompileBoostRunner>();
        if (runner == null)
            runner = player.gameObject.AddComponent<CompileBoostRunner>();

        runner.Execute(freezeDuration, boostDuration, boostMultiplier);
    }
}