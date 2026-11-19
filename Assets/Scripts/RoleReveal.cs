using System;
using System.Collections;
using TMPro;
using UnityEngine;

public class RoleReveal : MonoBehaviour
{
    public RoundManager roundManager;

    GameObject text1;
    GameObject text2;

    private void Start()
    {
        if (roundManager == null)
        {
            var go = GameObject.Find("Multiplayer");
            roundManager =
                go != null
                    ? go.GetComponent<RoundManager>()
                    : FindFirstObjectByType<RoundManager>();
        }
        if (roundManager != null)
        {
            roundManager.OnRoundStart += OnRoundStart;
        }
    }

    private void OnRoundStart()
    {
        StartCoroutine(ShowRoleWhenReady());
    }

    /// <summary>
    /// Waits for the SetPlayerType RPC to arrive before displaying the role banner,
    /// so the text always matches the actual assigned role.
    /// </summary>
    private IEnumerator ShowRoleWhenReady()
    {
        BasePlayer localPlayer = null;

        // Resolve the local player.
        BasePlayer[] players = FindObjectsByType<BasePlayer>(FindObjectsSortMode.None);
        foreach (BasePlayer player in players)
        {
            if (player.photonView.IsMine)
            {
                localPlayer = player;
                break;
            }
        }

        if (localPlayer == null)
        {
            Debug.LogWarning("[RoleReveal] Could not find local BasePlayer.");
            yield break;
        }

        // Wait up to 5 seconds for SetPlayerType RPC to arrive.
        const float timeout = 5f;
        float elapsed = 0f;
        while (elapsed < timeout)
        {
            if (localPlayer.GetPlayerType() != BasePlayer.PlayerType.None)
                break;
            elapsed += Time.deltaTime;
            yield return null;
        }

        BasePlayer.PlayerType type = localPlayer.GetPlayerType();
        if (type == BasePlayer.PlayerType.Killer)
        {
            text1 = CUBEStatusText.FadeInAndCreate(
                LocalizationManager.Get(
                    "role.killer_reveal",
                    "You are the <color=red>KILLER</color>! Hunt down the <color=green>SURVIVORS</color>!"
                ),
                2
            );
            Debug.Log("Created text");
        }
        else if (type == BasePlayer.PlayerType.Survivor)
        {
            text2 = CUBEStatusText.FadeInAndCreate(
                LocalizationManager.Get(
                    "role.survivor_reveal",
                    "You are a <color=green>SURVIVOR</color>! Work together to escape the <color=red>KILLER</color>!"
                ),
                2
            );
            Debug.Log("Created text");
        }

        StartCoroutine(Fade());
    }

    IEnumerator Fade()
    {
        yield return new WaitForSeconds(6);
        CUBEStatusText.FadeOutAndDestroy(text1, 2);
        yield return new WaitForSeconds(2f);
        CUBEStatusText.FadeOutAndDestroy(text2, 2);
    }
}
