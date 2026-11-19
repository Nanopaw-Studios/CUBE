using EZCameraShake;
using Photon.Pun;
using UnityEngine;

[RequireComponent(typeof(BasePlayer))]
[RequireComponent(typeof(BasePlayerStats))]
[RequireComponent(typeof(CubeCoinRewardManager))]
public class PlayerHitboxManager : MonoBehaviourPunCallbacks
{
    [Tooltip(
        "Bone colliders that register hits. If empty, all CollisionCallback components in children are used automatically."
    )]
    public CollisionCallback[] damageColliders;

    public BasePlayer basePlayer;
    public BasePlayerStats stats;

    void Awake()
    {
        if (basePlayer == null)
            basePlayer = GetComponent<BasePlayer>();
        if (stats == null)
            stats = GetComponent<BasePlayerStats>();
    }

    void Start()
    {
        if (basePlayer == null)
            basePlayer = GetComponent<BasePlayer>();
        if (stats == null)
            stats = GetComponent<BasePlayerStats>();

        // Auto-discover all CollisionCallback children when none are assigned in the inspector.
        if (damageColliders == null || damageColliders.Length == 0)
        {
            damageColliders = GetComponentsInChildren<CollisionCallback>(true);
            Debug.Log(
                $"[HitboxManager] Auto-discovered {damageColliders.Length} CollisionCallback(s) on {gameObject.name}"
            );
        }

        // Ensure hitbox collider GameObjects are on the Player layer so physics
        // collision matrix works and OverlapBox queries find them correctly.
        int playerLayer = LayerMask.NameToLayer("Player");
        if (playerLayer != -1)
        {
            foreach (CollisionCallback callback in damageColliders)
            {
                if (callback == null)
                    continue;
                callback.gameObject.layer = playerLayer;
            }
        }

        // Only the local owner's hitboxes should fire outgoing hits.
        if (photonView.IsMine)
        {
            foreach (CollisionCallback callback in damageColliders)
            {
                if (callback != null)
                    callback.collisionEntered.AddListener(CollisionEntered);
            }
        }
    }

    [Header("Collision Hit Settings")]
    [SerializeField]
    private float collisionHitCooldown = 0.4f;
    private readonly System.Collections.Generic.Dictionary<PhotonView, float> lastCollisionHitPerTarget = new();

    void CollisionEntered(Collision collision)
    {
        // Only the killer can deal collision damage.
        if (basePlayer.GetPlayerType() != BasePlayer.PlayerType.Killer)
            return;

        KillerType killerType = basePlayer.GetEquippedKiller();
        if (killerType == null)
            return;

        // Walk up the collider's hierarchy to find the target PlayerHitboxManager.
        // Using GetComponentInParent covers bone-deep colliders attached to any
        // child of the target player's root.
        PlayerHitboxManager targetHitbox =
            collision.collider.GetComponentInParent<PlayerHitboxManager>();
        if (targetHitbox == null)
            return;

        // Never hit yourself.
        PhotonView targetView = targetHitbox.photonView;
        if (targetView == null || targetView.IsMine)
            return;

        // Guard: targets must be survivors.
        if (
            targetHitbox.basePlayer != null
            && targetHitbox.basePlayer.GetPlayerType() != BasePlayer.PlayerType.Survivor
        )
            return;

        // Debounce collision hits per target to prevent multi-hit instakills from physics contacts
        if (lastCollisionHitPerTarget.TryGetValue(targetView, out float lastHitTime) &&
            Time.time - lastHitTime < collisionHitCooldown)
        {
            return;
        }
        lastCollisionHitPerTarget[targetView] = Time.time;

        int damage = killerType.PunchDamage;

        // Cache the target's current health before the RPC reduces it.
        BasePlayerStats targetStats = targetHitbox.GetComponent<BasePlayerStats>();
        bool isKillingBlow =
            targetStats != null && targetStats.health - damage <= 0 && targetStats.health > 0;

        targetView.RPC(nameof(RPC_OnHit), RpcTarget.All, damage);
        Debug.Log($"[HitboxManager] Collision hit: {targetView.Owner.NickName} for {damage}");

        // Award the local killer client when this hit eliminates a survivor.
        if (isKillingBlow)
        {
            CubeCoinRewardManager rewardManager = GetComponent<CubeCoinRewardManager>();
            rewardManager?.OnKilledSurvivor();

            CUBE.Achievements.GameplayEventBus.Publish(
                new CUBE.Achievements.GameplayEvent(CUBE.Achievements.GameplayEventType.PlayerKilledEnemy)
            );
        }

        // Attacker feedback: shake killer's camera & play punch impact sound
        CameraShaker localShaker = GetComponentInChildren<CameraShaker>();
        localShaker?.ShakeOnce(1.0f, 4f, 0.05f, 0.2f);

        AudioClip hitSound = Resources.Load<AudioClip>("SFX/punch_hit");
        if (hitSound != null)
        {
            Vector3 contactPoint = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
            AudioSource.PlayClipAtPoint(hitSound, contactPoint);
        }
    }

    /// <summary>
    /// Called via RPC on all clients when this player is hit.
    /// </summary>
    [PunRPC]
    public void RPC_OnHit(int damage)
    {
        if (stats == null)
            stats = GetComponent<BasePlayerStats>();

        Debug.Log($"[HitboxManager] {gameObject.name} received {damage} damage.");
        if (stats != null)
            stats.TakeDamage(damage);

        // Cancel any pending ability startups on this player when they are hit
        if (photonView.IsMine)
        {
            GetComponent<HealProximityRunner>()?.Cancel();
            GetComponent<MusicCircleAbility>()?.CancelStartup();
            GetComponent<EqualizerAbility>()?.CancelStartup();
            FindFirstObjectByType<PlayerAbilityManager>()?.InterruptCurrentCast("HIT!");
        }

        // Only the master client performs win-condition checks to avoid duplicate EndRound calls.
        if (PhotonNetwork.IsMasterClient && stats != null && stats.health <= 0)
        {
            CheckKillerWinCondition();
        }
    }

    /// <summary>
    /// Checks whether all survivors are dead. If so, the master client ends the round.
    /// </summary>
    public void CheckKillerWinCondition()
    {
        // Collect all survivor players in the scene.
        BasePlayer[] allPlayers = FindObjectsByType<BasePlayer>(FindObjectsSortMode.None);
        int totalSurvivors = 0;
        int aliveSurvivors = 0;

        foreach (BasePlayer player in allPlayers)
        {
            if (player == null || player.GetPlayerType() != BasePlayer.PlayerType.Survivor)
                continue;

            totalSurvivors++;
            BasePlayerStats survivorStats = player.GetComponent<BasePlayerStats>();
            if (survivorStats != null && survivorStats.health > 0)
            {
                aliveSurvivors++;
            }
        }

        // Only end the round if there were survivors in the match and all of them are now dead
        if (totalSurvivors > 0 && aliveSurvivors == 0)
        {
            Debug.Log("[HitboxManager] All survivors eliminated. Killer wins!");
            RoundManager roundManager = FindFirstObjectByType<RoundManager>();
            if (roundManager != null && roundManager.roundActive)
            {
                roundManager.photonView.RPC(nameof(RoundManager.EndRound), RpcTarget.All);
            }
        }
    }

    new void OnDisable()
    {
        if (damageColliders != null)
        {
            foreach (CollisionCallback callback in damageColliders)
            {
                if (callback != null)
                    callback.collisionEntered.RemoveListener(CollisionEntered);
            }
        }
    }
}
