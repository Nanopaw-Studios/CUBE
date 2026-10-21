using Photon.Pun;
using Unity.Collections;
using UnityEngine;

public class PassiveAbility : Ability
{
    public PassiveAbility()
    {
        isPassive = true;
    }

    private void OnEnable()
    {
        isPassive = true;
    }

    public override void ActivateAbility(BasePlayer player)
    {
        Debug.Log($"{AbilityName} is a passive ability and cannot be activated.");
    }

    /// <summary>
    /// Applies the passive effect of the ability to the specified player.
    /// Passive effects are always active and cannot be activated.
    /// </summary>
    /// <param name="player">The player to whom the passive effect is applied. Cannot be null.</param>
    public virtual void PassiveEffect(BasePlayer player)
    {
        Debug.Log(
            $"{AbilityName} passive effect applied to "
                + player.gameObject.GetPhotonView().Owner.NickName
        );
    }
}
