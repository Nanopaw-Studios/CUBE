using System.Collections;
using Photon.Pun;
using UnityEngine;

/// <summary>
/// MonoBehaviour runner for the Equalizer ability.
/// Waits during a short startup (killer can cancel by hitting the caster), then spawns
/// a wall on the network that slows the killer on contact. The wall has no physical collision.
/// </summary>
public class EqualizerAbility : MonoBehaviourPun
{
    private Coroutine activeCoroutine;
    private bool cancelled;

    private BasePlayer caster;
    private float killerSlowMultiplier;
    private float wallLifetime;
    private float startupDuration;

    /// <summary>
    /// Begins (or restarts) the startup → spawn sequence.
    /// </summary>
    public void Execute(BasePlayer player, float slowMultiplier, float lifetime, float startup)
    {
        if (activeCoroutine != null)
            StopCoroutine(activeCoroutine);

        caster = player;
        killerSlowMultiplier = slowMultiplier;
        wallLifetime = lifetime;
        startupDuration = startup;
        cancelled = false;

        activeCoroutine = StartCoroutine(StartupThenSpawn());
    }

    /// <summary>
    /// Cancels the pending spawn. Called by PlayerHitboxManager when the caster is hit.
    /// </summary>
    public void CancelStartup()
    {
        cancelled = true;
        if (activeCoroutine != null)
        {
            StopCoroutine(activeCoroutine);
            activeCoroutine = null;
        }
        CUBEStatusText.CreateText("<color=red>Equalizer cancelled!</color>");
        Debug.Log("[EqualizerAbility] Cancelled during startup.");
    }

    private IEnumerator StartupThenSpawn()
    {
        CUBEStatusText.CreateText("Placing Equalizer...");
        yield return new WaitForSeconds(startupDuration);

        if (cancelled)
        {
            activeCoroutine = null;
            yield break;
        }

        // Spawn in front of the caster so the wall faces them
        Vector3 spawnPos = caster.transform.position + caster.transform.forward * 1.5f;
        Quaternion spawnRot = caster.transform.rotation;

        GameObject wall = PhotonNetwork.Instantiate(
            "EqualizerWallPrefab",
            spawnPos,
            spawnRot
        );

        EqualizerWallFunction func = wall.GetComponent<EqualizerWallFunction>();
        if (func != null)
        {
            func.killerSlowMultiplier = killerSlowMultiplier;
            func.lifetime = wallLifetime;
        }

        activeCoroutine = null;
    }
}
