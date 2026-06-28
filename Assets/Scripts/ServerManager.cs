using System;
using Photon.Pun;
using UnityEngine;
using static BasePlayer;

// class responsible of things like: setting players type (killer, survivor)
public class ServerManager : MonoBehaviourPunCallbacks
{
    RoundManager roundManager;

    public GameObject[] allPlayers;
    public BasePlayer[] allBasePlayers;

    private void Start()
    {
        roundManager = GetComponent<RoundManager>();
        roundManager.OnRoundStart += RoundStartedRPC;
        roundManager.OnIntermissionStart += IntermissionStart;
    }

    private void IntermissionStart()
    {
        foreach (BasePlayer player in allBasePlayers)
        {
            player.GetComponentInChildren<InputArmController>().enabled = false;

            PhotonView pv = player.GetComponent<PhotonView>();

            PhotonNetwork.RemoveBufferedRPCs(pv.ViewID, "ResetToDefaultStats");
            pv.RPC("ResetToDefaultStats", RpcTarget.AllBuffered);
        }
    }

    private void OnDestroy()
    {
        if (roundManager != null)
        {
            roundManager.OnRoundStart -= RoundStartedRPC;
        }
    }

    private void Update()
    {
        CachePlayers();
    }

    private void CachePlayers()
    {
        allPlayers = GameObject.FindGameObjectsWithTag("Player");
        allBasePlayers = Array.FindAll(
            FindObjectsByType<BasePlayer>(FindObjectsSortMode.None),
            p => p != null && p.gameObject.CompareTag("Player")
        );
    }

    public static KillerType GetCurrentKiller()
    {
        foreach (BasePlayer player in FindFirstObjectByType<ServerManager>().allBasePlayers)
        {
            if (player.GetPlayerType() == PlayerType.Killer)
            {
                return player.GetEquippedKiller();
            }
        }
        return null;
    }

    public void RoundStartedRPC()
    {
        // only the Master Client assigns roles
        if (!PhotonNetwork.IsMasterClient)
            return;

        // No need to RPC to ourselves; just call directly
        HandleRoundStart();
    }

    [PunRPC]
    public void HandleRoundStart()
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        // SNAPSHOT players once
        CachePlayers();

        GameObject highestMalicePlayer = null;
        int highestMalice = int.MinValue;
        System.Collections.Generic.List<GameObject> contenders =
            new System.Collections.Generic.List<GameObject>();

        // PICK HIGHEST MALICE
        foreach (GameObject player in allPlayers)
        {
            if (player == null)
                continue;

            BasePlayerStats stats = player.GetComponent<BasePlayerStats>();
            if (stats == null)
                continue;

            if (stats.malice > highestMalice)
            {
                highestMalice = stats.malice;
                contenders.Clear();
                contenders.Add(player);
            }
            else if (stats.malice == highestMalice)
            {
                contenders.Add(player);
            }
        }

        if (contenders.Count > 0)
        {
            int chosenIndex = UnityEngine.Random.Range(0, contenders.Count);
            highestMalicePlayer = contenders[chosenIndex];
        }

        // ASSIGN ROLES + CHARACTER SKINS
        foreach (GameObject player in allPlayers)
        {
            if (player == null)
                continue;

            PhotonView pv = player.GetPhotonView();
            BasePlayer basePlayer = player.GetComponent<BasePlayer>();
            PlayerCustomizer customizer = player.GetComponent<PlayerCustomizer>();

            if (pv == null || basePlayer == null || customizer == null)
                continue;

            PlayerType type =
                (player == highestMalicePlayer) ? PlayerType.Killer : PlayerType.Survivor;

            PhotonNetwork.RemoveBufferedRPCs(pv.ViewID, "SetPlayerType");
            pv.RPC("SetPlayerType", RpcTarget.AllBuffered, type);

            // 2) Read the character ID from the player's Photon custom properties.
            //    These are set by BasePlayer.SyncEquippedCharacterProps() on each client's
            //    own machine, so the master always reads the correct remote player selections
            //    rather than its own local PlayerPrefs values.
            Photon.Realtime.Player photonPlayer = pv.Owner;
            string characterId = null;

            if (type == PlayerType.Killer)
            {
                if (
                    photonPlayer != null
                    && photonPlayer.CustomProperties.TryGetValue(
                        BasePlayer.PropEquippedKiller,
                        out object killerNameObj
                    )
                    && killerNameObj is string killerName
                    && !string.IsNullOrEmpty(killerName)
                )
                {
                    characterId = killerName;
                }
                else
                {
                    // Fallback: read local field (valid when this player is the master client itself)
                    KillerType killer = basePlayer.GetEquippedKiller();
                    if (killer != null)
                        characterId = killer._Name;
                }

                if (characterId == null)
                {
                    Debug.LogWarning(
                        $"[ServerManager] Could not resolve killer character for player {pv.OwnerActorNr} — skipping skin."
                    );
                    continue;
                }
            }
            else
            {
                if (
                    photonPlayer != null
                    && photonPlayer.CustomProperties.TryGetValue(
                        BasePlayer.PropEquippedSurvivor,
                        out object survivorNameObj
                    )
                    && survivorNameObj is string survivorName
                    && !string.IsNullOrEmpty(survivorName)
                )
                {
                    characterId = survivorName;
                }
                else
                {
                    // Fallback: read local field (valid when this player is the master client itself)
                    SurvivorType survivor = basePlayer.GetEquippedSurvivor();
                    if (survivor != null)
                        characterId = survivor._Name;
                }

                if (characterId == null)
                {
                    Debug.LogWarning(
                        $"[ServerManager] Could not resolve survivor character for player {pv.OwnerActorNr} — skipping skin."
                    );
                    continue;
                }
            }

            // 3) Apply role + character skin to every client via PlayerCustomizer
            PhotonView customizerPV = customizer.photonView;
            customizerPV.RPC("ApplyRoleAndCharacter", RpcTarget.AllBuffered, type, characterId);

            // 4) Apply stats from the ScriptableObject to BasePlayerStats on this player's
            //    owning client via RPC, so health/speed/stamina reflect the character's data.
            CubePlayerBase characterData =
                type == PlayerType.Killer
                    ? (CubePlayerBase)CharacterDatabase.GetKillerByName(characterId)
                    : (CubePlayerBase)CharacterDatabase.GetSurvivorByName(characterId);

            if (characterData != null)
            {
                PhotonNetwork.RemoveBufferedRPCs(pv.ViewID, "ApplyCharacterStats");

                pv.RPC(
                    "ApplyCharacterStats",
                    RpcTarget.AllBuffered,
                    characterData.Health,
                    characterData.Speed,
                    characterData.Stamina
                );
            }
            else
            {
                Debug.LogWarning(
                    $"[ServerManager] Could not resolve character data for '{characterId}' — stats not applied."
                );
            }
        }
    }
}
