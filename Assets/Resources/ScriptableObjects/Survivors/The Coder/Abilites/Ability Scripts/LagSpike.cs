using Photon.Pun;
using UnityEngine;

[CreateAssetMenu(fileName = "LagSpike", menuName = "ScriptableObjects/Survivor Abilities/LagSpike", order = 1)]
public class LagSpike : Ability
{
    [Header("Trap")]
    [Tooltip("How far in front of the caster the trap is placed.")]
    public float throwDistance = 3f;

    [Tooltip("Radius of the slow field.")]
    public float radius = 4f;

    [Tooltip("Status effect tier applied to the killer.")]
    [Range(1, 3)]
    public int tier = 2;

    [Tooltip("How long the killer is slowed for, in seconds.")]
    public float slowDuration = 4f;

    [Tooltip("How long the trap remains on the ground, in seconds.")]
    public float trapLifetime = 12f;

    public override void ActivateAbility(BasePlayer player)
    {
        base.ActivateAbility(player);

        if (player == null || !player.gameObject.GetPhotonView().IsMine)
            return;

        Vector3 center = player.transform.position + player.transform.forward * throwDistance;

        // Snap the trap to the ground so it is visible/usable on slopes and platforms.
        if (Physics.Raycast(center + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 10f, ~0, QueryTriggerInteraction.Ignore))
            center = hit.point;

        // The runner is attached to every player in BasePlayer.Awake so the
        // PUN visual RPC can be resolved on every client, not just the caster.
        LagSpikeTrapRunner runner = player.GetComponent<LagSpikeTrapRunner>();
        if (runner == null)
        {
            Debug.LogWarning("[LagSpike] Missing LagSpikeTrapRunner on the player prefab.");
            return;
        }

        runner.Begin(center, radius, tier, slowDuration, trapLifetime);
    }
}
