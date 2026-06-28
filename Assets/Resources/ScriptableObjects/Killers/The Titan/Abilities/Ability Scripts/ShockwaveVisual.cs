using System.Collections;
using Photon.Pun;
using UnityEngine;

public class ShockwaveVisual : MonoBehaviourPun
{
    [Header("Animation")]
    public float lifetime = 0.6f;
    public float maxScale = 6f;
    public float fadeDuration = 0.2f;

    private Material material;

    private static readonly int DitherInID = Shader.PropertyToID("_Dither_In");

    private void Awake()
    {
        // Start tiny
        transform.localScale = Vector3.zero;

        MeshRenderer renderer = GetComponentInChildren<MeshRenderer>();
        if (renderer != null)
        {
            // Use an instance so we don't modify the shared material
            material = renderer.material;
        }
    }

    private void Start()
    {
        StartCoroutine(Animate());

        if (photonView.IsMine)
            Destroy(gameObject, lifetime);
    }

    private IEnumerator Animate()
    {
        float elapsed = 0f;

        while (elapsed < lifetime)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / lifetime);

            // Expand
            float scale = Mathf.Lerp(0f, maxScale, t);
            transform.localScale = Vector3.one * scale;

            // Dither fade during the final part
            if (material != null && material.HasProperty(DitherInID))
            {
                if (elapsed >= lifetime - fadeDuration)
                {
                    float fadeT = Mathf.Clamp01(
                        (elapsed - (lifetime - fadeDuration)) / fadeDuration
                    );

                    // 0.6 -> 2.0
                    material.SetFloat(DitherInID, Mathf.Lerp(0.6f, 2f, fadeT));
                }
                else
                {
                    material.SetFloat(DitherInID, 0.6f);
                }
            }

            yield return null;
        }
    }
}
