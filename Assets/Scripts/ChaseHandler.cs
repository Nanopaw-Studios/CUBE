using System.Collections;
using Photon.Pun;
using UnityEngine;

public class ChaseHandler : MonoBehaviourPunCallbacks
{
    public BasePlayer player;
    private RoundManager roundManager;

    private KillerType killer;
    private CubeCharacterData data;

    public Transform hip; // reference to the killer's hip transform for distance calculations
    public bool inChase = false;

    [Header("Chase Settings")]
    public float chaseStartDistance = 15f; // distance at which chase starts
    public float chaseStopDistance = 20f; // distance at which chase ends

    private void Start()
    {
        roundManager = FindFirstObjectByType<RoundManager>();
        if (roundManager != null)
        {
            roundManager.OnRoundStart += OnRoundStart;
            roundManager.OnIntermissionStart += OnIntermissionStart;
        }
    }

    private void OnDestroy()
    {
        if (roundManager != null)
        {
            roundManager.OnRoundStart -= OnRoundStart;
            roundManager.OnIntermissionStart -= OnIntermissionStart;
        }
    }

    private void OnIntermissionStart()
    {
        // Reset chase state and cached killer data so it doesn't bleed into the next round.
        inChase = false;
        killer = null;
        data = null;
        enabled = true;
    }

    private void OnRoundStart()
    {
        inChase = false;
        killer = null;
        data = null;

        // Wait for the SetPlayerType RPC to arrive before reading the role.
        StartCoroutine(InitWhenRoleReady());
    }

    private IEnumerator InitWhenRoleReady()
    {
        const float timeout = 5f;
        float elapsed = 0f;

        while (elapsed < timeout)
        {
            if (player != null && player.GetPlayerType() != BasePlayer.PlayerType.None)
                break;
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (player == null || player.GetPlayerType() != BasePlayer.PlayerType.Killer)
        {
            enabled = false;
            yield break;
        }

        killer = player.GetEquippedKiller();
        if (killer == null)
        {
            Debug.LogWarning("ChaseHandler: Killer data not found.");
            enabled = false;
            yield break;
        }

        data = killer.character;
        enabled = true;
    }

    private void Update()
    {
        if (player == null || killer == null || hip == null)
            return;

        // Only killers check for chase states
        if (player.GetPlayerType() != BasePlayer.PlayerType.Killer)
            return;

        foreach (BasePlayer otherPlayer in FindFirstObjectByType<ServerManager>().allBasePlayers)
        {
            if (otherPlayer.GetPlayerType() != BasePlayer.PlayerType.Survivor)
                continue;

            float distance = Vector3.Distance(hip.position, otherPlayer.transform.position);

            if (!inChase && distance <= chaseStartDistance)
            {
                inChase = true;
                photonView.RPC("StartMusic", RpcTarget.All, 1); // start layer 1 music for all
            }
            else if (inChase && distance > chaseStopDistance)
            {
                inChase = false;
                photonView.RPC("StopMusic", RpcTarget.All);
            }

            // Optionally, add logic for stronger music layers based on distance thresholds
            if (inChase)
            {
                int layer = 1;
                if (distance < 12f)
                    layer = 2;
                if (distance < 8f)
                    layer = 3;
                if (distance < 4f)
                    layer = 4;

                photonView.RPC("StartMusic", RpcTarget.All, layer);
            }
        }
    }

    [PunRPC]
    public void StartMusic(int layer)
    {
        if (data == null)
        {
            Debug.LogWarning("Killer data is null in ChaseHandler.StartMusic");
            return;
        }
        if (layer < 1 || layer > 4)
        {
            Debug.LogWarning("Invalid layer in ChaseHandler.StartMusic. Must be between 1 and 4.");
            return;
        }

        AudioSource source = GameObject.Find("Audio Source").GetComponent<AudioSource>();
        if (source == null)
        {
            Debug.LogWarning("Audio Source not found in ChaseHandler.StartMusic");
            return;
        }

        // Replace with your layered audio logic
        // Example: use snapshots, blend groups, or swap clips
        Debug.Log($"Playing chase music layer {layer}");
    }

    [PunRPC]
    public void StopMusic()
    {
        AudioSource source = GameObject.Find("Audio Source").GetComponent<AudioSource>();
        if (source != null)
        {
            source.Stop();
        }
    }
}
