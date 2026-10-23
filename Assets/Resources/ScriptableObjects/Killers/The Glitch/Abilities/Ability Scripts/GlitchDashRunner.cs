using Photon.Pun;
using UnityEngine;

/// <summary>
/// Network runner for Glitch's Phase Dash ability.
/// Broadcasts the dash sound and visual particles to all players in the room.
/// </summary>
public class GlitchDashRunner : MonoBehaviourPun
{
    private static AudioClip cachedDashSound;

    /// <summary>
    /// Broadcasts the Phase Dash sound and particle effects to all other clients.
    /// </summary>
    public void BroadcastDashEffects(Vector3 position)
    {
        if (photonView != null && PhotonNetwork.IsConnected)
        {
            photonView.RPC(nameof(RPC_OnGlitchDashEffects), RpcTarget.Others, position);
        }
    }

    [PunRPC]
    public void RPC_OnGlitchDashEffects(Vector3 position)
    {
        if (cachedDashSound == null)
        {
            cachedDashSound = Resources.Load<AudioClip>("SFX/punch_swing") ?? Resources.Load<AudioClip>("SFX/woosh");
        }

        if (cachedDashSound != null)
        {
            AudioSource.PlayClipAtPoint(cachedDashSound, position, 1f);
        }

        GameObject prefab = Resources.Load<GameObject>("DashParticles");
        if (prefab != null)
        {
            GameObject fx = Instantiate(prefab, position, Quaternion.identity);
            Destroy(fx, 2f);
        }
    }
}
