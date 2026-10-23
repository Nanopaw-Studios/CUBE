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

        StatusEffectManager statusManager = GetComponent<StatusEffectManager>();
        if (statusManager == null)
            statusManager = gameObject.AddComponent<StatusEffectManager>();

        // --- Phase 1: Freeze (Compiling...) ---
        statusManager.ApplyEffect(StatusEffectType.Freeze, 1, freezeDuration);
        yield return new WaitForSeconds(freezeDuration);

        // --- Phase 2: Boost (Speed III) ---
        statusManager.ApplyEffect(StatusEffectType.Speed, 3, boostDuration, boostMultiplier > 0f ? boostMultiplier : 1.75f);

        isRunning = false;
        activeSequence = null;
    }
}
