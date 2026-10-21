using UnityEngine;

[CreateAssetMenu(fileName = "Hype", menuName = "ScriptableObjects/Survivor Abilities/Hype", order = 1)]
public class Hype : PassiveAbility
{
    [Tooltip("Speed multiplier applied to The DJ per Hype stack.")]
    public float speedMultiplier = 1.15f;

    [Tooltip("Stamina regen multiplier applied per Hype stack.")]
    public float staminaRegenMultiplier = 1.25f;

    [Tooltip("Duration of each Hype stack in seconds.")]
    public float stackDuration = 6f;

    [Tooltip("Maximum number of Hype stacks that can be active at once.")]
    public int maxStacks = 3;

    [Tooltip("Radius within which nearby ability activations trigger a Hype stack.")]
    public float detectionRadius = 20f;

    public override void PassiveEffect(BasePlayer player)
    {
        HypeRunner runner = player.GetComponent<HypeRunner>();
        if (runner == null)
            runner = player.gameObject.AddComponent<HypeRunner>();

        runner.Initialize(speedMultiplier, staminaRegenMultiplier, stackDuration, maxStacks, detectionRadius);
    }
}
