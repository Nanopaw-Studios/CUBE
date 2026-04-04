using System.Collections;
using Photon.Pun;
using UnityEngine;

/// <summary>
/// MonoBehaviour runner for the Music Circle ability.
/// After a startup window (during which the killer can cancel it by damaging the caster),
/// spawns a circle on the network that temporarily boosts the speed of any survivor inside it.
/// </summary>
public class MusicCircleAbility : MonoBehaviourPun
{
    private Coroutine activeCoroutine;
    private bool cancelled;

    private BasePlayer caster;
    private float boostDuration;
    private float boostMultiplier;
    private float radius;
    private float circleLifetime;
    private float startupDuration;

    /// <summary>
    /// Begins (or restarts) the startup → spawn sequence.
    /// </summary>
    public void Execute(BasePlayer player, float boost, float multiplier, float r, float lifetime, float startup)
    {
        if (activeCoroutine != null)
            StopCoroutine(activeCoroutine);

        caster = player;
        boostDuration = boost;
        boostMultiplier = multiplier;
        radius = r;
        circleLifetime = lifetime;
        startupDuration = startup;
        cancelled = false;

        activeCoroutine = StartCoroutine(StartupThenSpawn());
    }

    /// <summary>
    /// Cancels the pending spawn. Called by PlayerHitboxManager when the caster takes damage.
    /// </summary>
    public void CancelStartup()
    {
        cancelled = true;
        if (activeCoroutine != null)
        {
            StopCoroutine(activeCoroutine);
            activeCoroutine = null;
        }
        CUBEStatusText.CreateText("<color=red>Music Circle cancelled!</color>");
        Debug.Log("[MusicCircleAbility] Cancelled during startup.");
    }

    private IEnumerator StartupThenSpawn()
    {
        CUBEStatusText.CreateText("Placing Music Circle...");
        yield return new WaitForSeconds(startupDuration);

        if (cancelled)
        {
            activeCoroutine = null;
            yield break;
        }

        Vector3 spawnPos = caster.transform.Find("RIG")?.Find("Foot_Right")?.position
                           ?? caster.transform.position;

        // Spawn the circle prefab across the network (reuses HealCirclePrefab as the visual)
        GameObject circle = PhotonNetwork.Instantiate(
            "MusicCirclePrefab",
            spawnPos,
            Quaternion.identity
        );

        MusicCircleFunction func = circle.GetComponent<MusicCircleFunction>();
        if (func != null)
        {
            func.boostDuration = boostDuration;
            func.boostMultiplier = boostMultiplier;
            func.radius = radius;
            func.lifetime = circleLifetime;
        }

        activeCoroutine = null;
    }
}
