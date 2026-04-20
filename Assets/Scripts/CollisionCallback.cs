using Photon.Pun;
using UnityEngine;
using UnityEngine.Events;

public class CollisionCallback : MonoBehaviourPun
{
    public UnityEvent<Collision> collisionEntered;
    public UnityEvent<Collision> collisionExited;

    void OnCollisionEnter(Collision collision)
    {
        if (!photonView.IsMine) return;
        collisionEntered.Invoke(collision);
    }
    void OnCollisionExit(Collision collision)
    {
        if (!photonView.IsMine) return;
        collisionExited.Invoke(collision);
    }
}