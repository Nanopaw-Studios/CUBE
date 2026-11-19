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

        if (photonView == null || photonView.IsMine)
        {
            StartCoroutine(EvaluateDashGapClose());
        }
    }

    private System.Collections.IEnumerator EvaluateDashGapClose()
    {
        BasePlayer nearestSurvivor = FindNearestAliveSurvivor(out float preDist);
        if (nearestSurvivor == null || preDist > 28f)
            yield break;

        yield return new WaitForSeconds(0.55f);

        if (nearestSurvivor == null)
            yield break;

        Vector3 myPos = GetBodyPosition(transform);
        Vector3 targetPos = GetBodyPosition(nearestSurvivor.transform);
        float postDist = Vector3.Distance(myPos, targetPos);

        if ((preDist - postDist) >= 2.2f || postDist <= 3.8f)
        {
            CubeCoinRewardManager.NotifyLocalSuccessfulAbility("dash_gap_close", 30);
        }
    }

    private BasePlayer FindNearestAliveSurvivor(out float minDistance)
    {
        minDistance = float.MaxValue;
        BasePlayer nearest = null;
        Vector3 myPos = GetBodyPosition(transform);

        BasePlayer[] all = FindObjectsByType<BasePlayer>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            BasePlayer p = all[i];
            if (p == null || p.gameObject == gameObject)
                continue;
            if (p.GetPlayerType() != BasePlayer.PlayerType.Survivor)
                continue;

            BasePlayerStats s = p.GetComponent<BasePlayerStats>();
            if (s != null && s.health <= 0)
                continue;

            float d = Vector3.Distance(myPos, GetBodyPosition(p.transform));
            if (d < minDistance)
            {
                minDistance = d;
                nearest = p;
            }
        }
        return nearest;
    }

    private static Vector3 GetBodyPosition(Transform root)
    {
        RagdollNetwork rag = root.GetComponent<RagdollNetwork>();
        if (rag != null && rag.hip != null)
            return rag.hip.position;
        return root.position;
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
