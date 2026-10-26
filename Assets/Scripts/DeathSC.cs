using System.Collections;
using UnityEngine;

public class DeathSC : MonoBehaviour
{
    public BasePlayerStats stats;
    public MonoBehaviour[] scriptsToDisableOnDeath;
    StandingSC standing;
    Rig rig;
    PhotonLauncher photonLauncher;

    bool died = false;

    void Awake()
    {
        if (stats == null)
            stats = GetComponentInParent<BasePlayerStats>() ?? GetComponent<BasePlayerStats>();
        if (standing == null)
            standing = GetComponent<StandingSC>();
        if (rig == null)
            rig = GetComponent<Rig>();
    }

    void Start()
    {
        if (stats == null)
            stats = GetComponentInParent<BasePlayerStats>() ?? GetComponent<BasePlayerStats>();
        if (standing == null)
            standing = GetComponent<StandingSC>();
        if (rig == null)
            rig = GetComponent<Rig>();

        if (photonLauncher == null)
            photonLauncher = FindFirstObjectByType<PhotonLauncher>();
    }

    void Update()
    {
        if (stats == null)
        {
            stats = GetComponentInParent<BasePlayerStats>() ?? GetComponent<BasePlayerStats>();
            if (stats == null)
                return;
        }

        if (stats.health <= 0)
        {
            // Only execute once
            if (!died)
            {
                Debug.Log("Player has died.");

                // mark as dead immediately so Update won't call again
                died = true;

                // disable rigs
                if (standing != null && standing.rigToLift != null)
                {
                    foreach (RigWithMultiplier r in standing.rigToLift)
                    {
                        if (r != null)
                            r.multiplier = 0;
                    }
                }
                if (rig != null)
                {
                    rig.control = 0;
                }

                // disable scripts
                if (scriptsToDisableOnDeath != null)
                {
                    foreach (MonoBehaviour mb in scriptsToDisableOnDeath)
                    {
                        if (mb != null)
                            mb.enabled = false;
                    }
                }

                // start respawn coroutine once
                StartCoroutine(Respawn());
            }
        }
    }

    IEnumerator Respawn()
    {
        // wait respawn delay
        yield return new WaitForSeconds(5f);

        RoundManager rm = photonLauncher != null ? photonLauncher.roundManager : FindFirstObjectByType<RoundManager>();
        bool isMidRound = rm != null && rm.roundActive && !rm.intermissionActive;

        if (isMidRound)
        {
            // During an active round, dead players remain dead and collapsed so they can spectate.
            // They will be revived when intermission starts via ResetDeathState() / ResetToDefaultStats().
            Debug.Log("[DeathSC] Player died mid-round — staying collapsed for spectator mode.");
            yield break;
        }

        // Intermission / Lobby death — safe to revive immediately
        Revive();

        bool intermissionActive = rm != null && rm.intermissionActive;
        if (intermissionActive && photonLauncher != null)
        {
            Vector3 spawnPos =
                rm != null && rm.SyncedIntermissionSpawnPos != Vector3.zero
                    ? rm.SyncedIntermissionSpawnPos
                    : (photonLauncher.intermissionSpawn != null ? photonLauncher.intermissionSpawn.position : Vector3.zero);
            Quaternion spawnRot =
                rm != null && rm.SyncedIntermissionSpawnRot != Quaternion.identity
                    ? rm.SyncedIntermissionSpawnRot
                    : (photonLauncher.intermissionSpawn != null ? photonLauncher.intermissionSpawn.rotation : Quaternion.identity);

            photonLauncher.TeleportPlayer(spawnPos, spawnRot);
            photonLauncher.MarkPlayerAtIntermission();
        }
    }

    private void Revive()
    {
        if (stats != null)
            stats.health = stats.maxHealth;

        if (standing != null && standing.rigToLift != null)
        {
            foreach (RigWithMultiplier r in standing.rigToLift)
            {
                if (r != null)
                    r.multiplier = 1;
            }
        }

        if (rig != null)
        {
            rig.control = 1;
        }

        if (scriptsToDisableOnDeath != null)
        {
            foreach (MonoBehaviour mb in scriptsToDisableOnDeath)
            {
                if (mb != null)
                    mb.enabled = true;
            }
        }

        died = false;
    }

    public void ResetDeathState()
    {
        StopAllCoroutines();
        Revive();
    }
}
