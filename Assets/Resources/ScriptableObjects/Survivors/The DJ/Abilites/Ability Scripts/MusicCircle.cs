using Photon.Pun;
using UnityEngine;

[CreateAssetMenu(fileName = "MusicCircle", menuName = "ScriptableObjects/Survivor Abilities/MusicCircle", order = 1)]
public class MusicCircle : Ability
{
    [Tooltip("How long the speed boost lasts on survivors who step in the circle.")]
    public float boostDuration = 4f;

    [Tooltip("Speed multiplier applied to survivors inside the circle.")]
    public float boostMultiplier = 1.5f;

    [Tooltip("Radius of the Music Circle effect.")]
    public float radius = 5f;

    [Tooltip("How long the Music Circle persists in the world.")]
    public float circleLifetime = 12f;

    [Tooltip("Seconds of startup before the circle is placed (killer can cancel this window).")]
    public float startupDuration = 2f;

    public override void ActivateAbility(BasePlayer player)
    {
        base.ActivateAbility(player);

        MusicCircleAbility runner = player.GetComponent<MusicCircleAbility>();
        if (runner == null)
            runner = player.gameObject.AddComponent<MusicCircleAbility>();

        runner.Execute(player, boostDuration, boostMultiplier, radius, circleLifetime, startupDuration);
    }
}
