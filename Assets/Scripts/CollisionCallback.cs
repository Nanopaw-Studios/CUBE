using Photon.Pun;
using UnityEngine;
using UnityEngine.Events;

public class CollisionCallback : MonoBehaviourPun
{
    public UnityEvent<Collision> collisionEntered = new();
    public UnityEvent<Collision> collisionExited = new();

    void OnCollisionEnter(Collision collision)
    {
        if (!photonView.IsMine)
            return;
        collisionEntered?.Invoke(collision);
    }

    void OnCollisionExit(Collision collision)
    {
        if (!photonView.IsMine)
            return;
        collisionExited?.Invoke(collision);
    }
}
