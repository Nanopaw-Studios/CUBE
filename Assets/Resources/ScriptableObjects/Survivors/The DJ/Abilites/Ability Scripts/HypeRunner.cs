using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

/// <summary>
/// MonoBehaviour runner for the Hype passive.
/// The DJ gains a temporary speed stack each time a survivor inside a detection radius
/// receives healing (tracked via health changes). Stacks are additive up to a cap.
/// </summary>
public class HypeRunner : PassiveRunner
{
    private float speedMultiplier;
    private float stackDuration;
    private int maxStacks;
    private float detectionRadius;
    private bool initialized;

    // Health snapshots taken each tick to detect heals
    private readonly Dictionary<int, int> lastHealth = new Dictionary<int, int>();

    // Currently active stacks (each entry is a running coroutine expiry)
    private int activeStacks;

    private MovementSC movement;
    private BasePlayerStats myStats;

    /// <summary>
    /// Called once by the Hype ScriptableObject to configure the runner.
    /// </summary>
    public void Initialize(float multiplier, float duration, int maxSt, float radius)
    {
        if (initialized) return;
        speedMultiplier = multiplier;
        stackDuration = duration;
        maxStacks = maxSt;
        detectionRadius = radius;
        initialized = true;

        movement = GetComponentInChildren<MovementSC>();
        myStats = GetComponent<BasePlayerStats>();
    }

    public override void Tick()
    {
        base.Tick();

        if (!photonView.IsMine || !initialized) return;

        DetectHeals();
    }

    private void DetectHeals()
    {
        BasePlayer[] allPlayers = FindObjectsByType<BasePlayer>(FindObjectsSortMode.None);

        foreach (BasePlayer player in allPlayers)
        {
            if (player.GetPlayerType() != BasePlayer.PlayerType.Survivor) continue;

            float dist = Vector3.Distance(transform.position, player.transform.position);
            if (dist > detectionRadius) continue;

            int viewId = player.gameObject.GetPhotonView()?.ViewID ?? -1;
            if (viewId < 0) continue;

            BasePlayerStats stats = player.GetComponent<BasePlayerStats>();
            if (stats == null) continue;

            int currentHealth = stats.health;

            if (lastHealth.TryGetValue(viewId, out int previous))
            {
                if (currentHealth > previous && activeStacks < maxStacks)
                {
                    // A heal was detected — grant a Hype stack
                    AddHypeStack();
                }
            }

            lastHealth[viewId] = currentHealth;
        }
    }

    private void AddHypeStack()
    {
        activeStacks = Mathf.Min(activeStacks + 1, maxStacks);
        UpdateSpeed();
        CUBEStatusText.CreateText($"<color=cyan>Hype! x{activeStacks}</color>");
        StartCoroutine(ExpireStack());
    }

    private IEnumerator ExpireStack()
    {
        yield return new WaitForSeconds(stackDuration);
        activeStacks = Mathf.Max(0, activeStacks - 1);
        UpdateSpeed();
    }

    private void UpdateSpeed()
    {
        if (movement == null || myStats == null) return;

        float baseSpeed = myStats.speed;
        // Each stack adds the multiplier bonus on top of base
        float totalMultiplier = 1f + (speedMultiplier - 1f) * activeStacks;
        movement.Force = baseSpeed * totalMultiplier;
    }
}
