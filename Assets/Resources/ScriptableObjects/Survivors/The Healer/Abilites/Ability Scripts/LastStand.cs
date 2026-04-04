using Photon.Pun;
using UnityEngine;

[CreateAssetMenu(fileName = "LastStand", menuName = "ScriptableObjects/Survivor Abilities/LastStand", order = 1)]
public class LastStand : PassiveAbility
{
    [Tooltip("Base speed for The Healer from her character data.")]
    public float baseSpeed = 35f;

    [Tooltip("Maximum speed multiplier when the team's average health is at its lowest.")]
    public float maxSpeedMultiplier = 2f;

    public override void PassiveEffect(BasePlayer player)
    {
        if (player.GetComponent<LastStandRunner>() == null)
            player.gameObject.AddComponent<LastStandRunner>().Initialize(baseSpeed, maxSpeedMultiplier);
    }
}
