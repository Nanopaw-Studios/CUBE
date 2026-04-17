using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

/// <summary>
/// MonoBehaviour runner for Titan's Shockwave ability.
/// On activation, performs a slam and emits a radial shockwave that deals damage
/// to any survivor it touches. Only the owning client drives the damage logic.
/// </summary>
public class ShockwaveRunner : MonoBehaviourPun
{
    private bool isActive = false;

    /// <summary>
    /// Triggers the shockwave slam from the Titan's current position.
    /// </summary>
    public void Execute(BasePlayer player, int damage, float radius, float lifetime)
    {
        if (isActive) return;
        StartCoroutine(SlamSequence(player, damage, radius, lifetime));
    }

    private IEnumerator SlamSequence(BasePlayer player, int damage, float radius, float lifetime)
    {
        isActive = true;

        // Slam origin — feet level
        Vector3 origin = player.transform.position;

        // Spawn the shockwave prefab on the network for visual + collider feedback
        GameObject shockwaveObj = PhotonNetwork.Instantiate("ShockwavePrefab", origin, Quaternion.identity);

        // Damage phase: scan once per short interval for the full lifetime
        float elapsed = 0f;
        float tickRate = 0.1f;
        var alreadyHit = new HashSet<int>();

        while (elapsed < lifetime)
        {
            yield return new WaitForSeconds(tickRate);
            elapsed += tickRate;

            Collider[] hits = Physics.OverlapSphere(origin, radius);
            foreach (Collider col in hits)
            {
                BasePlayer target = col.GetComponentInParent<BasePlayer>();
                if (target == null) continue;
                if (target.GetPlayerType() != BasePlayer.PlayerType.Survivor) continue;

                PhotonView targetPV = target.gameObject.GetPhotonView();
                if (targetPV == null) continue;

                // Each survivor is only hit once per shockwave cast
                if (alreadyHit.Contains(targetPV.ViewID)) continue;
                alreadyHit.Add(targetPV.ViewID);

                targetPV.RPC(nameof(PlayerHitboxManager.RPC_OnHit), RpcTarget.All, damage);
                Debug.Log($"[Shockwave] Hit {targetPV.Owner.NickName} for {damage} damage.");
            }
        }

        isActive = false;
    }
}
