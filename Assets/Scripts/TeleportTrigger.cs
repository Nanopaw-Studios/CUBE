using UnityEngine;
using Photon.Pun;

/// <summary>
/// Dynamic state-based out-of-bounds teleport trigger.
/// Teleports the player back inside map bounds when they leave the map area,
/// automatically routing them to either round bounds or intermission bounds
/// based on the current game state in RoundManager.
/// </summary>
public class TeleportTrigger : MonoBehaviour
{
    [Header("Optional Target Overrides")]
    [Tooltip("If set, forces intermission out-of-bounds teleports to this transform instead of the default intermission spawn.")]
    public Transform intermissionSpawnOverride;

    [Tooltip("If set, forces round out-of-bounds teleports to this transform instead of role-based map spawns.")]
    public Transform roundSpawnOverride;

    [Header("Trigger Settings")]
    [Tooltip("Trigger teleport when a player collider enters this volume.")]
    public bool triggerOnEnter = true;

    [Tooltip("Trigger teleport when a player collider exits this volume (useful for boundary boxes surrounding the play area).")]
    public bool triggerOnExit = false;

    [Header("Y-Level Fall Net")]
    [Tooltip("Enable continuous Y position check for local player as a fallback safety floor.")]
    public bool enableYThresholdCheck = false;

    [Tooltip("If Y-level check is enabled, teleport the player when they fall below this Y coordinate.")]
    public float fallYThreshold = -50f;

    [Header("Cooldown")]
    [Tooltip("Cooldown period in seconds to avoid redundant teleports from multi-collider ragdolls.")]
    public float cooldownSeconds = 1.0f;

    private float nextAllowedTeleportTime = 0f;

    private void Update()
    {
        if (!enableYThresholdCheck)
            return;

        GameObject localPlayer = PhotonLauncher.LocalPlayerInstance;
        if (localPlayer != null && localPlayer.transform.position.y < fallYThreshold)
        {
            TryHandleOutOfBounds(localPlayer, "Y Threshold Falloff");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!triggerOnEnter)
            return;

        CheckAndHandleCollider(other, "OnTriggerEnter");
    }

    private void OnTriggerExit(Collider other)
    {
        if (!triggerOnExit)
            return;

        CheckAndHandleCollider(other, "OnTriggerExit");
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!triggerOnEnter)
            return;

        CheckAndHandleCollider(collision.collider, "OnCollisionEnter");
    }

    private void CheckAndHandleCollider(Collider other, string triggerReason)
    {
        if (other == null)
            return;

        GameObject localPlayer = PhotonLauncher.LocalPlayerInstance;

        // Verify if the colliding object belongs to the local player
        bool isLocalPlayer = false;

        if (localPlayer != null)
        {
            if (other.gameObject == localPlayer || other.transform.IsChildOf(localPlayer.transform))
            {
                isLocalPlayer = true;
            }
        }

        if (!isLocalPlayer)
        {
            PhotonView pv = other.GetComponentInParent<PhotonView>();
            if (pv != null && pv.IsMine)
            {
                isLocalPlayer = true;
                if (localPlayer == null)
                {
                    localPlayer = pv.gameObject;
                }
            }
        }

        if (isLocalPlayer && localPlayer != null)
        {
            TryHandleOutOfBounds(localPlayer, triggerReason);
        }
    }

    /// <summary>
    /// Processes out-of-bounds teleportation dynamically based on active game state.
    /// </summary>
    public void TryHandleOutOfBounds(GameObject playerObj, string triggerReason = "Out of Bounds")
    {
        if (Time.time < nextAllowedTeleportTime)
            return;

        nextAllowedTeleportTime = Time.time + cooldownSeconds;

        PhotonLauncher launcher = PhotonLauncher.Instance != null ? PhotonLauncher.Instance : FindFirstObjectByType<PhotonLauncher>();
        RoundManager roundManager = launcher != null ? launcher.roundManager : FindFirstObjectByType<RoundManager>();

        bool inRound = roundManager != null && roundManager.roundActive && !roundManager.intermissionActive;

        Vector3 targetPosition;
        Quaternion targetRotation;

        if (inRound)
        {
            // ROUND STATE: Teleport back to active round map bounds
            if (roundSpawnOverride != null)
            {
                targetPosition = roundSpawnOverride.position;
                targetRotation = roundSpawnOverride.rotation;
            }
            else
            {
                BasePlayer basePlayer = playerObj.GetComponent<BasePlayer>();
                BasePlayer.PlayerType role = basePlayer != null ? basePlayer.GetPlayerType() : BasePlayer.PlayerType.None;

                Transform chosenSpawn = launcher != null ? launcher.GetRandomSpawnForRole(role) : null;

                if (chosenSpawn != null)
                {
                    targetPosition = chosenSpawn.position;
                    targetRotation = chosenSpawn.rotation;
                }
                else if (launcher != null && launcher.currentMap != null)
                {
                    targetPosition = launcher.currentMap.transform.position + Vector3.up * 2f;
                    targetRotation = Quaternion.identity;
                }
                else
                {
                    // Fallback to intermission spawn if map spawns aren't found
                    GetIntermissionSpawnLocation(launcher, roundManager, out targetPosition, out targetRotation);
                }
            }

            Debug.Log($"[TeleportTrigger] ({triggerReason}) Player went out of bounds during ROUND. Teleporting to round map at {targetPosition}.");
        }
        else
        {
            // INTERMISSION STATE: Teleport back to intermission map bounds
            if (intermissionSpawnOverride != null)
            {
                targetPosition = intermissionSpawnOverride.position;
                targetRotation = intermissionSpawnOverride.rotation;
            }
            else
            {
                GetIntermissionSpawnLocation(launcher, roundManager, out targetPosition, out targetRotation);
            }

            if (launcher != null)
            {
                launcher.MarkPlayerAtIntermission();
            }

            Debug.Log($"[TeleportTrigger] ({triggerReason}) Player went out of bounds during INTERMISSION. Teleporting to intermission map at {targetPosition}.");
        }

        // Execute teleport using local player teleport handler via PhotonLauncher
        if (launcher != null)
        {
            launcher.TeleportPlayer(targetPosition, targetRotation);
        }
        else
        {
            PlayerTeleportHandler handler = playerObj.GetComponent<PlayerTeleportHandler>();
            if (handler != null)
            {
                handler.Teleport(targetPosition, targetRotation, snapToGround: true, useOffset: false);
            }
        }
    }

    private void GetIntermissionSpawnLocation(PhotonLauncher launcher, RoundManager roundManager, out Vector3 position, out Quaternion rotation)
    {
        if (launcher != null)
        {
            launcher.GetIntermissionSpawn(out position, out rotation);
        }
        else if (roundManager != null && roundManager.SyncedIntermissionSpawnPos != Vector3.zero)
        {
            position = roundManager.SyncedIntermissionSpawnPos;
            rotation = roundManager.SyncedIntermissionSpawnRot;
        }
        else
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
        }
    }
}
