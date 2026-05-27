using System.Collections;
using System.Linq;
using Photon.Pun;
using UnityEngine;

public class ProxSong : MonoBehaviour
{
    public float distanceThreshold = 10f;
    public float fadeSpeed = 2f; // how fast volume fades per second

    private SongLayer[] layers;
    private int maxLayer = 1;

    private RoundManager roundManager;

    public BasePlayer basePlayer; // this instance's BasePlayer
    private KillerType killer;
    private CubeCharacterData data;

    private GameObject localPlayer; // who we are locally

    private void Awake_DISABLED()
    {
        // Removed: localPlayer is now resolved lazily in OnRoundStart.
        localPlayer = FindObjectsByType<BasePlayer>(FindObjectsSortMode.None)
            .FirstOrDefault(p => p.gameObject.GetPhotonView().IsMine) // use your own �is mine� check
            ?.gameObject;
    }

    private void Start()
    {
        roundManager = FindFirstObjectByType<RoundManager>();
        if (roundManager != null)
        {
            roundManager.OnRoundStart += OnRoundStart;
            roundManager.OnIntermissionStart += OnIntermissionStart;
        }
        else
        {
            Debug.LogWarning("ProxSong: no RoundManager found.");
        }
    }

    private void OnIntermissionStart()
    {
        // Always tear down audio when the round ends so we start completely
        // fresh next round -- prevents the previous killer's theme from bleeding over.
        CleanupAudio();
        killer = null;
        data = null;

        // Re-enable the component so it can respond to the next OnRoundStart.
        enabled = true;
    }

    private void OnRoundStart()
    {
        // Tear down any audio that survived from a prior round.
        CleanupAudio();
        killer = null;
        data = null;

        // Resolve the local player lazily -- not reliably available at Start time.
        if (localPlayer == null)
        {
            localPlayer = FindObjectsByType<BasePlayer>(FindObjectsSortMode.None)
                .FirstOrDefault(static p => p.gameObject.GetPhotonView()?.IsMine == true)
                ?.gameObject;
        }

        // Wait for the SetPlayerType RPC before deciding whether to play music.
        StartCoroutine(InitWhenRoleReady());
    }

    private System.Collections.IEnumerator InitWhenRoleReady()
    {
        // Give the SetPlayerType RPC up to 5 seconds to arrive.
        const float timeout = 5f;
        float elapsed = 0f;

        while (elapsed < timeout)
        {
            if (basePlayer != null && basePlayer.GetPlayerType() != BasePlayer.PlayerType.None)
                break;
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Only proceed if this player was assigned the Killer role.
        if (basePlayer == null || basePlayer.GetPlayerType() != BasePlayer.PlayerType.Killer)
        {
            // Survivor -- stay enabled and idle; the next OnRoundStart will try again.
            yield break;
        }

        killer = basePlayer.GetEquippedKiller();
        if (killer == null)
        {
            Debug.LogWarning("ProxSong: Killer data not found.");
            yield break;
        }

        data = killer.character;
        InitializeAudio();
    }

    private void CleanupAudio()
    {
        if (layers == null)
            return;
        foreach (var layer in layers)
        {
            if (layer != null && layer.source != null)
                Destroy(layer.source.gameObject);
        }
        layers = null;
    }

    private void OnDestroy()
    {
        if (roundManager != null)
        {
            roundManager.OnRoundStart -= OnRoundStart;
            roundManager.OnIntermissionStart -= OnIntermissionStart;
        }
        CleanupAudio();
    }

    private void InitializeAudio()
    {
        AudioClip[] killerLayers = new AudioClip[]
        {
            data.layer1,
            data.layer2,
            data.layer3,
            data.layer4,
        };

        layers = new SongLayer[killerLayers.Length];
        for (int i = 0; i < killerLayers.Length; i++)
        {
            var srcGO = new GameObject("AudioSource Layer" + (i + 1));
            srcGO.transform.SetParent(transform);
            AudioSource src = srcGO.AddComponent<AudioSource>();
            src.transform.localPosition = Vector3.zero;

            layers[i] = new SongLayer
            {
                layer = i + 1,
                audio = killerLayers[i],
                source = src,
            };

            src.clip = killerLayers[i];
            src.loop = true;
            src.playOnAwake = false;
            src.volume = 0f;
            src.spatialBlend = 0f; // or 1f if you want 3D audio
            src.Play();
        }

        if (layers.Length > 0)
            maxLayer = layers.Max(l => l.layer);
    }

    private void Update()
    {
        if (localPlayer == null || layers == null)
            return;

        float distance = GetRelevantDistance();

        foreach (var layer in layers)
        {
            if (layer == null || layer.source == null)
                continue;

            float activationRadius = (maxLayer - layer.layer + 1) * distanceThreshold;
            float targetVolume = (distance < activationRadius) ? .4f : 0f;

            layer.source.volume = Mathf.MoveTowards(
                layer.source.volume,
                targetVolume,
                fadeSpeed * Time.deltaTime
            );
        }
    }

    /// <summary>
    /// Returns the distance used for layer fading.
    /// Survivors use their own distance to the killer.
    /// The killer uses the distance to their nearest survivor so they hear
    /// chase music when someone is close, but not all round.
    /// </summary>
    private float GetRelevantDistance()
    {
        bool localIsKiller =
            basePlayer != null && basePlayer.gameObject.GetPhotonView()?.IsMine == true;

        if (!localIsKiller)
        {
            // Survivor path: distance from killer to this local survivor's hips.
            Transform hips = localPlayer.transform.Find("RIG")?.Find("Hip");
            if (hips == null)
                return float.MaxValue;
            return Vector3.Distance(transform.position, hips.position);
        }
        else
        {
            // Killer path: use the nearest survivor's distance so chase music
            // only plays when a survivor is actually close.
            float nearest = float.MaxValue;
            foreach (BasePlayer other in FindObjectsByType<BasePlayer>(FindObjectsSortMode.None))
            {
                if (other.GetPlayerType() != BasePlayer.PlayerType.Survivor)
                    continue;

                Transform hips = other.transform.Find("RIG")?.Find("Hip");
                if (hips == null)
                    continue;

                float dist = Vector3.Distance(transform.position, hips.position);
                if (dist < nearest)
                    nearest = dist;
            }
            return nearest;
        }
    }
}

[System.Serializable]
public class SongLayer
{
    public int layer;
    public AudioClip audio;
    public AudioSource source;
}
