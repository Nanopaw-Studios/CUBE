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
    public int speed = 35;

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
        speed = characterSpeed > 0 ? (int)characterSpeed : 35;

        // Push base speed into MovementSC so walk force matches the character's ScriptableObject speed.
        // MovementSC.Force acts as the "current speed" and can be modified at runtime by abilities/effects.
        MovementSC movement = GetComponentInChildren<MovementSC>();
        if (movement != null)
        {
            CrouchingSC crouching = GetComponentInChildren<CrouchingSC>();
            if (crouching != null && crouching.isCrouched)
                movement.Force = speed * crouching.crouchSpeedMultiplier;
            else
                movement.Force = speed;
        }

        Debug.Log(
            $"[BasePlayerStats] Stats applied — health={health}, speed={speed}, stamina={stamina}/{maxStamina}"
        );
    }

    [PunRPC]
    public void ResetToDefaultStats()
    {
        health = 100;
        maxHealth = 100;

        stamina = 100;
        maxStamina = 100;

        speed = 35;
        damageResistance = 0f;

        // 1. Reset Movement
        MovementSC movement = GetComponentInChildren<MovementSC>();
        if (movement != null)
        {
            movement.enabled = true;
            movement.Force = speed;
        }

        // 2. Reset Sprinting
        SprintingSC sprinting = GetComponentInChildren<SprintingSC>();
        if (sprinting != null)
        {
            sprinting.ResetSprintState();
        }

        // 3. Reset Crouching
        CrouchingSC crouching = GetComponentInChildren<CrouchingSC>();
        if (crouching != null)
        {
            crouching.ResetCrouchState();
        }

        // 3. Destroy dynamic ability runner components attached to player
        PassiveRunner[] passiveRunners = GetComponents<PassiveRunner>();
        foreach (var runner in passiveRunners)
        {
            if (runner != null)
                Destroy(runner);
        }
        PassiveRunner[] passiveRunnersInChild = GetComponentsInChildren<PassiveRunner>();
        foreach (var runner in passiveRunnersInChild)
        {
            if (runner != null)
                Destroy(runner);
        }

        MonoBehaviour[] allComponents = GetComponents<MonoBehaviour>();
        foreach (var mb in allComponents)
        {
            if (mb is CompileBoostRunner || mb is MusicBoostResetter)
            {
                Destroy(mb);
            }
        }

        // 4. Clear status effects
        StatusEffectReceiver statusEffects = GetComponent<StatusEffectReceiver>();
        if (statusEffects != null)
        {
            statusEffects.ClearAllEffects();
        }

        StatusEffectManager statusEffectManager = GetComponent<StatusEffectManager>();
        if (statusEffectManager != null)
        {
            statusEffectManager.ClearAllEffects();
        }

        // 5. Reset death state and rig control
        DeathSC death = GetComponent<DeathSC>();
        if (death != null)
        {
            death.ResetDeathState();
        }

        Rig rig = GetComponent<Rig>();
        if (rig != null)
        {
            rig.control = 1;
        }

        // 6. Reset role type
        BasePlayer basePlayer = GetComponent<BasePlayer>();
        if (basePlayer != null)
        {
            basePlayer.SetPlayerType(BasePlayer.PlayerType.None);
        }
    }

    /// <summary>Maximum health for this character, set from the ScriptableObject at round start.</summary>
    public int maxHealth = 100;

    /// <summary>Fractional damage resistance (0 to 1). When > 0, incoming damage is reduced.</summary>
    public float damageResistance = 0f;

    /// <summary>Fired on the local player client when taking damage.</summary>
    public event System.Action<int> OnDamageTaken;

    private CameraShaker localCameraShaker;

    [PunRPC]
    public void TakeDamage(int damage)
    {
        int actualDamage = Mathf.Max(0, damage);
        if (damageResistance > 0f)
        {
            actualDamage = Mathf.Max(1, Mathf.RoundToInt(actualDamage * (1f - Mathf.Clamp01(damageResistance))));
        }

        StatusEffectManager statusManager = GetComponent<StatusEffectManager>();
        if (statusManager != null)
        {
            actualDamage = Mathf.Max(1, Mathf.RoundToInt(actualDamage * statusManager.GetDamageTakenMultiplier()));
        }

        health -= actualDamage;
        if (health < 0)
            health = 0;

        if (photonView.IsMine)
        {
            if (localCameraShaker == null)
            {
                localCameraShaker = GetComponentInChildren<CameraShaker>();
            }

            if (localCameraShaker != null)
            {
                localCameraShaker.ShakeOnce(1.4f, 4f, 0f, 0.7f);
            }
            else if (CameraShaker.Instance != null)
            {
                CameraShaker.Instance.ShakeOnce(1.4f, 4f, 0f, 0.7f);
            }

            AudioClip hitClip = Resources.Load<AudioClip>("SFX/punch_hit");
            if (hitClip != null)
            {
                AudioSource.PlayClipAtPoint(hitClip, transform.position);
            }

            OnDamageTaken?.Invoke(actualDamage);
        }
    }

    [PunRPC]
    public void Heal(int amount)
    {
        health += amount;
        if (health > maxHealth)
            health = maxHealth;
    }

    [PunRPC]
    public void UseStamina(int amount)
    {
        stamina -= amount;
        if (stamina < 0)
            stamina = 0;
    }

    [PunRPC]
    public void RecoverStamina(int amount)
    {
        stamina += amount;
        if (stamina > maxStamina)
            stamina = maxStamina;
    }

    [PunRPC]
    public void GrantMalice(int amount)
    {
        CUBEStatusText.CreateText(
            "You feel something <color=red>malicious</color> take over you.."
        );
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
        CUBEStatusText.CreateText(
            "You feel something <color=red>malicious</color> take over you.."
        );

        malice += amount;
        if (malice < 0)
            malice = 0;
        Debug.Log($"{photonView.Owner.NickName} malice now {malice}");
    }
}
