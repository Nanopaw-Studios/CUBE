using Photon.Pun;
using UnityEngine;

/// <summary>
/// Passive runner for The Coder's Overclocked Mind ability.
/// Accelerates active ability cooldown progression by 10% whenever The Coder is not in a chase.
/// </summary>
public class OverclockedMindRunner : PassiveRunner
{
    private PlayerAbilityManager abilityManager;

    protected override void OnEnable()
    {
        base.OnEnable();

        if (!photonView || !photonView.IsMine) return;

        abilityManager = FindFirstObjectByType<PlayerAbilityManager>();
        if (abilityManager != null)
        {
            abilityManager.SetOverclockedMindActive(true);
        }

        CUBEStatusText.CreateText("<color=green>Overclocked Mind: Active</color>");
    }

    protected override void OnDisable()
    {
        base.OnDisable();

        if (abilityManager != null)
        {
            abilityManager.SetOverclockedMindActive(false);
        }
    }

    public override void Tick()
    {
        base.Tick();

        if (!photonView.IsMine) return;

        if (abilityManager == null)
        {
            abilityManager = FindFirstObjectByType<PlayerAbilityManager>();
            if (abilityManager != null)
            {
                abilityManager.SetOverclockedMindActive(true);
            }
        }
    }
}
