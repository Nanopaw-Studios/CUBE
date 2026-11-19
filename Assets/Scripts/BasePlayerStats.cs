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
        deathReported = false;

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
        deathReported = false;

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
        DeathSC death = GetComponentInChildren<DeathSC>() ?? GetComponent<DeathSC>();
        if (death != null)
        {
            death.ResetDeathState();
        }

        Rig rig = GetComponentInChildren<Rig>() ?? GetComponent<Rig>();
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

    // Ensures the death signal is sent exactly once per life.
    private bool deathReported;

    /// <summary>Fired on the local player client when taking damage.</summary>
    public event System.Action<int> OnDamageTaken;

    private CameraShaker localCameraShaker;

    /// <summary>
    /// Owner-only setter for damage resistance. The value is replicated so that
    /// every client applies the same modifier when it simulates a hit; otherwise
    /// the master's health copy drifts above the owner's and round-end checks fail.
    /// </summary>
    public void SetDamageResistance(float resistance)
    {
        float clamped = Mathf.Clamp01(resistance);

        if (Mathf.Abs(damageResistance - clamped) < 0.001f)
            return;

        damageResistance = clamped;

        if (photonView != null && photonView.IsMine)
            photonView.RPC(nameof(RPC_SetDamageResistance), RpcTarget.Others, clamped);
    }

    [PunRPC]
    private void RPC_SetDamageResistance(float resistance)
    {
        damageResistance = Mathf.Clamp01(resistance);
    }

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

        // The owning client is the authority on whether this player died. Report
        // it to the master so the killer's win condition does not depend on the
        // master's replicated health reaching zero a frame later.
        if (health <= 0 && !deathReported)
        {
            deathReported = true;
            if (photonView.IsMine)
                photonView.RPC(nameof(RPC_ReportDeath), RpcTarget.MasterClient);
        }

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
                Vector3 soundPos = Camera.main != null ? Camera.main.transform.position : transform.position;
                AudioSource.PlayClipAtPoint(hitClip, soundPos);
            }

            OnDamageTaken?.Invoke(actualDamage);
        }
    }

    [PunRPC]
    private void RPC_ReportDeath()
    {
        PlayerHitboxManager hitbox = GetComponent<PlayerHitboxManager>();
        if (hitbox != null)
            hitbox.CheckKillerWinCondition();
    }

    [PunRPC]
    public void Heal(int amount)
    {
        // Dead players stay dead until intermission. Healing a zero-health survivor
        // would make the killer's elimination win-check treat them as alive while
        // DeathSC still keeps them collapsed and spectating.
        if (health <= 0)
            return;

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
        if (photonView.IsMine)
        {
            CUBEStatusText.CreateText(
                "You feel something <color=red>malicious</color> take over you.."
            );
        }
        malice += amount;
    }

    [PunRPC]
    public void RemoveMalice(int amount)
    {
        if (photonView.IsMine)
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
        // The RPC runs on every client, but the feedback belongs only to the player
        // whose malice changed. Showing it everywhere made everyone think they were
        // becoming the killer.
        if (photonView.IsMine)
        {
            CUBEStatusText.CreateText(
                "You feel something <color=red>malicious</color> take over you.."
            );
        }

        malice += amount;
        if (malice < 0)
            malice = 0;

        string ownerName = photonView.Owner != null ? photonView.Owner.NickName : "unknown";
        Debug.Log($"{ownerName} malice now {malice}");
    }
}
