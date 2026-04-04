using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using System;
using UnityEngine;

public class BasePlayer : MonoBehaviourPunCallbacks
{
    // PlayerPrefs keys for persisting the selected character names across sessions.
    private const string KillerPrefKey = "EquippedKillerName";
    private const string SurvivorPrefKey = "EquippedSurvivorName";

    // Photon custom property keys used to sync equipped character names to all clients.
    public const string PropEquippedKiller = "ek";
    public const string PropEquippedSurvivor = "es";

    #region Player Type
    public enum PlayerType
    {
        None,
        Killer,
        Survivor
    }

    public PlayerType playerType = PlayerType.None;

    public PlayerType GetPlayerType()
    {
        return playerType;
    }

    [PunRPC]
    public void SetPlayerType(PlayerType type)
    {
        Debug.Log($"Setting player type to: {type}");
        playerType = type;
    }

    #endregion

    #region Player Character

    public SurvivorType equippedSurvivor;
    public KillerType equippedKiller;

    private void Awake()
    {
        // Restore the last equipped characters from persistent storage so every
        // session starts with the player's own selection rather than the prefab default.
        LoadEquippedCharacters();
    }

    /// <summary>
    /// Re-syncs equipped character names once we are inside a room so Photon
    /// custom properties are actually written (SetCustomProperties is a no-op
    /// before the local player is in a room).
    /// </summary>
    public override void OnJoinedRoom()
    {
        if (photonView.IsMine)
            SyncEquippedCharacterProps();
    }

    public SurvivorType GetEquippedSurvivor()
    {
        return equippedSurvivor;
    }

    public KillerType GetEquippedKiller()
    {
        return equippedKiller;
    }

    [PunRPC]
    public void SetKiller(KillerType character)
    {
        Debug.Log($"Setting Killer to: {character}");
        equippedKiller = character;
    }

    [PunRPC]
    public void SetSurvivor(SurvivorType character)
    {
        Debug.Log($"Setting Survivor to: {character}");
        equippedSurvivor = character;
    }

    /// <summary>
    /// Equips a killer by name, persists the selection to PlayerPrefs, and syncs the name
    /// to all clients via Photon custom properties so the master can read it during role assignment.
    /// </summary>
    public void EquipKiller(KillerType killer)
    {
        if (killer == null) return;
        equippedKiller = killer;
        PlayerPrefs.SetString(KillerPrefKey, killer._Name);
        PlayerPrefs.Save();
        SyncEquippedCharacterProps();
        Debug.Log($"[BasePlayer] Equipped killer: {killer._Name}");
    }

    /// <summary>
    /// Equips a survivor by name, persists the selection to PlayerPrefs, and syncs the name
    /// to all clients via Photon custom properties so the master can read it during role assignment.
    /// </summary>
    public void EquipSurvivor(SurvivorType survivor)
    {
        if (survivor == null) return;
        equippedSurvivor = survivor;
        PlayerPrefs.SetString(SurvivorPrefKey, survivor._Name);
        PlayerPrefs.Save();
        SyncEquippedCharacterProps();
        Debug.Log($"[BasePlayer] Equipped survivor: {survivor._Name}");
    }

    /// <summary>
    /// Publishes the currently equipped killer and survivor names as Photon player custom
    /// properties so every client (especially the master) can read the correct values.
    /// Only the local player's properties are ever written — Photon propagates them automatically.
    /// </summary>
    private void SyncEquippedCharacterProps()
    {
        if (!photonView.IsMine) return;

        var props = new Hashtable
        {
            { PropEquippedKiller,   equippedKiller  != null ? equippedKiller._Name  : string.Empty },
            { PropEquippedSurvivor, equippedSurvivor != null ? equippedSurvivor._Name : string.Empty }
        };

        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
    }

    /// <summary>
    /// Loads the last equipped killer and survivor from PlayerPrefs and immediately syncs
    /// the names to Photon so the master client has up-to-date data from the first frame.
    /// Falls back to the prefab-default if no selection is saved or the asset cannot be loaded.
    /// </summary>
    private void LoadEquippedCharacters()
    {
        if (PlayerPrefs.HasKey(KillerPrefKey))
        {
            string name = PlayerPrefs.GetString(KillerPrefKey);
            KillerType loaded = CharacterDatabase.GetKillerByName(name);
            if (loaded != null)
            {
                equippedKiller = loaded;
                Debug.Log($"[BasePlayer] Loaded saved killer: {name}");
            }
            else
            {
                Debug.LogWarning($"[BasePlayer] Saved killer '{name}' could not be loaded — keeping prefab default.");
            }
        }

        if (PlayerPrefs.HasKey(SurvivorPrefKey))
        {
            string name = PlayerPrefs.GetString(SurvivorPrefKey);
            SurvivorType loaded = CharacterDatabase.GetSurvivorByName(name);
            if (loaded != null)
            {
                equippedSurvivor = loaded;
                Debug.Log($"[BasePlayer] Loaded saved survivor: {name}");
            }
            else
            {
                Debug.LogWarning($"[BasePlayer] Saved survivor '{name}' could not be loaded — keeping prefab default.");
            }
        }

        // Sync immediately after loading so the master client sees the correct characters
        // even if the player never opens the loadout screen this session.
        SyncEquippedCharacterProps();
    }

    #endregion
}
