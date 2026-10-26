using System.Collections;
using Photon.Pun;
using UnityEngine;

/// <summary>
/// MonoBehaviour runner for the Music Circle ability.
/// After a startup window (during which the killer can cancel it by damaging the caster),
/// spawns a circle on the network directly on the ground at the caster's feet.
/// Displays an in-world groovy ground preview ring while channeling.
/// </summary>
public class MusicCircleAbility : MonoBehaviourPun
{
    private Coroutine activeCoroutine;
    private bool cancelled;
    private GameObject previewCircle;

    public bool IsCancelled => cancelled;

    private BasePlayer caster;
    private float boostDuration;
    private float boostMultiplier;
    private float radius;
    private float circleLifetime;
    private float startupDuration;

    /// <summary>
    /// Begins (or restarts) the startup → spawn sequence.
    /// </summary>
    public void Execute(BasePlayer player, float boost, float multiplier, float r, float lifetime, float startup)
    {
        if (activeCoroutine != null)
            StopCoroutine(activeCoroutine);

        DestroyPreview();

        caster = player;
        boostDuration = boost;
        boostMultiplier = multiplier;
        radius = r;
        circleLifetime = lifetime;
        startupDuration = startup;
        cancelled = false;

        activeCoroutine = StartCoroutine(StartupThenSpawn());
    }

    /// <summary>
    /// Cancels the pending spawn. Called by PlayerHitboxManager when the caster takes damage.
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

        CUBEStatusText.CreateText("<color=red>Music Circle cancelled!</color>");
        Debug.Log("[MusicCircleAbility] Cancelled during startup.");
    }

    public Vector3 CalculateCirclePosition()
    {
        Transform hip = caster.GetComponent<RagdollNetwork>()?.hip
            ?? caster.transform.Find("RIG/Hip")
            ?? caster.transform.Find("RIG/hip")
            ?? caster.transform;

        Vector3 originPos = hip != null ? hip.position : caster.transform.position;

        if (Physics.Raycast(originPos + Vector3.up * 1f, Vector3.down, out RaycastHit hit, 10f, ~0, QueryTriggerInteraction.Ignore))
        {
            return hit.point + Vector3.up * 0.05f;
        }

        return originPos;
    }

    private void CreateCirclePreview(Vector3 pos, float r)
    {
        DestroyPreview();

        previewCircle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        previewCircle.name = "MusicCircle_Ground_Preview";
        previewCircle.transform.position = pos;
        previewCircle.transform.localScale = new Vector3(r * 2f, 0.02f, r * 2f);

        var col = previewCircle.GetComponent<Collider>();
        if (col != null)
            Destroy(col);

        Renderer rend = previewCircle.GetComponent<Renderer>();
        if (rend != null)
        {
            Material mat = Resources.Load<Material>("MusicCircle");
            if (mat != null)
            {
                rend.material = mat;
            }
            else
            {
                Material tempMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                tempMat.color = new Color(0.2f, 0.9f, 0.4f, 0.45f);
                rend.material = tempMat;
            }
        }
    }

    private void DestroyPreview()
    {
        if (previewCircle != null)
        {
            Destroy(previewCircle);
            previewCircle = null;
        }
    }

    private IEnumerator StartupThenSpawn()
    {
        Vector3 spawnPos = CalculateCirclePosition();
        CreateCirclePreview(spawnPos, radius);

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

            if (previewCircle != null)
            {
                float pulse = 1f + Mathf.Sin(elapsed * 10f) * 0.04f;
                previewCircle.transform.localScale = new Vector3(radius * 2f * pulse, 0.02f, radius * 2f * pulse);
            }

            yield return null;
        }

        DestroyPreview();

        if (cancelled)
        {
            activeCoroutine = null;
            yield break;
        }

        // Spawn real networked music circle
        GameObject circle = PhotonNetwork.Instantiate(
            "MusicCirclePrefab",
            spawnPos,
            Quaternion.identity
        );

        MusicCircleFunction func = circle.GetComponent<MusicCircleFunction>();
        if (func != null)
        {
            func.boostDuration = boostDuration;
            func.boostMultiplier = boostMultiplier;
            func.radius = radius;
            func.lifetime = circleLifetime;
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
