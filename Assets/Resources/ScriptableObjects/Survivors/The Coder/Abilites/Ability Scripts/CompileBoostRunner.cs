using System.Collections;
using UnityEngine;

/// <summary>
/// MonoBehaviour runner for the Compile Boost ability.
/// Freezes the player momentarily, then applies a temporary speed boost.
/// </summary>
public class CompileBoostRunner : MonoBehaviour
{
    private Coroutine activeSequence;

    /// <summary>
    /// Starts (or restarts) the freeze → boost sequence.
    /// </summary>
    public void Execute(float freezeDuration, float boostDuration, float boostMultiplier)
    {
        if (activeSequence != null)
            StopCoroutine(activeSequence);

        activeSequence = StartCoroutine(FreezeAndBoost(freezeDuration, boostDuration, boostMultiplier));
    }

    private IEnumerator FreezeAndBoost(float freezeDuration, float boostDuration, float boostMultiplier)
    {
        MovementSC movement = GetComponentInChildren<MovementSC>();
        if (movement == null)
        {
            Debug.LogError("[CompileBoostRunner] MovementSC not found — cannot apply Compile Boost.");
            activeSequence = null;
            yield break;
        }

        float originalForce = movement.Force;

        // --- Phase 1: Freeze ---
        movement.Force = 0f;
        yield return new WaitForSeconds(freezeDuration);

        // --- Phase 2: Boost ---
        movement.Force = originalForce * boostMultiplier;
        yield return new WaitForSeconds(boostDuration);

        // --- Restore ---
        // Only restore if Force wasn't changed externally (e.g. sprint, another ability).
        // If it was changed, leave it as-is to respect the newer modifier.
        if (Mathf.Approximately(movement.Force, originalForce * boostMultiplier))
            movement.Force = originalForce;

        activeSequence = null;
    }
}
