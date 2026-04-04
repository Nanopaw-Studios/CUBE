using Photon.Pun;
using System.Collections;
using UnityEngine;

/// <summary>
/// Attaches to any PhotonNetwork-instantiated object and destroys it via
/// PhotonNetwork.Destroy after the specified lifetime so it is removed on
/// all clients — not just the instantiating client.
///
/// Only the owner (IsMine) calls PhotonNetwork.Destroy; Photon replicates
/// the destruction to every client automatically.
/// </summary>
[RequireComponent(typeof(PhotonView))]
public class PhotonAutoDestroy : MonoBehaviourPun
{
    [Tooltip("Seconds until the object is destroyed across the network.")]
    public float lifetime = 5f;

    private void Start()
    {
        if (photonView.IsMine)
        {
            StartCoroutine(DestroyAfterDelay());
        }
    }

    private IEnumerator DestroyAfterDelay()
    {
        yield return new WaitForSeconds(lifetime);

        if (gameObject != null && photonView.IsMine)
        {
            PhotonNetwork.Destroy(gameObject);
        }
    }
}
