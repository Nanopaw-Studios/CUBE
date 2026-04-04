using Photon.Pun;
using UnityEngine;

[CreateAssetMenu(fileName = "CompileBoost", menuName = "ScriptableObjects/Survivor Abilities/CompileBoost", order = 1)]
public class CompileBoost : Ability
{
    GameObject playerObj;

    public override void ActivateAbility(BasePlayer player)
    {
        base.ActivateAbility(player);
        playerObj = player.gameObject;

        BoostCompileSpeed();
    }

    public void BoostCompileSpeed()
    {
        MovementSC movement = playerObj.GetComponentInChildren<MovementSC>();

        float speed = movement.Force * 2;
        movement.Force = speed;

        playerObj.GetComponent<BasePlayerStats>().photonView.RPC("TakeDamage", RpcTarget.Others, 5);

        // Reset to 1 instead of original force
        movement.ResetAfter(2.5f, 1f);
    }
}