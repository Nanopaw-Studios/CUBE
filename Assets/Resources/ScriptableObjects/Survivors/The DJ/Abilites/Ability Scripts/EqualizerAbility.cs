using System.Collections;
using Photon.Pun;
using UnityEngine;

/// <summary>
/// MonoBehaviour runner for the Equalizer ability.
/// Waits during a short startup (killer can cancel by hitting the caster), then spawns
/// an Equalizer wall on the network directly in front of the caster.
/// Provides an in-world holographic charging preview during startup so there is zero latency confusion.
/// </summary>
public class EqualizerAbility : MonoBehaviourPun
{
    private Coroutine activeCoroutine;
    private bool cancelled;
    private GameObject previewWall;

    public bool IsCancelled => cancelled;

    private BasePlayer caster;
    private float killerSlowMultiplier;
    private float wallLifetime;
    private float startupDuration;

    /// <summary>
    /// Begins (or restarts) the startup → spawn sequence.
    /// </summary>
    public void Execute(BasePlayer player, float slowMultiplier, float lifetime, float startup)
    {
        if (activeCoroutine != null)
            StopCoroutine(activeCoroutine);

        DestroyPreview();

        caster = player;
        killerSlowMultiplier = slowMultiplier;
        wallLifetime = lifetime;
        startupDuration = startup;
        cancelled = false;

        activeCoroutine = StartCoroutine(StartupThenSpawn());
    }

    /// <summary>
    /// Cancels the pending spawn. Called by PlayerHitboxManager when the caster is hit.
    /// </summary>
    public void CancelStartup()
    {
        cancelled = true;
        DestroyPreview();

        if (activeCoroutine != null)
        {
            StopCoroutine(activeCoroutine);
            activeCoroutine = null;
        }

        CUBEStatusText.CreateText("<color=red>Equalizer cancelled!</color>");
        Debug.Log("[EqualizerAbility] Cancelled during startup.");
    }

    public Vector3 CalculateWallPosition(out Quaternion wallRotation)
    {
        Transform hip = caster.GetComponent<RagdollNetwork>()?.hip
            ?? caster.transform.Find("RIG/Hip")
            ?? caster.transform.Find("RIG/hip")
            ?? caster.transform;

        Vector3 originPos = hip != null ? hip.position : caster.transform.position;

        Camera cam = Camera.main;
        Vector3 forward = cam != null
            ? Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized
            : caster.transform.forward;

        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.forward;

        // Position wall 2.5 meters in front of the player
        Vector3 wallPos = originPos + forward * 2.5f;

        // Snap cleanly to the floor
        if (Physics.Raycast(wallPos + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 10f, ~0, QueryTriggerInteraction.Ignore))
        {
            // EqualizerWallPrefab is 5.6m high, centered at origin
            wallPos.y = hit.point.y + 2.5f;
        }
        else
        {
            wallPos.y = originPos.y + 1.5f;
        }

        wallRotation = Quaternion.LookRotation(forward, Vector3.up);
        return wallPos;
    }

    private void CreateHologramPreview(Vector3 pos, Quaternion rot)
    {
        DestroyPreview();

        previewWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        previewWall.name = "Equalizer_Hologram_Preview";
        previewWall.transform.position = pos;
        previewWall.transform.rotation = rot;
        previewWall.transform.localScale = new Vector3(12f, 5.6196f, 0.1f);

        // Remove collider so it has no physics
        var col = previewWall.GetComponent<Collider>();
        if (col != null)
            Destroy(col);

        Renderer rend = previewWall.GetComponent<Renderer>();
        if (rend != null)
        {
            Material mat = Resources.Load<Material>("Shader Graphs_Equalizer") ?? Resources.Load<Material>("Equalizer");
            if (mat != null)
            {
                rend.material = mat;
            }
            else
            {
                Material tempMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                tempMat.color = new Color(0f, 0.85f, 1f, 0.45f);
                rend.material = tempMat;
            }
        }
    }

    private void DestroyPreview()
    {
        if (previewWall != null)
        {
            Destroy(previewWall);
            previewWall = null;
        }
    }

    private IEnumerator StartupThenSpawn()
    {
        Vector3 spawnPos = CalculateWallPosition(out Quaternion spawnRot);
        CreateHologramPreview(spawnPos, spawnRot);

        float elapsed = 0f;
        while (elapsed < startupDuration)
        {
            if (cancelled)
            {
                DestroyPreview();
                activeCoroutine = null;
                yield break;
            }

            elapsed += Time.deltaTime;

            // Subtle pulsing effect on hologram
            if (previewWall != null)
            {
                float pulse = 1f + Mathf.Sin(elapsed * 12f) * 0.05f;
                previewWall.transform.localScale = new Vector3(12f * pulse, 5.6196f, 0.1f);
            }

            yield return null;
        }

        DestroyPreview();

        if (cancelled)
        {
            activeCoroutine = null;
            yield break;
        }

        // Spawn real networked wall
        GameObject wall = PhotonNetwork.Instantiate(
            "EqualizerWallPrefab",
            spawnPos,
            spawnRot
        );

        EqualizerWallFunction func = wall.GetComponent<EqualizerWallFunction>();
        if (func != null)
        {
            func.killerSlowMultiplier = killerSlowMultiplier;
            func.lifetime = wallLifetime;
        }

        activeCoroutine = null;
    }

    private void OnDisable()
    {
        DestroyPreview();
    }

    private void OnDestroy()
    {
        DestroyPreview();
    }
}
