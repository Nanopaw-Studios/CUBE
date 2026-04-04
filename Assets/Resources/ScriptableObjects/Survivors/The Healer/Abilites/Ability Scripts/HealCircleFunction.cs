using Photon.Pun;
using UnityEngine;

public class HealCircleFunction : MonoBehaviourPunCallbacks
{
    public BasePlayer creator;
    public float healAmount = 2f;
    public float healInterval = 1f;
    public float radius = 5f;
    public float lifetime = 15f;

    private void Start()
    {
        // Only the owner ticks and destroys
        if (!photonView.IsMine) return;

        InvokeRepeating(nameof(HealSurvivors), healInterval, healInterval);
        Invoke(nameof(NetworkDestroy), lifetime);
    }

    /// <summary>
    /// Scans for survivors in range and heals them via RPC on their own PhotonView.
    /// Only runs on the owner client.
    /// </summary>
    public void HealSurvivors()
    {
        if (!photonView.IsMine) return;

        Collider[] hitColliders = Physics.OverlapSphere(transform.position, radius);
        foreach (var hitCollider in hitColliders)
        {
            BasePlayer player = hitCollider.GetComponentInParent<BasePlayer>();
            if (player == null) continue;
            if (player.GetPlayerType() != BasePlayer.PlayerType.Survivor) continue;

            PhotonView targetPV = player.gameObject.GetPhotonView();
            if (targetPV == null) continue;

            // Call Heal RPC on the target player's BasePlayerStats
            targetPV.RPC(nameof(BasePlayerStats.Heal), RpcTarget.All, (int)healAmount);
        }
    }

    private void NetworkDestroy()
    {
        if (photonView.IsMine)
            PhotonNetwork.Destroy(gameObject);
    }
}