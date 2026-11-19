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
        BasePlayer killer = FindActiveKiller();
        float preDist = killer != null ? Vector3.Distance(GetBodyPos(transform), GetBodyPos(killer.transform)) : float.MaxValue;

        statusManager.ApplyEffect(StatusEffectType.Speed, 3, boostDuration, boostMultiplier > 0f ? boostMultiplier : 1.75f);

        yield return new WaitForSeconds(boostDuration);

        BasePlayerStats myStats = GetComponent<BasePlayerStats>();
        if (killer != null && preDist <= 22f && (myStats == null || myStats.health > 0))
        {
            float postDist = Vector3.Distance(GetBodyPos(transform), GetBodyPos(killer.transform));
            if (postDist - preDist >= 3.0f)
            {
                CubeCoinRewardManager.NotifyLocalSuccessfulAbility("compile_boost_escape", 35);
            }
        }

        isRunning = false;
        activeSequence = null;
    }

    private static BasePlayer FindActiveKiller()
    {
        BasePlayer[] all = FindObjectsByType<BasePlayer>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].GetPlayerType() == BasePlayer.PlayerType.Killer)
                return all[i];
        }
        return null;
    }

    private static Vector3 GetBodyPos(Transform root)
    {
        RagdollNetwork rag = root.GetComponent<RagdollNetwork>();
        if (rag != null && rag.hip != null)
            return rag.hip.position;
        return root.position;
    }
}

