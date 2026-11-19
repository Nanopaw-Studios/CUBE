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
        if (isActive)
            return;
        StartCoroutine(SlamSequence(player, damage, radius, lifetime));
    }

    private IEnumerator SlamSequence(BasePlayer player, int damage, float radius, float lifetime)
    {
        isActive = true;

        Vector3 origin = player.transform.Find("RIG").GetComponent<Rig>().hip.position;

        RaycastHit hit;
        if (Physics.Raycast(origin + Vector3.up, Vector3.down, out hit, 50f, 0)) // "default" layer
        {
            origin = hit.point;
        }

        Quaternion rotation = player.transform.rotation * Quaternion.Euler(90f, 0f, 0f);
        Vector3 finalPos = new Vector3(origin.x, 0, origin.z);

        // Spawn the shockwave prefab on the network for visual + collider feedback

        GameObject shockwaveObj = PhotonNetwork.Instantiate(
            "ShockwavePrefab",
            finalPos,
            Quaternion.Euler(90f, 0f, 0f)
        );

        ShockwaveVisual visual = shockwaveObj.GetComponent<ShockwaveVisual>();
        if (visual != null)
        {
            visual.maxScale = radius * 2f; // Diameter
            visual.lifetime = lifetime;
        }

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
                if (target == null)
                    continue;
                if (target.GetPlayerType() != BasePlayer.PlayerType.Survivor)
                    continue;

                PhotonView targetPV = target.gameObject.GetPhotonView();
                if (targetPV == null)
                    continue;

                // Each survivor is only hit once per shockwave cast
                if (alreadyHit.Contains(targetPV.ViewID))
                    continue;
                alreadyHit.Add(targetPV.ViewID);

                BasePlayerStats targetStats = target.GetComponent<BasePlayerStats>();
                bool isKillingBlow =
                    targetStats != null && targetStats.health - damage <= 0 && targetStats.health > 0;

                targetPV.RPC(nameof(PlayerHitboxManager.RPC_OnHit), RpcTarget.All, damage);

                // Apply Tremor status effect (Tier 2, 4 seconds)
                StatusEffectManager targetFx = target.GetComponent<StatusEffectManager>();
                if (targetFx != null)
                {
                    targetPV.RPC(nameof(StatusEffectManager.RPC_ApplyStatusEffect), RpcTarget.All, (byte)StatusEffectType.Tremor, (byte)2, 4f);
                }

                CubeCoinRewardManager.NotifyLocalSuccessfulAbility("shockwave_hit", 35);
                if (isKillingBlow)
                {
                    GetComponent<CubeCoinRewardManager>()?.OnKilledSurvivor();
                }

                Debug.Log($"[Shockwave] Hit {targetPV.Owner.NickName} for {damage} damage and applied Tremor.");

            }
        }

        isActive = false;
    }
}
