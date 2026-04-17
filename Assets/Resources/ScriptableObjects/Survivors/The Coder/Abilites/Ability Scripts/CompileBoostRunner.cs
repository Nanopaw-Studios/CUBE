using System.Collections;
using UnityEngine;

/// <summary>
/// MonoBehaviour runner for the Compile Boost ability.
/// Freezes the player momentarily, then applies a temporary speed boost.
/// </summary>
public class CompileBoostRunner : MonoBehaviour
{
    private Coroutine activeSequence;
    private bool isRunning = false;

    /// <summary>
    /// Starts (or restarts) the freeze → boost sequence.
    /// If already running, restores movement first before re-executing.
    /// </summary>
    public void Execute(float freezeDuration, float boostDuration, float boostMultiplier)
    {
        // Guard: don't let the ability stack during freeze phase
        if (isRunning) return;

        if (activeSequence != null)
            StopCoroutine(activeSequence);

        activeSequence = StartCoroutine(FreezeAndBoost(freezeDuration, boostDuration, boostMultiplier));
    }

    private IEnumerator FreezeAndBoost(float freezeDuration, float boostDuration, float boostMultiplier)
    {
        isRunning = true;

        MovementSC movement = GetComponentInChildren<MovementSC>();
        if (movement == null)
        {
            Debug.LogError("[CompileBoostRunner] MovementSC not found — cannot apply Compile Boost.");
            isRunning = false;
            activeSequence = null;
            yield break;
        }

        // Capture base speed from stats so the restore is always correct,
        // even if Force was already modified by sprint or another effect.
        BasePlayerStats stats = GetComponent<BasePlayerStats>();
        float baseForce = stats != null ? stats.speed : movement.Force;

        // --- Phase 1: Freeze ---
        movement.Force = 0f;
        yield return new WaitForSeconds(freezeDuration);

        // --- Phase 2: Boost ---
        movement.Force = baseForce * boostMultiplier;
        yield return new WaitForSeconds(boostDuration);

        // --- Restore ---
        movement.Force = baseForce;

        isRunning = false;
        activeSequence = null;
    }
}
