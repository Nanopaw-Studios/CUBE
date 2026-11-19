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

    // Guards against double round-end when two survivors die in the same frame.
    private bool roundEnding = false;

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

    public override void OnLeftRoom()
    {
        // A new room may reuse this component after leaving. Clear cycle state so
        // OnJoinedRoom cannot mistake a fresh lobby for a running round.
        roundActive = false;
        intermissionActive = false;
        roundEnding = false;
        HasSyncedState = false;
        endTimestamp = 0.0;

        if (timerCoroutine != null)
        {
            StopCoroutine(timerCoroutine);
            timerCoroutine = null;
        }
    }

    public override void OnJoinedRoom()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            // Never restart an already-running cycle, and never leave two
            // buffered StartIntermission events in the room cache.
            if (roundActive || intermissionActive)
                return;

            PhotonNetwork.RemoveRPCs(photonView);
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

        // Best-effort push. The joining client cannot rely on this: it fires
        // before that client has loaded SampleScene, so it also requests state
        // itself once its scene is ready (see RequestState).
        SendStateTo(newPlayer);

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

    /// <summary>
    /// Pull-based state sync. A non-master client calls this after its scene has
    /// loaded, so it receives roles, colors, timers and the current map index
    /// even when the initial push was dropped during the scene load.
    /// </summary>
    [PunRPC]
    public void RequestState(PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient || info.Sender == null)
            return;

        SendStateTo(info.Sender);
    }

    private void SendStateTo(Photon.Realtime.Player newPlayer)
    {
        if (newPlayer == null)
            return;

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
            nameof(SyncState),
            newPlayer,
            roundActive,
            intermissionActive,
            endTimestamp,
            SyncedIntermissionSpawnPos,
            SyncedIntermissionSpawnRot,
            actorNumbers.ToArray(),
            roleValues.ToArray(),
            colorHexes.ToArray(),
            GetCurrentMapIndex()
        );
    }

    private int GetCurrentMapIndex()
    {
        PhotonLauncher launcher = PhotonLauncher.Instance != null
            ? PhotonLauncher.Instance
            : FindFirstObjectByType<PhotonLauncher>();

        return launcher != null ? launcher.CurrentMapIndex : -1;
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
        roundEnding = false;
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
        roundEnding = false;
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

    /// <summary>Set on the joining client once a full state snapshot has been applied.</summary>
    public bool HasSyncedState { get; private set; }

    [PunRPC]
    private void SyncState(
        bool roundActiveState,
        bool intermissionActiveState,
        double syncedEndTimestamp,
        Vector3 spawnPosition,
        Quaternion spawnRotation,
        int[] actorNumbers,
        int[] roleValues,
        string[] colorHexes,
        int mapIndex
    )
    {
        HasSyncedState = true;
        roundEnding = false;
        roundActive = roundActiveState;
        intermissionActive = intermissionActiveState;
        endTimestamp = syncedEndTimestamp;
        SyncedIntermissionSpawnPos = spawnPosition;
        SyncedIntermissionSpawnRot = spawnRotation;

        // Rebuild the current map locally if this client joined while one existed.
        // ChooseMap is idempotent, so existing clients are unaffected.
        if (mapIndex >= 0)
        {
            PhotonLauncher launcher = PhotonLauncher.Instance != null
                ? PhotonLauncher.Instance
                : FindFirstObjectByType<PhotonLauncher>();

            if (launcher != null)
                launcher.ChooseMap(mapIndex);
        }

        ApplyRolesAndColors(actorNumbers, roleValues, colorHexes);

        // Player objects on the joining client may still be instantiating, so
        // re-apply for a short window until every actor has been resolved.
        StartCoroutine(ReapplyStateUntilPlayersSpawn(actorNumbers, roleValues, colorHexes));

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

    private IEnumerator ReapplyStateUntilPlayersSpawn(
        int[] actorNumbers,
        int[] roleValues,
        string[] colorHexes
    )
    {
        const float timeout = 3f;
        const float interval = 0.25f;
        float elapsed = 0f;

        while (elapsed < timeout)
        {
            ApplyRolesAndColors(actorNumbers, roleValues, colorHexes);

            if (AllActorsPresent(actorNumbers))
                yield break;

            elapsed += interval;
            yield return new WaitForSecondsRealtime(interval);
        }
    }

    private static bool AllActorsPresent(int[] actorNumbers)
    {
        if (actorNumbers == null || actorNumbers.Length == 0)
            return true;

        int found = 0;
        foreach (GameObject go in GameObject.FindGameObjectsWithTag("Player"))
        {
            PhotonView pv = go.GetComponent<PhotonView>();
            if (pv == null)
                continue;

            if (System.Array.IndexOf(actorNumbers, pv.OwnerActorNr) >= 0)
                found++;
        }

        return found >= actorNumbers.Length;
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
            if (roundEnding)
                return;
            roundEnding = true;

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
                    // Only survivors who actually survived the round gain malice.
                    // Dead survivors are spectating and must not influence the next
                    // killer selection.
                    if (stats.health > 0)
                        stats.photonView.RPC("AdjustMalice", RpcTarget.All, +1);
                }
                else if (basePlayer.GetPlayerType() == BasePlayer.PlayerType.Killer)
                {
                    // Malice is local state. Buffering it made every late joiner
                    // replay all previous rounds' adjustments.
                    stats.photonView.RPC("AdjustMalice", RpcTarget.All, -5);
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
                    // Role-less players (spectators / a joining client) need the
                    // color applied even mid-round. Players with a role keep their
                    // character skin; the hex is still stored for intermission.
                    customizer.ForceApplyIntermissionColor(color, force: roleValue < 0);
                }
            }
        }
    }
}
