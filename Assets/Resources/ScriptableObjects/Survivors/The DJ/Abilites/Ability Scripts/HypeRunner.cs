using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

/// <summary>
/// MonoBehaviour runner for the Hype passive.
/// The DJ gains temporary speed and stamina regeneration stacks whenever abilities are activated
/// or teammates receive healing within the detection radius. Stacks are additive up to a cap.
/// </summary>
public class HypeRunner : PassiveRunner
{
    private float speedMultiplier = 1.15f;
    private float staminaRegenMultiplier = 1.25f;
    private float stackDuration = 6f;
    private int maxStacks = 3;
    private float detectionRadius = 20f;
    private bool initialized;

    // Health snapshots taken each tick to detect heals
    private readonly Dictionary<int, int> lastHealth = new Dictionary<int, int>();

    // Currently active stacks (each entry is a running coroutine expiry)
    private int activeStacks;

    private MovementSC movement;
    private BasePlayerStats myStats;
    private SprintingSC sprinting;
    private CrouchingSC crouching;
    private Transform myHip;

    private float baseStaminaRegen = 50f;

    /// <summary>
    /// Called once by the Hype ScriptableObject to configure the runner.
    /// </summary>
    public void Initialize(float speedMult, float staminaRegenMult, float duration, int maxSt, float radius)
    {
        speedMultiplier = Mathf.Max(1f, speedMult);
        staminaRegenMultiplier = Mathf.Max(1f, staminaRegenMult);
        stackDuration = Mathf.Max(1f, duration);
        maxStacks = Mathf.Max(1, maxSt);
        detectionRadius = Mathf.Max(1f, radius);
        initialized = true;

        CacheReferences();
    }

    private void CacheReferences()
    {
        if (movement == null) movement = GetComponentInChildren<MovementSC>();
        if (myStats == null) myStats = GetComponent<BasePlayerStats>();
        if (sprinting == null)
        {
            sprinting = GetComponentInChildren<SprintingSC>();
            if (sprinting != null)
                baseStaminaRegen = sprinting.staminaPerSecondRegen;
        }
        if (crouching == null) crouching = GetComponentInChildren<CrouchingSC>();
        if (myHip == null)
        {
            var rn = GetComponent<RagdollNetwork>();
            if (rn != null && rn.hip != null)
                myHip = rn.hip;
            else
            {
                var found = transform.Find("RIG/Hip");
                if (found != null)
                    myHip = found;
            }
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        CacheReferences();

        PlayerRPCHandler.OnNetworkAbilityUsed += HandleNetworkAbilityUsed;
    }

    protected override void OnDisable()
    {
        base.OnDisable();

        PlayerRPCHandler.OnNetworkAbilityUsed -= HandleNetworkAbilityUsed;

        ResetStacks();
    }

    private void HandleNetworkAbilityUsed(Vector3 abilityWorldPos, int actorNumber)
    {
        if (!photonView || !photonView.IsMine || !initialized) return;

        CacheReferences();

        Vector3 myPos = myHip != null ? myHip.position : transform.position;
        float dist = Vector3.Distance(myPos, abilityWorldPos);

        if (dist <= detectionRadius)
        {
            AddHypeStack();
        }
    }

    public override void Tick()
    {
        base.Tick();

        if (!photonView.IsMine || !initialized) return;

        CacheReferences();
        DetectHeals();
    }

    private void DetectHeals()
    {
        BasePlayer[] allPlayers = FindObjectsByType<BasePlayer>(FindObjectsSortMode.None);
        Vector3 myPos = myHip != null ? myHip.position : transform.position;

        foreach (BasePlayer player in allPlayers)
        {
            if (player.GetPlayerType() != BasePlayer.PlayerType.Survivor) continue;

            Transform otherHip = player.GetComponent<RagdollNetwork>()?.hip;
            Vector3 otherPos = otherHip != null ? otherHip.position : player.transform.position;

            float dist = Vector3.Distance(myPos, otherPos);
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
                    // A heal was detected inside our radius — grant a Hype stack
                    AddHypeStack();
                }
            }

            lastHealth[viewId] = currentHealth;
        }
    }

    public void AddHypeStack()
    {
        activeStacks = Mathf.Min(activeStacks + 1, maxStacks);
        UpdateBuffs();
        CUBEStatusText.CreateText($"<color=cyan>Hype! x{activeStacks}</color>");
        StartCoroutine(ExpireStack());
    }

    private IEnumerator ExpireStack()
    {
        yield return new WaitForSeconds(stackDuration);
        activeStacks = Mathf.Max(0, activeStacks - 1);
        UpdateBuffs();
    }

    private void UpdateBuffs()
    {
        CacheReferences();

        if (sprinting != null)
        {
            float totalRegenMult = 1f + (staminaRegenMultiplier - 1f) * activeStacks;
            sprinting.staminaPerSecondRegen = baseStaminaRegen * totalRegenMult;
        }

        if (movement != null && myStats != null)
        {
            float totalSpeedMult = 1f + (speedMultiplier - 1f) * activeStacks;
            float effectiveSpeed = myStats.speed * totalSpeedMult;
            ApplySpeed(effectiveSpeed);
        }
    }

    private void ResetStacks()
    {
        activeStacks = 0;

        if (sprinting != null)
        {
            sprinting.staminaPerSecondRegen = baseStaminaRegen;
        }

        if (myStats != null)
        {
            ApplySpeed(myStats.speed > 0 ? myStats.speed : 30f);
        }
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
}
