using System.Collections;
using Photon.Pun;
using UnityEngine;

/// <summary>
/// MonoBehaviour runner for Heal Proximity.
/// Waits for a startup window during which the killer can cancel the cast by
/// dealing damage to the caster, then heals the nearest survivor.
/// </summary>
public class HealProximityRunner : MonoBehaviourPun
{
    private Coroutine activeCoroutine;

    private int pendingHealAmount;
    private float pendingStartup;
    private BasePlayer caster;
    private bool cancelled;

    /// <summary>
    /// Starts (or restarts) the startup → heal sequence.
    /// </summary>
    public void Execute(int healAmount, float startupDuration, BasePlayer player)
    {
        if (activeCoroutine != null)
            StopCoroutine(activeCoroutine);

        pendingHealAmount = healAmount;
        pendingStartup = startupDuration;
        caster = player;
        cancelled = false;

        activeCoroutine = StartCoroutine(StartupThenHeal());
    }

    /// <summary>
    /// Cancels the pending heal. Called externally when the caster is hit during startup.
    /// </summary>
    public void Cancel()
    {
        cancelled = true;
        if (activeCoroutine != null)
        {
            StopCoroutine(activeCoroutine);
            activeCoroutine = null;
        }
        CUBEStatusText.CreateText("<color=red>Heal Proximity cancelled!</color>");
        Debug.Log("[HealProximityRunner] Heal cancelled by damage.");
    }

    private IEnumerator StartupThenHeal()
    {
        CUBEStatusText.CreateText("Healing...");
        yield return new WaitForSeconds(pendingStartup);

        if (cancelled)
        {
            activeCoroutine = null;
            yield break;
        }

        ApplyHealToClosest();
        activeCoroutine = null;
    }

    private void ApplyHealToClosest()
    {
        BasePlayer closest = null;
        float closestDist = float.MaxValue;

        BasePlayer[] allPlayers = FindObjectsByType<BasePlayer>(FindObjectsSortMode.None);
        foreach (BasePlayer player in allPlayers)
        {
            if (player == caster) continue;
            if (player.GetPlayerType() != BasePlayer.PlayerType.Survivor) continue;

            float dist = Vector3.Distance(caster.transform.position, player.transform.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                closest = player;
            }
        }

        if (closest == null)
        {
            // No other survivors found — heal self
            PhotonView selfPV = caster.gameObject.GetPhotonView();
            if (selfPV != null)
                selfPV.RPC(nameof(BasePlayerStats.Heal), RpcTarget.All, pendingHealAmount);
            return;
        }

        PhotonView targetPV = closest.gameObject.GetPhotonView();
        if (targetPV != null)
            targetPV.RPC(nameof(BasePlayerStats.Heal), RpcTarget.All, pendingHealAmount);

        CUBEStatusText.CreateText($"<color=green>Healed {closest.gameObject.GetPhotonView()?.Owner?.NickName ?? "teammate"} for {pendingHealAmount}!</color>");
        Debug.Log($"[HealProximityRunner] Healed {closest.name} for {pendingHealAmount} HP.");
    }
}
