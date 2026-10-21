using Photon.Pun;
using UnityEngine;

[CreateAssetMenu(fileName = "LastStand", menuName = "ScriptableObjects/Survivor Abilities/LastStand", order = 1)]
public class LastStand : PassiveAbility
{
    [Tooltip("Base speed for The Healer from her character data.")]
    public float baseSpeed = 35f;

    [Tooltip("Maximum speed multiplier when health is critical.")]
    public float maxSpeedMultiplier = 1.6f;

    [Tooltip("Critical health threshold (0-1) below which emergency effects activate.")]
    public float criticalHealthThreshold = 0.35f;

    [Tooltip("Damage resistance (0-1) granted while Last Stand is active.")]
    public float damageResistance = 0.30f;

    public override void PassiveEffect(BasePlayer player)
    {
        LastStandRunner runner = player.GetComponent<LastStandRunner>();
        if (runner == null)
            runner = player.gameObject.AddComponent<LastStandRunner>();

        runner.Initialize(baseSpeed, maxSpeedMultiplier, criticalHealthThreshold, damageResistance);
    }
}
