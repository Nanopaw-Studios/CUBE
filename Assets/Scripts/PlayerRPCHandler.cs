using Photon.Pun;
using UnityEngine;

// Attach this to the ROOT GameObject with PhotonView
public class PlayerRPCHandler : MonoBehaviourPunCallbacks
{
    // Reference to ArmController (can be on a child)
    public ArmController armController;

    private void Awake()
    {
        if (photonView == null)
        {
            Debug.LogError("PhotonView missing on root GameObject!");
            enabled = false;
            return;
        }

        if (armController == null)
        {
            Debug.LogError("ArmController not found in children!");
            enabled = false;
            return;
        }
    }

    [PunRPC]
    public void RPC_ArmHandle(int hand, bool cancelled)
    {
        Debug.Log($"RPC_ArmHandle called on {gameObject.name}: hand={hand}, cancelled={cancelled}");
        armController.ArmHandle((Hands)hand, cancelled);
    }

    [PunRPC]
    public void RPC_ApplyClientEffect(byte effectId, bool on, float intensity)
    {
        var receiver = GetComponent<StatusEffectReceiver>();
        if (receiver == null)
        {
            Debug.LogWarning("No StatusEffectReceiver on this player prefab.");
            return;
        }

        receiver.ApplyEffect((ClientEffectId)effectId, on, intensity);
    }

    /// <summary>
    /// Static event invoked when any player activates an ability.
    /// Provides hip position and actor number so proximity passives (like Hype) can trigger.
    /// </summary>
    public static event System.Action<Vector3, int> OnNetworkAbilityUsed;

    [PunRPC]
    public void RPC_NotifyAbilityUsed(Vector3 position, int actorNumber)
    {
        OnNetworkAbilityUsed?.Invoke(position, actorNumber);
    }
}
