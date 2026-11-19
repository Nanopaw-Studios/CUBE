using Photon.Pun;
using UnityEngine;

/// <summary>
/// MonoBehaviour runner for the Last Stand passive.
/// Triggers emergency damage resistance and movement speed buffs when The Healer's
/// health drops dangerously low (critical), or when the team's average health collapses.
/// </summary>
public class LastStandRunner : PassiveRunner
{
    private float baseSpeed = 35f;
    private float maxSpeedMultiplier = 1.6f;
    private float criticalHealthThreshold = 0.35f;
    private float damageResistance = 0.30f;
    private bool initialized;

    private BasePlayerStats myStats;
    private MovementSC movement;
    private SprintingSC sprinting;
    private CrouchingSC crouching;

    private bool isLastStandActive = false;

    public void Initialize(float speed, float multiplier, float threshold, float resistance)
    {
        baseSpeed = speed > 0 ? speed : 35f;
        maxSpeedMultiplier = Mathf.Max(1f, multiplier);
        criticalHealthThreshold = Mathf.Clamp(threshold, 0.1f, 0.9f);
        damageResistance = Mathf.Clamp01(resistance);
        initialized = true;

        CacheReferences();
    }

    private void CacheReferences()
    {
        if (myStats == null) myStats = GetComponent<BasePlayerStats>();
        if (movement == null) movement = GetComponentInChildren<MovementSC>();
        if (sprinting == null) sprinting = GetComponentInChildren<SprintingSC>();
        if (crouching == null) crouching = GetComponentInChildren<CrouchingSC>();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        CacheReferences();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        DeactivateLastStand();
    }

    public override void Tick()
    {
        base.Tick();

        if (!photonView.IsMine || !initialized) return;

        CacheReferences();
        if (myStats == null || movement == null) return;

        float selfHealthPercent = myStats.maxHealth > 0 ? (float)myStats.health / myStats.maxHealth : 1f;
        float teamAverageHealthPercent = GetTeamAverageHealthPercent();

        // Activates if either The Healer is in critical condition or the team average is critical
        bool shouldActivate = selfHealthPercent <= criticalHealthThreshold || teamAverageHealthPercent <= criticalHealthThreshold;

        if (shouldActivate)
        {
            float lowestPercent = Mathf.Min(selfHealthPercent, teamAverageHealthPercent);
            float severity = Mathf.Clamp01(1f - (lowestPercent / criticalHealthThreshold)); // 0 at threshold, 1 at 0 HP
            float speedMult = Mathf.Lerp(1.15f, maxSpeedMultiplier, severity);

            // Apply damage resistance: full resistance if self is critical, partial if team is critical
            if (selfHealthPercent <= criticalHealthThreshold)
                myStats.SetDamageResistance(damageResistance);
            else
                myStats.SetDamageResistance(damageResistance * 0.5f);

            ApplySpeed(baseSpeed * speedMult);

            if (!isLastStandActive)
            {
                isLastStandActive = true;
                CUBEStatusText.CreateText("<color=yellow>Last Stand Activated!</color>");
            }
        }
        else if (isLastStandActive)
        {
            DeactivateLastStand();
        }
    }

    private void DeactivateLastStand()
    {
        isLastStandActive = false;
        if (myStats != null)
        {
            myStats.SetDamageResistance(0f);
        }

        float normalSpeed = myStats != null && myStats.speed > 0 ? myStats.speed : baseSpeed;
        ApplySpeed(normalSpeed);
    }

    private void ApplySpeed(float targetBaseSpeed)
    {
        if (movement == null) return;

        if (sprinting != null && sprinting.isSprinting)
        {
            movement.Force = targetBaseSpeed * sprinting.sprintMultiplier;
        }
        else if (crouching != null && crouching.isCrouched)
        {
            movement.Force = targetBaseSpeed * crouching.crouchSpeedMultiplier;
        }
        else
        {
            movement.Force = targetBaseSpeed;
        }

        if (sprinting != null)
        {
            sprinting.preSprintForce = targetBaseSpeed;
        }
    }

    private float GetTeamAverageHealthPercent()
    {
        BasePlayer[] allPlayers = FindObjectsByType<BasePlayer>(FindObjectsSortMode.None);

        int totalHealth = 0;
        int totalMax = 0;
        int count = 0;

        foreach (BasePlayer p in allPlayers)
        {
            if (p.GetPlayerType() != BasePlayer.PlayerType.Survivor) continue;

            BasePlayerStats stats = p.GetComponent<BasePlayerStats>();
            if (stats == null) continue;

            totalHealth += stats.health;
            totalMax += stats.maxHealth > 0 ? stats.maxHealth : 100;
            count++;
        }

        if (count == 0 || totalMax == 0) return 1f;
        return Mathf.Clamp01((float)totalHealth / totalMax);
    }
}
