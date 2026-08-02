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

    void Start()
    {
        standing = GetComponent<StandingSC>();
        rig = GetComponent<Rig>();

        photonLauncher = FindFirstObjectByType<PhotonLauncher>();
    }

    void Update()
    {
        if (stats.health <= 0)
        {
            // Only execute once
            if (!died)
            {
                Debug.Log("Player has died.");

                // mark as dead immediately so Update won't call again
                died = true;

                // disable rigs
                foreach (RigWithMultiplier rig in standing.rigToLift)
                {
                    rig.multiplier = 0;
                }
                rig.control = 0;

                // disable scripts
                foreach (MonoBehaviour mb in scriptsToDisableOnDeath)
                {
                    mb.enabled = false;
                }

                // start respawn coroutine once
                StartCoroutine(Respawn());
            }
        }
    }

    IEnumerator Respawn()
    {
        // wait respawn time
        yield return new WaitForSeconds(5f);

        // restore health
        stats.health = 100;

        // re-enable scripts
        foreach (MonoBehaviour mb in scriptsToDisableOnDeath)
        {
            mb.enabled = true;
        }

        // re-enable rigs
        foreach (RigWithMultiplier rig in standing.rigToLift)
        {
            rig.multiplier = 1;
        }
        rig.control = 1;

        // Only teleport to the intermission spawn if intermission is currently active
        // (i.e. the round has ended). If the round is still running, keep the player
        // where they are — the round-end teleport will handle them when the time comes.
        RoundManager rm = photonLauncher != null ? photonLauncher.roundManager : null;
        bool intermissionActive = rm != null ? rm.intermissionActive : false;

        if (intermissionActive)
        {
            Vector3 spawnPos =
                rm != null
                    ? rm.SyncedIntermissionSpawnPos
                    : photonLauncher.intermissionSpawn.position;
            Quaternion spawnRot =
                rm != null
                    ? rm.SyncedIntermissionSpawnRot
                    : photonLauncher.intermissionSpawn.rotation;

            photonLauncher.TeleportPlayer(spawnPos, spawnRot);

            // Inform PhotonLauncher so the upcoming OnIntermissionStart doesn't re-teleport.
            photonLauncher.MarkPlayerAtIntermission();
        }

        // allow death to happen again
        died = false;
    }

    public void ResetDeathState()
    {
        StopAllCoroutines();
        died = false;

        if (standing != null && standing.rigToLift != null)
        {
            foreach (RigWithMultiplier rig in standing.rigToLift)
            {
                if (rig != null)
                    rig.multiplier = 1;
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
    }
}
