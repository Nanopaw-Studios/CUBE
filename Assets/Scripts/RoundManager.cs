using Photon.Pun;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Realtime;

public class RoundManager : MonoBehaviourPunCallbacks
{
    public int currentTime;
    public int roundTime = 180; // 3 minutes
    [HideInInspector] public bool roundActive = false;
    [HideInInspector] public bool intermissionActive = false;

    private int intermissionTime = 40; // 40 seconds
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
        if (!PhotonNetwork.IsMasterClient) return;

        // If already running, don't restart.
        if (roundActive || intermissionActive) return;

        double endTime = PhotonNetwork.Time + intermissionTime;

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
            double endTime = PhotonNetwork.Time + intermissionTime;
            photonView.RPC("StartIntermission", RpcTarget.AllBuffered, SyncedIntermissionSpawnPos, SyncedIntermissionSpawnRot, endTime);
        }
    }

    public override void OnPlayerEnteredRoom(Photon.Realtime.Player newPlayer)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        // Send the current round state so late joiners keep timers, roles, and teleports in sync
        var actorNumbers = new System.Collections.Generic.List<int>();
        var roleValues = new System.Collections.Generic.List<int>();
        var colorHexes = new System.Collections.Generic.List<string>();

        foreach (GameObject go in GameObject.FindGameObjectsWithTag("Player"))
        {
            PhotonView pv = go.GetComponent<PhotonView>();
            if (pv == null) continue;

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
        Debug.Log($"RoundManager.StartRound invoked. Subscribers: {(OnRoundStart == null ? 0 : OnRoundStart.GetInvocationList().Length)}");
        Debug.Log("Round Started");
    }

    [PunRPC]
    public void StartIntermission(Vector3 spawnPosition, Quaternion spawnRotation, double intermissionEndTime)
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
    private void SyncState(bool roundActiveState, bool intermissionActiveState, double syncedEndTimestamp, Vector3 spawnPosition, Quaternion spawnRotation, int[] actorNumbers, int[] roleValues, string[] colorHexes)
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

    private System.Collections.IEnumerator TeleportLocalPlayerToIntermission(Vector3 position, Quaternion rotation)
    {
        // Poll until the local owned player GameObject is available — spawning is async
        const float timeout = 10f;
        float elapsed = 0f;
        PlayerTeleportHandler teleporter = null;

        while (elapsed < timeout)
        {
            foreach (GameObject go in GameObject.FindGameObjectsWithTag("Player"))
            {
                PhotonView pv = go.GetComponent<PhotonView>();
                if (pv == null || !pv.IsMine) continue;

                teleporter = go.GetComponent<PlayerTeleportHandler>();
                break;
            }

            if (teleporter != null) break;

            elapsed += 0.1f;
            yield return new WaitForSecondsRealtime(0.1f);
        }

        if (teleporter == null)
        {
            Debug.LogWarning("[RoundManager] TeleportLocalPlayerToIntermission: local player not found within timeout.");
            yield break;
        }

        // Intermission spawns are pre-placed safe points — skip ground-snapping so the
        // raycast can't miss under a floating platform and land the player in the void below.
        teleporter.InitiateTeleport(position, rotation, snapToGround: false);
    }

    private IEnumerator Timer()
    {
        while (roundActive || intermissionActive)
        {
            UpdateCurrentTime();

            if (PhotonNetwork.IsMasterClient && PhotonNetwork.Time >= endTimestamp)
            {
                if (roundActive)
                {
                    photonView.RPC("EndRound", RpcTarget.All);
                }
                else if (intermissionActive)
                {
                    double nextEnd = PhotonNetwork.Time + roundTime;
                    // Clear the buffered StartIntermission so late joiners don't receive
                    // a stale intermission state once the round has started.
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
            double endTime = PhotonNetwork.Time + intermissionTime;

            // Flush stale buffered RPCs on this view so late joiners don't receive
            // an old StartRound when the round has ended.
            PhotonNetwork.RemoveRPCs(photonView);
            photonView.RPC("StartIntermission", RpcTarget.AllBuffered, SyncedIntermissionSpawnPos, SyncedIntermissionSpawnRot, endTime);
        }
    }

    public int GetCurrentTime()
    {
        return currentTime;
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
        double remaining = Math.Max(0, endTimestamp - PhotonNetwork.Time);
        currentTime = (int)Math.Ceiling(remaining);
    }

private void ApplyRolesAndColors(int[] actorNumbers, int[] roleValues, string[] colorHexes)
{
    if (actorNumbers == null || roleValues == null || colorHexes == null) return;

    for (int i = 0; i < actorNumbers.Length; i++)
    {
        int actor = actorNumbers[i];
        int roleValue = i < roleValues.Length ? roleValues[i] : -1;
        string color = i < colorHexes.Length ? colorHexes[i] : string.Empty;

        foreach (GameObject go in GameObject.FindGameObjectsWithTag("Player"))
        {
            PhotonView pv = go.GetComponent<PhotonView>();
            if (pv == null || pv.OwnerActorNr != actor) continue;

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
