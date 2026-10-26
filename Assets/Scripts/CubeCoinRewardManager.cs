using System.Collections;
using Photon.Pun;
using UnityEngine;

/// <summary>
/// Tracks all in-round Cube Coin reward conditions for the LOCAL player only and
/// forwards earned amounts to PlayFab via PlayfabManager.AddCubeCoins().
///
/// Attach this to the player prefab root (same object as PhotonView, BasePlayer, etc.).
///
/// Reward schedule (per round):
///   Low-health win  — 10–50 CC scaled inversely with remaining HP (capped at 19 HP)
///   Distance        — 5 CC per 100 m traveled (tracked every second, round only)
///   Ability used    — 15 CC per successful activation (up to 20 per round)
///   Killer kill     — 25 CC per survivor eliminated (killer-only)
///   Killer win      — 30 CC when the killer's team wins
///   Survivor win    — 15 CC when the round ends with the survivor still alive
///   Ragequit escape — 40 CC when a survivor disconnects mid-chase
/// </summary>
[RequireComponent(typeof(PhotonView))]
[RequireComponent(typeof(BasePlayer))]
[RequireComponent(typeof(BasePlayerStats))]
public class CubeCoinRewardManager : MonoBehaviourPunCallbacks
{
    // ── Reward amounts ────────────────────────────────────────────────
    private const int LowHpWinMin = 10;
    private const int LowHpWinMax = 50;
    private const int LowHpWinThreshold = 20; // HP must be below this
    private const int DistanceCCPerSegment = 5;
    private const float DistanceSegmentMeters = 100f;
    private const int AbilityUsedCC = 5;
    private const int KillerKillCC = 25;
    private const int KillerWinCC = 30;
    private const int SurvivorWinCC = 15;
    private const int RagequitEscapeCC = 40;

    // ── Per-round abuse caps ──────────────────────────────────────────
    private const int MaxAbilityRewardsPerRound = 20;
    private const int MaxDistanceRewardsPerRound = 10000; // 10 000 m max

    // ── State ─────────────────────────────────────────────────────────
    private BasePlayer basePlayer;
    private BasePlayerStats playerStats;
    private ChaseHandler chaseHandler;
    private PlayerAbilityManager abilityManager;
    private RoundManager roundManager;

    private bool roundActive;
    private bool didGrantEndRoundReward;

    // Distance
    private Vector3 lastPosition;
    private float distanceAccumulator;
    private int distanceRewardsGrantedThisRound;
    private Coroutine distanceCoroutine;

    // Ability
    private int abilityRewardsGrantedThisRound;

    // ─────────────────────────────────────────────────────────────────

    private void Awake()
    {
        basePlayer = GetComponent<BasePlayer>();
        playerStats = GetComponent<BasePlayerStats>();
        chaseHandler = GetComponent<ChaseHandler>();
        abilityManager = GetComponentInChildren<PlayerAbilityManager>(true);
    }

    private void Start()
    {
        // Only the local player runs reward logic.
        if (!photonView.IsMine)
        {
            enabled = false;
            return;
        }

        roundManager = FindFirstObjectByType<RoundManager>();
        if (roundManager != null)
        {
            roundManager.OnRoundStart += OnRoundStart;
            roundManager.OnIntermissionStart += OnIntermissionStart;
        }
    }

    private void OnDestroy()
    {
        if (roundManager != null)
        {
            roundManager.OnRoundStart -= OnRoundStart;
            roundManager.OnIntermissionStart -= OnIntermissionStart;
        }
    }

    // ── Round lifecycle ───────────────────────────────────────────────

    private void OnRoundStart()
    {
        roundActive = true;
        didGrantEndRoundReward = false;
        distanceAccumulator = 0f;
        distanceRewardsGrantedThisRound = 0;
        abilityRewardsGrantedThisRound = 0;
        lastPosition = GetTrackedPosition();

        if (distanceCoroutine != null)
            StopCoroutine(distanceCoroutine);
        distanceCoroutine = StartCoroutine(DistanceTracker());
    }

    private Vector3 GetTrackedPosition()
    {
        RagdollNetwork ragdoll = GetComponent<RagdollNetwork>();
        if (ragdoll != null && ragdoll.hip != null)
            return ragdoll.hip.position;
        return transform.position;
    }

    private void OnIntermissionStart()
    {
        // Intermission = round just ended. Grant end-of-round rewards once.
        if (roundActive && !didGrantEndRoundReward)
            GrantEndRoundRewards();

        roundActive = false;
        if (distanceCoroutine != null)
        {
            StopCoroutine(distanceCoroutine);
            distanceCoroutine = null;
        }
    }

    // ── Distance tracking ─────────────────────────────────────────────

    private IEnumerator DistanceTracker()
    {
        var wait = new WaitForSeconds(1f);
        while (roundActive)
        {
            Vector3 currentPos = GetTrackedPosition();
            float delta = Vector3.Distance(currentPos, lastPosition);
            lastPosition = currentPos;
            distanceAccumulator += delta;

            int completedSegments = 0;
            while (
                distanceAccumulator >= DistanceSegmentMeters
                && distanceRewardsGrantedThisRound + completedSegments < MaxDistanceRewardsPerRound
            )
            {
                distanceAccumulator -= DistanceSegmentMeters;
                completedSegments++;
            }

            if (completedSegments > 0)
            {
                distanceRewardsGrantedThisRound += completedSegments;
                int totalCoins = completedSegments * DistanceCCPerSegment;
                GrantCC(totalCoins, $"distance_{distanceRewardsGrantedThisRound}x100m");
            }

            yield return wait;
        }
    }

    // ── Ability reward ────────────────────────────────────────────────

    /// <summary>
    /// Call this from PlayerAbilityManager when an active ability is successfully activated.
    /// </summary>
    public void OnAbilitySuccessfullyUsed()
    {
        if (!roundActive)
            return;
        if (abilityRewardsGrantedThisRound >= MaxAbilityRewardsPerRound)
            return;

        abilityRewardsGrantedThisRound++;
        GrantCC(AbilityUsedCC, "ability_used");
    }

    // ── Kill reward (killer only) ─────────────────────────────────────

    /// <summary>
    /// Call this from PlayerHitboxManager on the killing blow when a survivor's HP hits 0.
    /// Only fires for the local killer client.
    /// </summary>
    public void OnKilledSurvivor()
    {
        if (!roundActive)
            return;
        if (basePlayer.GetPlayerType() != BasePlayer.PlayerType.Killer)
            return;

        GrantCC(KillerKillCC, "killer_kill");
    }

    // ── Ragequit escape reward (survivor's perspective) ───────────────

    public override void OnPlayerLeftRoom(Photon.Realtime.Player otherPlayer)
    {
        if (!roundActive)
            return;
        if (basePlayer.GetPlayerType() != BasePlayer.PlayerType.Survivor)
            return;

        // Reward this survivor only if they are actively being chased when the killer leaves.
        if (chaseHandler == null || !chaseHandler.inChase)
            return;

        // Check whether the player who left was the killer.
        foreach (GameObject go in GameObject.FindGameObjectsWithTag("Player"))
        {
            PhotonView pv = go.GetComponent<PhotonView>();
            if (pv == null || pv.Owner != otherPlayer)
                continue;

            BasePlayer leavingPlayer = go.GetComponent<BasePlayer>();
            if (
                leavingPlayer != null
                && leavingPlayer.GetPlayerType() == BasePlayer.PlayerType.Killer
            )
            {
                GrantCC(RagequitEscapeCC, "ragequit_escape");
                break;
            }
        }
    }

    // ── End-of-round rewards ──────────────────────────────────────────

    private void GrantEndRoundRewards()
    {
        didGrantEndRoundReward = true;
        var role = basePlayer.GetPlayerType();

        if (role == BasePlayer.PlayerType.Killer)
        {
            // Check whether the killer won: all survivors must be at 0 HP.
            bool killerWon = AllSurvivorsDead();
            if (killerWon)
                GrantCC(KillerWinCC, "killer_win");

            // Low-HP win bonus — only applies if the killer won.
            if (killerWon && playerStats.health < LowHpWinThreshold && playerStats.health > 0)
                GrantCC(CalculateLowHpBonus(playerStats.health), "low_hp_win");
        }
        else if (role == BasePlayer.PlayerType.Survivor)
        {
            // Survivor wins if they are still alive.
            if (playerStats.health > 0)
            {
                GrantCC(SurvivorWinCC, "survivor_win");

                // Low-HP win bonus for survivors who survived with less than 20 HP.
                if (playerStats.health < LowHpWinThreshold)
                    GrantCC(CalculateLowHpBonus(playerStats.health), "low_hp_win");
            }
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Scales from LowHpWinMin (at 19 HP) to LowHpWinMax (at 1 HP).
    /// </summary>
    private int CalculateLowHpBonus(int currentHp)
    {
        // Clamp hp into [1, LowHpWinThreshold-1]
        currentHp = Mathf.Clamp(currentHp, 1, LowHpWinThreshold - 1);
        float t = 1f - ((float)(currentHp - 1) / (LowHpWinThreshold - 2));
        return Mathf.RoundToInt(Mathf.Lerp(LowHpWinMin, LowHpWinMax, t));
    }

    private bool AllSurvivorsDead()
    {
        BasePlayer[] all = FindObjectsByType<BasePlayer>(FindObjectsSortMode.None);
        foreach (BasePlayer p in all)
        {
            if (p.GetPlayerType() != BasePlayer.PlayerType.Survivor)
                continue;
            BasePlayerStats s = p.GetComponent<BasePlayerStats>();
            if (s != null && s.health > 0)
                return false;
        }
        return true;
    }

    private void GrantCC(int amount, string reason)
    {
        if (PlayfabManager.Instance == null)
        {
            Debug.LogWarning(
                $"[CubeCoinRewardManager] PlayfabManager not found — could not grant {amount} CC ({reason})."
            );
            return;
        }
        PlayfabManager.Instance.AddCubeCoins(amount, reason);
        CUBEStatusText.CreateText(
            $"<color=yellow>+{amount} CC</color> ({reason.Replace('_', ' ')})"
        );
        Debug.Log($"[CubeCoinReward] +{amount} CC | reason={reason}");
    }
}
