using Photon.Pun;
using UnityEngine;

public class NetworkEnsurer : MonoBehaviourPunCallbacks
{
    [Header("Local (Objects to disable for everyone but localplayer)")]
    public GameObject[] localObjects;
    public MonoBehaviour[] localScripts;

    [Header("Remote (Objects to disable for everyone but remote players)")]
    public GameObject[] remoteObjects;
    public MonoBehaviour[] remoteScripts;
    public Renderer[] remoteRenderers;
    [Tooltip("If true, local player renderers stay visible even if placed in remoteRenderers by mistake.")]
    public bool keepLocalRenderersVisible = true;

    [Header("Keep these alive at all times!")]
    public GameObject[] allObjects;

    private void Start()
    {

        // Ensure local objects are enabled only for the local player
        foreach (var obj in localObjects)
        {
            obj.SetActive(photonView.IsMine);
        }
        // Ensure remote objects are enabled only for remote players
        foreach (var obj in remoteObjects)
        {
            obj.SetActive(!photonView.IsMine);
        }

        // Enable/disable local scripts
        foreach (var script in localScripts)
        {
            script.enabled = photonView.IsMine;
        }
        // Enable/disable remote scripts
        foreach (var script in remoteScripts)
        {
            script.enabled = !photonView.IsMine;
        }
        // Enable/disable remote renderers
        foreach (var renderer in remoteRenderers)
        {
            if (renderer == null) continue;
            renderer.enabled = keepLocalRenderersVisible && photonView.IsMine
                ? true
                : !photonView.IsMine;
        }

        foreach (var obj in allObjects)
        {
            obj.gameObject.SetActive(true);
        }
    }
}
