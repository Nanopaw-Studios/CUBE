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

    private int currentMusicLayer = 0;
    private AudioSource cachedAudioSource;

    private void Awake()
    {
        if (player == null)
            player = GetComponent<BasePlayer>();

        if (hip == null)
        {
            var ragdoll = GetComponent<RagdollNetwork>();
            if (ragdoll != null && ragdoll.hip != null)
                hip = ragdoll.hip;
            else
                hip = transform.Find("RIG/Hip") ?? transform.Find("RIG/hip") ?? transform;
        }
    }

    private void OnIntermissionStart()
    {
        // Reset chase state and cached killer data so it doesn't bleed into the next round.
        inChase = false;
        currentMusicLayer = 0;
        killer = null;
        data = null;
        enabled = true;
    }

    private void OnRoundStart()
    {
        inChase = false;
        currentMusicLayer = 0;
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

        // Only the local killer client evaluates chase distance to avoid multiple clients fighting over RPCs
        if (!photonView.IsMine || player.GetPlayerType() != BasePlayer.PlayerType.Killer)
            return;

        ServerManager sm = FindFirstObjectByType<ServerManager>();
        if (sm == null || sm.allBasePlayers == null)
            return;

        float minDistance = float.MaxValue;
        bool hasAliveSurvivor = false;

        foreach (BasePlayer otherPlayer in sm.allBasePlayers)
        {
            if (otherPlayer == null || otherPlayer.GetPlayerType() != BasePlayer.PlayerType.Survivor)
                continue;

            BasePlayerStats otherStats = otherPlayer.GetComponent<BasePlayerStats>();
            if (otherStats != null && otherStats.health <= 0)
                continue;

            hasAliveSurvivor = true;
            Transform otherHip = otherPlayer.GetComponent<RagdollNetwork>()?.hip;
            Vector3 survivorPos = otherHip != null ? otherHip.position : otherPlayer.transform.position;

            float dist = Vector3.Distance(hip.position, survivorPos);
            if (dist < minDistance)
            {
                minDistance = dist;
            }
        }

        if (!hasAliveSurvivor)
        {
            if (inChase)
            {
                inChase = false;
                currentMusicLayer = 0;
                photonView.RPC(nameof(StopMusic), RpcTarget.All);
            }
            return;
        }

        if (!inChase)
        {
            if (minDistance <= chaseStartDistance)
            {
                inChase = true;
                currentMusicLayer = 1;
                photonView.RPC(nameof(StartMusic), RpcTarget.All, 1);
            }
        }
        else
        {
            if (minDistance > chaseStopDistance)
            {
                inChase = false;
                currentMusicLayer = 0;
                photonView.RPC(nameof(StopMusic), RpcTarget.All);
            }
            else
            {
                int desiredLayer = 1;
                if (minDistance < 4f)
                    desiredLayer = 4;
                else if (minDistance < 8f)
                    desiredLayer = 3;
                else if (minDistance < 12f)
                    desiredLayer = 2;

                if (desiredLayer != currentMusicLayer)
                {
                    currentMusicLayer = desiredLayer;
                    photonView.RPC(nameof(StartMusic), RpcTarget.All, currentMusicLayer);
                }
            }
        }
    }

    private AudioSource GetChaseAudioSource()
    {
        if (cachedAudioSource != null)
            return cachedAudioSource;

        var go = GameObject.Find("Audio Source");
        if (go != null)
            cachedAudioSource = go.GetComponent<AudioSource>();

        if (cachedAudioSource == null)
            cachedAudioSource = FindFirstObjectByType<AudioSource>();

        return cachedAudioSource;
    }

    [PunRPC]
    public void StartMusic(int layer)
    {
        if (data == null)
        {
            return;
        }
        if (layer < 1 || layer > 4)
        {
            Debug.LogWarning("Invalid layer in ChaseHandler.StartMusic. Must be between 1 and 4.");
            return;
        }

        AudioSource source = GetChaseAudioSource();
        if (source == null)
        {
            return;
        }

        Debug.Log($"Playing chase music layer {layer}");
    }

    [PunRPC]
    public void StopMusic()
    {
        AudioSource source = GetChaseAudioSource();
        if (source != null)
        {
            source.Stop();
        }
    }
}
