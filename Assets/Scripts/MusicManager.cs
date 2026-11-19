using System;
using UnityEngine;

public class MusicManager : MonoBehaviour
{
    public AudioClip intermissionTrack;
    public AudioClip[] ambienceTracks;

    private AudioSource interSource;
    public AudioSource roundSource;
    public RoundManager roundManager;

    void Start()
    {
        interSource = GetComponent<AudioSource>();

        if (roundManager == null)
            roundManager = FindFirstObjectByType<RoundManager>();

        if (roundManager != null)
        {
            roundManager.OnIntermissionStart += PlayIntermissionMusic;
            roundManager.OnRoundStart += PlayAmbienceMusic;
        }
        else
        {
            Debug.LogWarning("[MusicManager] No RoundManager found; music will not play.");
        }
    }

    private void OnDestroy()
    {
        if (roundManager == null)
            return;

        roundManager.OnIntermissionStart -= PlayIntermissionMusic;
        roundManager.OnRoundStart -= PlayAmbienceMusic;
    }

    private void PlayAmbienceMusic()
    {
        if (roundSource == null || interSource == null)
            return;

        if (roundSource.isPlaying || interSource.isPlaying)
        {
            interSource.Stop();
            roundSource.Stop();
        }

        if (ambienceTracks == null || ambienceTracks.Length == 0)
            return;

        AudioClip selectedTrack = ambienceTracks[
            UnityEngine.Random.Range(0, ambienceTracks.Length)
        ];

        if (selectedTrack == null)
            return;

        roundSource.clip = selectedTrack;
        roundSource.Play();
    }

    private void PlayIntermissionMusic()
    {
        if (interSource == null)
            return;

        if (interSource.isPlaying)
            interSource.Stop();

        if (intermissionTrack == null)
            return;

        interSource.clip = intermissionTrack;
        interSource.Play();
    }
}
