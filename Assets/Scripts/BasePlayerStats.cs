using EZCameraShake;
using Photon.Pun;
using UnityEngine;

public class BasePlayerStats : MonoBehaviourPunCallbacks
{
    // health: duh
    public int health = 100;
    // stamina: used for sprinting, dodging, etc.
    public int stamina = 100;
    // speed: affects movement speed
    public int speed = 5;
    // malice: your chance of being killer, higher is more chance, lower is less chance
    public int malice = 1;
    // cc: cube coins: money! duh!
    public int money = 100;

    /// <summary>
    /// Maximum stamina for this character, set from the ScriptableObject at round start.
    /// </summary>
    public int maxStamina = 100;

    /// <summary>
    /// Applies base stats from the equipped character's ScriptableObject at round start.
    /// Called via RPC from ServerManager so all clients have consistent values.
    /// </summary>
    [PunRPC]
    public void ApplyCharacterStats(int characterHealth, float characterSpeed, int characterStamina)
    {
        health = characterHealth > 0 ? characterHealth : 100;
        maxHealth = health;
        maxStamina = characterStamina > 0 ? characterStamina : 100;
        stamina = maxStamina;
        speed = characterSpeed > 0 ? (int)characterSpeed : 5;

        // Push base speed into MovementSC so walk force matches the character's ScriptableObject speed.
        // MovementSC.Force acts as the "current speed" and can be modified at runtime by abilities/effects.
        MovementSC movement = GetComponentInChildren<MovementSC>();
        if (movement != null)
            movement.Force = speed;

        Debug.Log($"[BasePlayerStats] Stats applied — health={health}, speed={speed}, stamina={stamina}/{maxStamina}");
    }

    /// <summary>Maximum health for this character, set from the ScriptableObject at round start.</summary>
    public int maxHealth = 100;

    [PunRPC]
    public void TakeDamage(int damage)
    {
        CameraShaker.Instance.ShakeOnce(3f * (damage / 1000), 4f, .1f, 1f);

        health -= damage;
        if (health < 0) health = 0;
    }
    [PunRPC]
    public void Heal(int amount)
    {
        health += amount;
        if (health > maxHealth) health = maxHealth;
    }
    [PunRPC]
    public void UseStamina(int amount)
    {
        stamina -= amount;
        if (stamina < 0) stamina = 0;
    }
    [PunRPC]
    public void RecoverStamina(int amount)
    {
        stamina += amount;
        if (stamina > 100) stamina = 100;
    }
    [PunRPC]
    public void GrantMalice(int amount)
    {
        CUBEStatusText.CreateText("You feel something <color=red>malicious</color> take over you..");
        malice += amount;
    }
    [PunRPC]
    public void RemoveMalice(int amount)
    {
        CUBEStatusText.CreateText("You feel less malicious.");
        malice -= amount;
    }
    [PunRPC]
    public void GrantMoney(int amount)
    {
        money += amount;
    }
    [PunRPC]
    public void RemoveMoney(int amount)
    {
        money -= amount;
    }

    [PunRPC]
    public void AdjustMalice(int amount)
    {
        CUBEStatusText.CreateText("You feel something <color=red>malicious</color> take over you..");

        malice += amount;
        if (malice < 0) malice = 0;
        Debug.Log($"{photonView.Owner.NickName} malice now {malice}");
    }
}
