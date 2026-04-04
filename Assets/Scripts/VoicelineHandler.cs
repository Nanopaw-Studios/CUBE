using NanodogsToolkit.NanoVoice;
using Photon.Pun;
using System.Collections;
using UnityEngine;

/// <summary>
/// Handles voicelines for a survivor player.
/// </summary>
public class VoicelineHandler : MonoBehaviourPunCallbacks
{
    public BasePlayer player;
    private RoundManager roundManager;

    private SurvivorType survivor;
    private CubeCharacterData data;

    private bool isRunning = false;
    private Coroutine voicelineCoroutine = null;

    private void Start()
    {
        roundManager = FindFirstObjectByType<RoundManager>();
        if (roundManager != null)
        {
            roundManager.OnRoundStart += RoundStarted;
        }
    }

    private void RoundStarted()
    {
        StartCoroutine(RoundStartedDelayed());
    }

    // Role assignment RPCs may arrive shortly after OnRoundStart fires, so we
    // wait a couple of frames before reading playerType to avoid a false None result.
    private System.Collections.IEnumerator RoundStartedDelayed()
    {
        yield return null;
        yield return null;

        if (player.GetPlayerType() == BasePlayer.PlayerType.Killer)
        {
            Debug.LogWarning("VoicelineHandler attached to a Killer. This should be on a Survivor.");
            this.enabled = false;
            yield break;
        }

        if (player.GetPlayerType() != BasePlayer.PlayerType.Survivor)
        {
            // Still None — not a survivor this round.
            yield break;
        }

        survivor = player.GetEquippedSurvivor();
        if (survivor == null)
        {
            Debug.LogWarning("VoicelineHandler: No equipped survivor found.");
            yield break;
        }

        data = survivor.character;
        if (data == null || data.voicelines.Length == 0)
        {
            Debug.LogWarning("VoicelineHandler attached to a Survivor that does not have voicelines.");
            this.enabled = false;
            yield break;
        }

        if (!isRunning)
        {
            isRunning = true;
            if (voicelineCoroutine != null) StopCoroutine(voicelineCoroutine);
            voicelineCoroutine = StartCoroutine(IdleVoicelineLoop());
        }
    }

    private IEnumerator IdleVoicelineLoop()
    {
        while (enabled)
        {
            if (player == null || survivor == null || data == null)
            {
                isRunning = false;
                yield break;
            }
            if (player.GetPlayerType() != BasePlayer.PlayerType.Survivor)
            {
                isRunning = false;
                yield break;
            }

            var idleLines = System.Array.FindAll(
                data.voicelines,
                voiceLine => voiceLine.type == Voiceline.VoicelineType.Idle
            );

            if (idleLines.Length > 0)
            {
                int waitTime = UnityEngine.Random.Range(10, 70);
                yield return new WaitForSeconds(waitTime);

                // Re-roll the line after waiting
                var lines = System.Array.FindAll(
                    data.voicelines,
                    voiceLine => voiceLine.type == Voiceline.VoicelineType.Idle
                );
                var selectedLine = lines[UnityEngine.Random.Range(0, lines.Length)];

                AudioSource audioSource = player.GetComponent<AudioSource>();
                if (audioSource != null && selectedLine?.voiceline?.voiceLine != null)
                {
                    audioSource.clip = selectedLine.voiceline.voiceLine;
                    Debug.Log($"Playing voiceline: {selectedLine.voiceline.lineID}");
                    audioSource.Play();
                }
            }
            else
            {
                // No idle lines — stop the loop
                isRunning = false;
                yield break;
            }
        }

        isRunning = false;
    }
}