using Photon.Pun;
using UnityEngine;

/// <summary>
/// MonoBehaviour runner for the Last Stand passive.
/// Scales The Healer's movement speed inversely with the team's average health:
/// the lower the team's average HP, the faster she moves.
/// </summary>
public class LastStandRunner : PassiveRunner
{
    private float baseSpeed;
    private float maxSpeedMultiplier;
    private bool initialized;

    /// <summary>
    /// Sets the speed parameters. Called once by the LastStand ScriptableObject.
    /// </summary>
    public void Initialize(float speed, float multiplier)
    {
        if (initialized) return;
        baseSpeed = speed;
        maxSpeedMultiplier = multiplier;
        initialized = true;
    }

    public override void Tick()
    {
        base.Tick();

        if (!photonView.IsMine) return;
        if (!initialized) return;

        MovementSC movement = GetComponentInChildren<MovementSC>();
        if (movement == null) return;

        float teamAverageHealthPercent = GetTeamAverageHealthPercent();

        // As team health goes from 100% → 0%, speed goes from baseSpeed → baseSpeed * maxSpeedMultiplier
        float speedMultiplier = Mathf.Lerp(maxSpeedMultiplier, 1f, teamAverageHealthPercent);
        movement.Force = baseSpeed * speedMultiplier;
    }

    /// <summary>
    /// Returns the team's average health as a 0–1 fraction (1 = full health, 0 = all dead).
    /// </summary>
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
