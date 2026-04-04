using Photon.Pun;
using UnityEngine;

/// <summary>
/// Lives on the player root GameObject (same one that holds the PhotonView) and relays
/// pose/animation RPCs to the child PoseHandler component.
///
/// Background: Photon only searches for [PunRPC] methods on components that are on the
/// SAME GameObject as the PhotonView. PoseHandler lives on a child object, so its own
/// RPC methods are never found, producing the "RPC method not found" warning.
///
/// Solution: PoseHandler sends RPCs using the root PhotonView (obtained via this relay),
/// and those RPCs land here — which then calls the actual logic on PoseHandler.
/// </summary>
[RequireComponent(typeof(PhotonView))]
public class PoseHandlerNetworkRelay : MonoBehaviourPun
{
    private PoseHandler poseHandler;

    private void Awake()
    {
        poseHandler = GetComponentInChildren<PoseHandler>(true);
        if (poseHandler == null)
        {
            Debug.LogWarning("[PoseHandlerNetworkRelay] No PoseHandler found in children.");
        }
    }

    /// <summary>
    /// Relays a pose index change to the local PoseHandler.
    /// Called via RPC from the owning client's PoseHandler.
    /// </summary>
    [PunRPC]
    public void Relay_SetPose(int index)
    {
        if (poseHandler == null) return;
        poseHandler.ApplyPoseLocally(index);
    }

    /// <summary>
    /// Relays an animation sequence to the local PoseHandler.
    /// Called via RPC from the owning client's PoseHandler.
    /// </summary>
    [PunRPC]
    public void Relay_PlayAnimation(int[] poseIndexes, float timeBetween)
    {
        if (poseHandler == null) return;
        poseHandler.PlayAnimationLocally(poseIndexes, timeBetween);
    }
}
