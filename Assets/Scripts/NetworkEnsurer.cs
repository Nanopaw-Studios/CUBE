using Photon.Pun;
using UnityEngine;
using System.Collections;

public class NetworkEnsurer : MonoBehaviourPunCallbacks
{
    [Header("Local (Objects to disable for everyone but localplayer)")]
    public GameObject[] localObjects;
    public MonoBehaviour[] localScripts;

    [Header("Remote (Objects to disable for everyone but remote players)")]
    public GameObject[] remoteObjects;
    public MonoBehaviour[] remoteScripts;
    public Renderer[] remoteRenderers;

    [Header("Keep these alive at all times!")]
    public GameObject[] allObjects;

    private void Start()
    {
        ApplyVisibility();
        StartCoroutine(ApplyVisibilityNextFrame());
    }

    private IEnumerator ApplyVisibilityNextFrame()
    {
        yield return null;
        ApplyVisibility();
    }

    private void ApplyVisibility()
    {
        // Ensure local objects are enabled only for the local player
        foreach (var obj in localObjects)
        {
            if (obj == null) continue;
            obj.SetActive(photonView.IsMine);
        }
        // Ensure remote objects are enabled only for remote players
        foreach (var obj in remoteObjects)
        {
            if (obj == null) continue;
            obj.SetActive(!photonView.IsMine);
        }

        // Enable/disable local scripts
        foreach (var script in localScripts)
        {
            if (script == null) continue;
            script.enabled = photonView.IsMine;
        }
        // Enable/disable remote scripts
        foreach (var script in remoteScripts)
        {
            if (script == null) continue;
            script.enabled = !photonView.IsMine;
        }
        // Enable/disable remote renderers
        foreach (var renderer in remoteRenderers)
        {
            if (renderer == null) continue;
            renderer.enabled = !photonView.IsMine;
        }

        foreach (var obj in allObjects)
        {
            if (obj == null) continue;
            obj.gameObject.SetActive(true);
        }
    }
}
