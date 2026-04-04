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
        survivor = player.GetEquippedSurvivor();
        data = survivor.character;

        if (player.GetPlayerType() == BasePlayer.PlayerType.Killer)
        {
            Debug.LogWarning("VoicelineHandler attached to a Killer. This should be on a Survivor.");
            this.enabled = false;
            return;
        }
        else if (player.GetPlayerType() == BasePlayer.PlayerType.Survivor && data.voicelines.Length == 0)
        {
            Debug.LogWarning("VoicelineHandler attached to a Survivor that does not have voicelines.");
            this.enabled = false;
            return;
        }

        if (!isRunning)
        {
            isRunning = true;
            StartCoroutine(IdleVoicelineLoop());
        }
    }

    private IEnumerator IdleVoicelineLoop()
    {
        while (enabled)
        {
            if (player == null || survivor == null || data == null) yield break;
            if (player.GetPlayerType() != BasePlayer.PlayerType.Survivor) yield break;

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
                yield break;
            }
        }
    }
}