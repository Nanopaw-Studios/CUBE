using Photon.Pun;
using UnityEngine;

[CreateAssetMenu(fileName = "Equalizer", menuName = "ScriptableObjects/Survivor Abilities/Equalizer", order = 1)]
public class Equalizer : Ability
{
    [Tooltip("Speed multiplier applied to the killer while inside the wall's effect zone (e.g. 0.4 = 60% slower).")]
    public float killerSlowMultiplier = 0.4f;

    [Tooltip("How long the Equalizer wall persists in the world (seconds).")]
    public float wallLifetime = 10f;

    [Tooltip("Startup seconds before the wall is placed. Killer can damage the caster to cancel.")]
    public float startupDuration = 1f;

    public override void ActivateAbility(BasePlayer player)
    {
        base.ActivateAbility(player);

        EqualizerAbility runner = player.GetComponent<EqualizerAbility>();
        if (runner == null)
            runner = player.gameObject.AddComponent<EqualizerAbility>();

        runner.Execute(player, killerSlowMultiplier, wallLifetime, startupDuration);
    }
}
