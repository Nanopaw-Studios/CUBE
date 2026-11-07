using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class RoundManager : MonoBehaviourPunCallbacks
{
    public int currentTime;
    public int roundTime = 180; // 3 minutes

    [HideInInspector]
    public bool roundActive = false;

    [HideInInspector]
    public bool intermissionActive = false;

    [Tooltip("Minimum number of players required to begin a match.")]
    public int minPlayers = 2;

    [Tooltip("Countdown duration in seconds once enough players are present to start a match.")]
    public int countdownDuration = 10;

    private int intermissionTime = 50;
    public Transform intermissionSpawnPoint;

    public event Action OnRoundStart;
    public event Action OnIntermissionStart;

    private Coroutine timerCoroutine;
    private double endTimestamp;

    public Vector3 SyncedIntermissionSpawnPos { get; private set; }
    public Quaternion SyncedIntermissionSpawnRot { get; private set; }

    private void Start()
    {
        RefreshIntermissionSpawnFromPoint();

        // If we loaded this scene AFTER joining the room, OnJoinedRoom won't fire.
        if (PhotonNetwork.InRoom)
        {
            BootstrapIfNeeded();
        }
    }

    private void BootstrapIfNeeded()
    {
        // Only the master should start the initial cycle.
        if (!PhotonNetwork.IsMasterClient)
            return;

        // If already running, don't restart.
        if (roundActive || intermissionActive)
            return;

        RefreshIntermissionSpawnFromPoint();

        // If we don't have enough players yet, endTimestamp = 0 so no round countdown runs while waiting
        double endTime = HasEnoughPlayers() ? (PhotonNetwork.Time + countdownDuration) : 0.0;

        // IMPORTANT: buffer it so everyone (and late scene loads) get it
        photonView.RPC(
            "StartIntermission",
            RpcTarget.AllBuffered,
            SyncedIntermissionSpawnPos,
            SyncedIntermissionSpawnRot,
            endTime
        );
    }

    public override void OnJoinedRoom()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            RefreshIntermissionSpawnFromPoint();
            double endTime = HasEnoughPlayers() ? (PhotonNetwork.Time + countdownDuration) : 0.0;
            photonView.RPC(
                "StartIntermission",
                RpcTarget.AllBuffered,
                SyncedIntermissionSpawnPos,
                SyncedIntermissionSpawnRot,
                endTime
            );
        }
    }

    public override void OnPlayerEnteredRoom(Photon.Realtime.Player newPlayer)
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        // Send the current round state so late joiners keep timers, roles, and teleports in sync
        var actorNumbers = new System.Collections.Generic.List<int>();
        var roleValues = new System.Collections.Generic.List<int>();
        var colorHexes = new System.Collections.Generic.List<string>();

        foreach (GameObject go in GameObject.FindGameObjectsWithTag("Player"))
        {
            PhotonView pv = go.GetComponent<PhotonView>();
            if (pv == null)
                continue;

            actorNumbers.Add(pv.OwnerActorNr);

            BasePlayer basePlayer = go.GetComponent<BasePlayer>();
            roleValues.Add(basePlayer != null ? (int)basePlayer.GetPlayerType() : -1);

            PlayerCustomizer customizer = go.GetComponent<PlayerCustomizer>();
            colorHexes.Add(customizer != null ? customizer.GetCurrentHex() : string.Empty);
        }

        photonView.RPC(
            "SyncState",
            newPlayer,
            roundActive,
            intermissionActive,
            endTimestamp,
            SyncedIntermissionSpawnPos,
            SyncedIntermissionSpawnRot,
            actorNumbers.ToArray(),
            roleValues.ToArray(),
            colorHexes.ToArray()
        );

        if (intermissionActive && HasEnoughPlayers())
        {
            // If countdown is not running or was 0 while waiting, begin countdown
            if (endTimestamp <= PhotonNetwork.Time)
            {
                endTimestamp = PhotonNetwork.Time + countdownDuration;
                photonView.RPC(nameof(UpdateIntermissionEndTimestamp), RpcTarget.Others, endTimestamp);
            }
        }
    }

    [PunRPC]
    public void UpdateIntermissionEndTimestamp(double syncedEndTimestamp)
    {
        endTimestamp = syncedEndTimestamp;
        UpdateCurrentTime();
    }

    public override void OnPlayerLeftRoom(Photon.Realtime.Player otherPlayer)
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        // If the room is in an active round, check if we still have a killer and survivors
        if (roundActive)
        {
            bool hasKiller = false;
            int aliveSurvivors = 0;

            foreach (GameObject go in GameObject.FindGameObjectsWithTag("Player"))
            {
                if (go == null)
                    continue;

                PhotonView pv = go.GetComponent<PhotonView>();
                if (pv == null || pv.Owner == null || pv.Owner == otherPlayer)
                    continue;

                BasePlayer bp = go.GetComponent<BasePlayer>();
                BasePlayerStats stats = go.GetComponent<BasePlayerStats>();
                if (bp == null)
                    continue;

                if (bp.GetPlayerType() == BasePlayer.PlayerType.Killer)
                {
                    hasKiller = true;
                }
                else if (bp.GetPlayerType() == BasePlayer.PlayerType.Survivor)
                {
                    if (stats != null && stats.health > 0)
                        aliveSurvivors++;
                }
            }

            // If the killer disconnected, or no alive survivors remain, end the round early!
            if (!hasKiller || aliveSurvivors == 0)
            {
                Debug.Log("[RoundManager] Ending round because a role became empty after player disconnect.");
                photonView.RPC("EndRound", RpcTarget.All);
            }
        }
        else if (intermissionActive)
        {
            // If player count drops below minimum during intermission, cancel countdown and restore waiting state
            if (!HasEnoughPlayers())
            {
                Debug.Log("[RoundManager] Player count dropped below minimum during intermission. Restoring waiting lobby state.");
                endTimestamp = 0.0;
                photonView.RPC(nameof(UpdateIntermissionEndTimestamp), RpcTarget.Others, 0.0);
            }
        }
    }

    public override void OnMasterClientSwitched(Photon.Realtime.Player newMasterClient)
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        if (intermissionActive)
        {
            if (!HasEnoughPlayers())
            {
                if (endTimestamp > 0.0)
                {
                    endTimestamp = 0.0;
                    photonView.RPC(nameof(UpdateIntermissionEndTimestamp), RpcTarget.Others, 0.0);
                }
            }
            else if (endTimestamp <= PhotonNetwork.Time)
            {
                endTimestamp = PhotonNetwork.Time + countdownDuration;
                photonView.RPC(nameof(UpdateIntermissionEndTimestamp), RpcTarget.Others, endTimestamp);
            }
        }
    }

    [PunRPC]
    public void StartRound(double roundEndTime)
    {
        roundActive = true;
        intermissionActive = false;
        endTimestamp = roundEndTime;
        UpdateCurrentTime();

        if (timerCoroutine != null)
        {
            StopCoroutine(timerCoroutine);
        }
        timerCoroutine = StartCoroutine(Timer());

        OnRoundStart?.Invoke();
        Debug.Log(
            $"RoundManager.StartRound invoked. Subscribers: {(OnRoundStart == null ? 0 : OnRoundStart.GetInvocationList().Length)}"
        );
        Debug.Log("Round Started");
    }

    [PunRPC]
    public void StartIntermission(
        Vector3 spawnPosition,
        Quaternion spawnRotation,
        double intermissionEndTime
    )
    {
        roundActive = false;
        intermissionActive = true;
        SyncedIntermissionSpawnPos = spawnPosition;
        SyncedIntermissionSpawnRot = spawnRotation;
        endTimestamp = intermissionEndTime;
        UpdateCurrentTime();

        if (timerCoroutine != null)
        {
            StopCoroutine(timerCoroutine);
        }
        timerCoroutine = StartCoroutine(Timer());

        OnIntermissionStart?.Invoke();
    }

    [PunRPC]
    private void SyncState(
        bool roundActiveState,
        bool intermissionActiveState,
        double syncedEndTimestamp,
        Vector3 spawnPosition,
        Quaternion spawnRotation,
        int[] actorNumbers,
        int[] roleValues,
        string[] colorHexes
    )
    {
        roundActive = roundActiveState;
        intermissionActive = intermissionActiveState;
        endTimestamp = syncedEndTimestamp;
        SyncedIntermissionSpawnPos = spawnPosition;
        SyncedIntermissionSpawnRot = spawnRotation;

        ApplyRolesAndColors(actorNumbers, roleValues, colorHexes);

        UpdateCurrentTime();

        if (timerCoroutine != null)
        {
            StopCoroutine(timerCoroutine);
        }

        if (roundActive || intermissionActive)
        {
            timerCoroutine = StartCoroutine(Timer());

            if (intermissionActive)
            {
                OnIntermissionStart?.Invoke();

                // Teleport the local player to intermission spawn so late joiners
                // don't end up in the map via a stale buffered round teleport.
                StartCoroutine(TeleportLocalPlayerToIntermission(spawnPosition, spawnRotation));
            }
            else if (roundActive)
            {
                // A mid-round joiner should NOT be placed on the map with active players.
                // Send them to the intermission spawn instead and fire intermission events
                // so their UI and abilities reflect a spectator/waiting state until the
                // next intermission begins naturally.
                intermissionActive = true;
                roundActive = false;
                OnIntermissionStart?.Invoke();
                StartCoroutine(TeleportLocalPlayerToIntermission(spawnPosition, spawnRotation));
            }
        }
    }

    private IEnumerator TeleportLocalPlayerToIntermission(Vector3 position, Quaternion rotation)
    {
        const float timeout = 10f;
        float elapsed = 0f;

        PlayerTeleportHandler teleporter = null;

        // 🧠 Wait for LOCAL OWNED player to fully exist
        while (elapsed < timeout)
        {
            foreach (GameObject go in GameObject.FindGameObjectsWithTag("Player"))
            {
                PhotonView pv = go.GetComponent<PhotonView>();
                if (pv == null || !pv.IsMine)
                    continue;

                teleporter = go.GetComponent<PlayerTeleportHandler>();
                if (teleporter != null)
                    break;
            }

            if (teleporter != null)
                break;

            elapsed += 0.1f;
            yield return new WaitForSecondsRealtime(0.1f);
        }

        if (teleporter == null)
        {
            Debug.LogWarning("[RoundManager] Teleport failed: local player not found.");
            yield break;
        }

        // 🧠 EXTRA SAFETY: wait one frame for ownership/rig init
        yield return null;

        Debug.Log("[RoundManager] Teleporting local player to intermission.");

        // 🚀 LOCAL teleport ONLY
        teleporter.Teleport(position, rotation, snapToGround: true, useOffset: false);
    }

    private IEnumerator Timer()
    {
        while (roundActive || intermissionActive)
        {
            UpdateCurrentTime();

            if (PhotonNetwork.IsMasterClient)
            {
                if (roundActive && endTimestamp > 0 && PhotonNetwork.Time >= endTimestamp)
                {
                    photonView.RPC("EndRound", RpcTarget.All);
                }
                else if (intermissionActive && endTimestamp > 0 && PhotonNetwork.Time >= endTimestamp)
                {
                    // Authoritative check immediately before starting round
                    if (!HasEnoughPlayers())
                    {
                        Debug.LogWarning("[RoundManager] Timer expired but insufficient players. Restoring waiting lobby state.");
                        endTimestamp = 0.0;
                        photonView.RPC(nameof(UpdateIntermissionEndTimestamp), RpcTarget.Others, 0.0);
                        continue;
                    }

                    double nextEnd = PhotonNetwork.Time + roundTime;

                    PhotonNetwork.RemoveRPCs(photonView);
                    photonView.RPC("StartRound", RpcTarget.AllBuffered, nextEnd);
                }
            }

            yield return new WaitForSecondsRealtime(1);
        }
    }

    [PunRPC]
    public void EndRound()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            foreach (GameObject go in GameObject.FindGameObjectsWithTag("Player"))
            {
                PhotonView pv = go.GetComponent<PhotonView>();
                if (pv == null)
                    continue;

                BasePlayer basePlayer = go.GetComponent<BasePlayer>();
                BasePlayerStats stats = go.GetComponent<BasePlayerStats>();
                if (basePlayer == null || stats == null)
                    continue;

                if (basePlayer.GetPlayerType() == BasePlayer.PlayerType.Survivor)
                {
                    stats.photonView.RPC("AdjustMalice", RpcTarget.AllBuffered, +1);
                }
                else if (basePlayer.GetPlayerType() == BasePlayer.PlayerType.Killer)
                {
                    stats.photonView.RPC("AdjustMalice", RpcTarget.AllBuffered, -5);
                }
            }

            RefreshIntermissionSpawnFromPoint();
            double endTime = HasEnoughPlayers() ? (PhotonNetwork.Time + intermissionTime) : 0.0;

            // Flush stale buffered RPCs on this view so late joiners don't receive
            // an old StartRound when the round has ended.
            PhotonNetwork.RemoveRPCs(photonView);
            photonView.RPC(
                "StartIntermission",
                RpcTarget.AllBuffered,
                SyncedIntermissionSpawnPos,
                SyncedIntermissionSpawnRot,
                endTime
            );
        }
    }

    public int GetCurrentTime()
    {
        return currentTime;
    }

    public int GetRequiredPlayerCount()
    {
        return Mathf.Max(2, minPlayers);
    }

    public bool HasEnoughPlayers()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
            return false;

        return PhotonNetwork.CurrentRoom.PlayerCount >= GetRequiredPlayerCount();
    }

    public void UpdateIntermissionSpawn(Transform spawn)
    {
        intermissionSpawnPoint = spawn;
        RefreshIntermissionSpawnFromPoint();
    }

    private void RefreshIntermissionSpawnFromPoint()
    {
        if (intermissionSpawnPoint != null)
        {
            SyncedIntermissionSpawnPos = intermissionSpawnPoint.position;
            SyncedIntermissionSpawnRot = intermissionSpawnPoint.rotation;
        }
    }

    private void UpdateCurrentTime()
    {
        if (endTimestamp <= 0)
        {
            currentTime = 0;
            return;
        }

        double remaining = Math.Max(0, endTimestamp - PhotonNetwork.Time);
        currentTime = (int)Math.Ceiling(remaining);
    }

    private void ApplyRolesAndColors(int[] actorNumbers, int[] roleValues, string[] colorHexes)
    {
        if (actorNumbers == null || roleValues == null || colorHexes == null)
            return;

        for (int i = 0; i < actorNumbers.Length; i++)
        {
            int actor = actorNumbers[i];
            int roleValue = i < roleValues.Length ? roleValues[i] : -1;
            string color = i < colorHexes.Length ? colorHexes[i] : string.Empty;

            foreach (GameObject go in GameObject.FindGameObjectsWithTag("Player"))
            {
                PhotonView pv = go.GetComponent<PhotonView>();
                if (pv == null || pv.OwnerActorNr != actor)
                    continue;

                // 1. Update the Role
                BasePlayer basePlayer = go.GetComponent<BasePlayer>();
                if (basePlayer != null && roleValue >= 0)
                {
                    basePlayer.SetPlayerType((BasePlayer.PlayerType)roleValue);
                }

                // 2. Update the Customizer
                PlayerCustomizer customizer = go.GetComponent<PlayerCustomizer>();
                if (customizer != null && !string.IsNullOrEmpty(color))
                {
                    // Store the hex and conditionally show it — works regardless of round state
                    customizer.ForceApplyIntermissionColor(color);
                }
            }
        }
    }
}
