using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

/// <summary>
/// Attached to the dynamic physics cubes on the '2 Bases' map prefab.
/// Kills any Survivor on collision when the cube impacts with sufficient speed and force.
/// Killers are immune.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class LethalPhysicsCube : MonoBehaviour
{
    [Tooltip("Minimum velocity magnitude (m/s) the cube itself must have to be lethal (prevents dying from walking into a stationary cube).")]
    public float minCubeSpeed = 2.5f;

    [Tooltip("Minimum relative impact speed (m/s) required to kill a survivor.")]
    public float minRelativeVelocity = 4.0f;

    [Tooltip("Minimum collision impulse (N·s) that also qualifies as a lethal crush when the cube is in motion.")]
    public float minImpactImpulse = 80f;

    private const int LethalDamage = 9999;
    private const float HitCooldownPerPlayer = 1.0f;

    private Rigidbody _rb;
    private readonly Dictionary<int, float> _lastKillTimeByViewId = new Dictionary<int, float>();

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryProcessCollision(collision);
    }

    private void TryProcessCollision(Collision collision)
    {
        if (collision == null || collision.collider == null)
            return;

        BasePlayer targetPlayer = collision.collider.GetComponentInParent<BasePlayer>();
        if (targetPlayer == null)
            return;

        // Only survivors can be killed by physics cubes
        if (targetPlayer.GetPlayerType() != BasePlayer.PlayerType.Survivor)
            return;

        BasePlayerStats targetStats = targetPlayer.GetComponent<BasePlayerStats>();
        if (targetStats == null || targetStats.health <= 0)
            return;

        PhotonView targetPV = targetPlayer.photonView;
        if (targetPV == null)
            return;

        // Only the survivor's owning client or the Master Client issues the lethal hit RPC
        if (!targetPV.IsMine && !PhotonNetwork.IsMasterClient)
            return;

        if (
            _lastKillTimeByViewId.TryGetValue(targetPV.ViewID, out float lastHit)
            && Time.time - lastHit < HitCooldownPerPlayer
        )
        {
            return;
        }

        float cubeSpeed = _rb != null ? _rb.linearVelocity.magnitude : 0f;
        float relativeSpeed = collision.relativeVelocity.magnitude;
        float impulse = collision.impulse.magnitude;

        // The cube itself must be moving, and the impact must exceed either relative velocity or impulse threshold
        bool hasEnoughForce =
            cubeSpeed >= minCubeSpeed
            && (relativeSpeed >= minRelativeVelocity || impulse >= minImpactImpulse);

        if (!hasEnoughForce)
            return;

        _lastKillTimeByViewId[targetPV.ViewID] = Time.time;

        Debug.Log(
            $"[LethalPhysicsCube] '{name}' crushed survivor '{targetPV.Owner?.NickName}' (cubeSpeed={cubeSpeed:F1}m/s, relSpeed={relativeSpeed:F1}m/s, impulse={impulse:F1})"
        );

        targetPV.RPC(nameof(PlayerHitboxManager.RPC_OnHit), RpcTarget.All, LethalDamage);
    }
}
