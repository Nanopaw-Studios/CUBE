using Photon.Pun;
using System;
using UnityEngine;

public class BasePlayer : MonoBehaviourPunCallbacks
{
    // PlayerPrefs keys for persisting the selected character names across sessions.
    private const string KillerPrefKey = "EquippedKillerName";
    private const string SurvivorPrefKey = "EquippedSurvivorName";

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
    /// Equips a killer by name and persists the selection to PlayerPrefs.
    /// </summary>
    public void EquipKiller(KillerType killer)
    {
        if (killer == null) return;
        equippedKiller = killer;
        PlayerPrefs.SetString(KillerPrefKey, killer._Name);
        PlayerPrefs.Save();
        Debug.Log($"[BasePlayer] Equipped killer: {killer._Name}");
    }

    /// <summary>
    /// Equips a survivor by name and persists the selection to PlayerPrefs.
    /// </summary>
    public void EquipSurvivor(SurvivorType survivor)
    {
        if (survivor == null) return;
        equippedSurvivor = survivor;
        PlayerPrefs.SetString(SurvivorPrefKey, survivor._Name);
        PlayerPrefs.Save();
        Debug.Log($"[BasePlayer] Equipped survivor: {survivor._Name}");
    }

    /// <summary>
    /// Loads the last equipped killer and survivor from PlayerPrefs.
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
    }

    #endregion
}
