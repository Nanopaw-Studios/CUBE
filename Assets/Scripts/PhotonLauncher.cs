using System.Collections;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using System.Collections.Generic;

public class PhotonLauncher : MonoBehaviourPunCallbacks
{
    [Header("References")]
    public RoundManager roundManager;
    public GameObject playerPrefab;
    public static GameObject LocalPlayerInstance;

    [Header("Spawns")]
    public List<Transform> killerSpawns;
    public List<Transform> survivorSpawns;
    public Transform intermissionSpawn;

    [Header("Maps")]
    public MapData[] maps;
    public GameObject currentMap;

    private void Awake()
    {
        // subscribe to round manager events if assigned
        if (roundManager != null)
        {
            roundManager.OnRoundStart += HandleRoundStart;
            roundManager.OnRoundStart += HandleRoundStartTracking;
            roundManager.OnIntermissionStart += HandleIntermissionStart;

            if (intermissionSpawn != null)
            {
                roundManager.UpdateIntermissionSpawn(intermissionSpawn);
            }
        }
    }

    private void PopulateSpawns()
    {
        killerSpawns.Clear();
        survivorSpawns.Clear();

        if (currentMap == null) return;

        Transform[] allTransforms = currentMap.GetComponentsInChildren<Transform>();
        foreach (Transform t in allTransforms)
        {
            if (t.name.Contains("Spawn"))
            {
                if (t.name.Contains("Killer"))
                {
                    if (!killerSpawns.Exists(x => x.name == t.name))
                        killerSpawns.Add(t);
                }
                else
                {
                    if (!survivorSpawns.Exists(x => x.name == t.name))
                        survivorSpawns.Add(t);
                }
            }
        }
    }

    private void OnDestroy()
    {
        // unsubscribe to avoid leaks
        if (roundManager != null)
        {
            roundManager.OnRoundStart -= HandleRoundStart;
            roundManager.OnRoundStart -= HandleRoundStartTracking;
            roundManager.OnIntermissionStart -= HandleIntermissionStart;
        }
    }

    // This is now the main teleport method that other scripts call
    public void TeleportPlayer(Vector3 targetPosition, Quaternion? targetRotation = null)
    {
        if (LocalPlayerInstance == null)
        {
            Debug.LogWarning("LocalPlayerInstance is null!");
            return;
        }

        // Try to use the PlayerTeleportHandler if it exists
        PlayerTeleportHandler teleportHandler = LocalPlayerInstance.GetComponent<PlayerTeleportHandler>();
        if (teleportHandler != null)
        {
            teleportHandler.Teleport(targetPosition, targetRotation, snapToGround: true, useOffset: false);
        }
        else
        {
            Debug.LogWarning("PlayerTeleportHandler component not found on LocalPlayerInstance.");
        }
    }

    void Start()
    {
        PhotonNetwork.AutomaticallySyncScene = true;
        if (PhotonNetwork.InRoom)
        {
            EnsureSpawned();
        }
    }

    private void EnsureSpawned()
    {
        if (LocalPlayerInstance != null) return;

        // Pick a safe spawn even if roundManager/intermissionSpawn isn't ready yet
        Vector3 pos = transform.position;
        Quaternion rot = Quaternion.identity;

        if (roundManager != null)
        {
            pos = roundManager.SyncedIntermissionSpawnPos;
            rot = roundManager.SyncedIntermissionSpawnRot;
        }
        else if (intermissionSpawn != null)
        {
            pos = intermissionSpawn.position;
            rot = intermissionSpawn.rotation;
        }

        LocalPlayerInstance = PhotonNetwork.Instantiate(playerPrefab.name, pos, rot);
        Debug.Log("Spawned local player.");

        BasePlayer bp = LocalPlayerInstance.GetComponent<BasePlayer>();
        if (roundManager != null && roundManager.roundActive)
        {
            bp.playerType = BasePlayer.PlayerType.None;
        }

        // Read the player's own saved color from PlayerPrefs (set by PlayerColorer via the color picker).
        // Falling back to the shared PlayerMat would pick up whatever the Master Client last set on it,
        // giving mid-joiners the wrong color until the next round ends.
        string hex;
        if (PlayerPrefs.HasKey("PlayerColor"))
        {
            hex = PlayerPrefs.GetString("PlayerColor");
        }
        else
        {
            Color color = Resources.Load<Material>("PlayerMat").GetColor("_MainColor");
            hex = "#" + ColorUtility.ToHtmlStringRGB(color);
        }

        var customizer = LocalPlayerInstance.GetComponent<PlayerCustomizer>();
        if (customizer != null)
        {
            customizer.ChangeColorCalled(hex);
        }
    }


    public override void OnConnectedToMaster()
    {
        Debug.Log("Connected to Master");
    }

    public override void OnJoinedRoom()
    {
        Debug.Log("Joined a room...");
        if (PhotonNetwork.IsMasterClient)
            EnsureSpawned();
        else
            StartCoroutine(SpawnAfterStateSync());
    }

    private IEnumerator SpawnAfterStateSync()
    {
        // Wait until RoundManager has received a valid synced state
        float timeout = 5f;
        float elapsed = 0f;
        while (elapsed < timeout)
        {
            // SyncedIntermissionSpawnPos is non-zero once SyncState RPC arrives
            if (roundManager != null &&
                (roundManager.roundActive || roundManager.intermissionActive))
                break;
            elapsed += Time.deltaTime;
            yield return null;
        }
        EnsureSpawned();
    }


    public override void OnLeftRoom()
    {
        Debug.Log("left room...");

        if (LocalPlayerInstance != null)
        {
            Destroy(LocalPlayerInstance);
        }
    }

    [PunRPC]
    public void ChooseMap(int mapIndex)
    {
        if (maps == null || maps.Length == 0)
        {
            Debug.LogError("HandleRoundStart: No maps assigned in the inspector.");
            return;
        }

        if (currentMap != null)
        {
            Destroy(currentMap); // destroy old map if needed
        }

        // Everyone instantiates the same map index
        currentMap = Instantiate(maps[mapIndex].mapPrefab, new Vector3(0, 0, -200), Quaternion.identity);
        Debug.Log("Map chosen and instantiated: " + maps[mapIndex].mapName);

        // Now populate spawns after the map is created
        PopulateSpawns();

        if (roundManager != null && intermissionSpawn != null)
        {
            roundManager.UpdateIntermissionSpawn(intermissionSpawn);
        }

        StartCoroutine(TeleportToRoleSpawnWhenReady());
    }

    /// <summary>
    /// Waits until the local player is spawned and their role is assigned,
    /// then teleports them to the correct spawn point for their role.
    /// </summary>
    private System.Collections.IEnumerator TeleportToRoleSpawnWhenReady()
    {
        // Wait until LocalPlayerInstance exists (handles late joiners)
        float timeout = 10f;
        float elapsed = 0f;
        while (LocalPlayerInstance == null && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (LocalPlayerInstance == null)
        {
            Debug.LogWarning("[PhotonLauncher] TeleportToRoleSpawnWhenReady: LocalPlayerInstance is still null after timeout.");
            yield break;
        }

        // Wait one more frame to allow SetPlayerType RPC to arrive
        yield return null;

        if (roundManager != null)
        {
            // If player joined mid-round, NEVER teleport into match
            if (roundManager.roundActive && !roundManager.intermissionActive)
            {
                BasePlayer bp = LocalPlayerInstance.GetComponent<BasePlayer>();

                // If they don't have a role yet → they are a mid-joiner
                if (bp != null && bp.GetPlayerType() == BasePlayer.PlayerType.None)
                {
                    Debug.Log("[PhotonLauncher] Mid-round join detected → staying in intermission.");
                    yield break;
                }
            }
        }
        BasePlayer basePlayer = LocalPlayerInstance.GetComponent<BasePlayer>();
        if (basePlayer == null)
        {
            Debug.LogWarning("[PhotonLauncher] TeleportToRoleSpawnWhenReady: No BasePlayer on LocalPlayerInstance.");
            yield break;
        }

        Transform chosenSpawn = null;

        var type = basePlayer.GetPlayerType();

        if (type == BasePlayer.PlayerType.Killer && killerSpawns.Count > 0)
        {
            chosenSpawn = killerSpawns[Random.Range(0, killerSpawns.Count)];
        }
        else if (type == BasePlayer.PlayerType.Survivor && survivorSpawns.Count > 0)
        {
            chosenSpawn = survivorSpawns[Random.Range(0, survivorSpawns.Count)];
        }
        else
        {
            Debug.Log("[PhotonLauncher] No valid role → not teleporting.");
            yield break;
        }

        if (chosenSpawn == null)
        {
            Debug.LogWarning("[PhotonLauncher] TeleportToRoleSpawnWhenReady: No valid spawn point found.");
            yield break;
        }

        Debug.Log($"[PhotonLauncher] Teleporting {basePlayer.GetPlayerType()} to: {chosenSpawn.position}");
        TeleportPlayer(chosenSpawn.position, chosenSpawn.rotation);
    }

    private void HandleRoundStart()
    {
        // Safety checks
        if (LocalPlayerInstance == null)
        {
            Debug.LogWarning("HandleRoundStart: LocalPlayerInstance is null. Player hasn't spawned yet.");
            return;
        }

        // Only the host chooses and spawns the map
        if (PhotonNetwork.IsMasterClient)
        {
            int mapIndex = Random.Range(0, maps.Length);
            photonView.RPC("ChooseMap", RpcTarget.All, mapIndex);
        }
    }

    // Tracks whether the local player was already in intermission before the latest
    // OnIntermissionStart event fires (i.e. the round just ended). If they were
    // already sitting at the intermission spawn we should NOT teleport them again.
    private bool wasInIntermissionBeforeEvent = false;

    private void HandleIntermissionStart()
    {
        if (LocalPlayerInstance == null) return;

        // If the player was already in intermission (mid-round joiner or previous
        // intermission), don't teleport them again — they are already there.
        if (wasInIntermissionBeforeEvent)
        {
            Debug.Log("Intermission started but player was already in intermission — skipping teleport.");
            wasInIntermissionBeforeEvent = true; // stays true
            return;
        }

        Debug.Log("Intermission started — teleporting to intermission spawn.");

        Vector3 basePos = roundManager.SyncedIntermissionSpawnPos;
        Quaternion spawnRot = roundManager.SyncedIntermissionSpawnRot;

        // Spread players in a circle around the spawn point to avoid collisions
        const float spreadRadius = 1.5f;
        int actorNr = PhotonNetwork.LocalPlayer.ActorNumber;
        float angle = actorNr * Mathf.PI * 2f / Mathf.Max(PhotonNetwork.CurrentRoom.PlayerCount, 1);
        Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * spreadRadius;

        TeleportPlayer(basePos + offset, spawnRot);

        // Mark as in intermission so the next OnIntermissionStart won't re-teleport.
        wasInIntermissionBeforeEvent = true;
    }

    private void HandleRoundStartTracking()
    {
        // When a round starts the player is no longer in intermission,
        // so the next OnIntermissionStart (round end) should teleport them.
        wasInIntermissionBeforeEvent = false;
    }
}
