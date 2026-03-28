using Photon.Pun;
using UnityEngine;
using UnityEngine.Events;

public class CollisionCallback : MonoBehaviourPun
{
    public UnityEvent<Collision> collisionEntered;
    public UnityEvent<Collision> collisionExited;

    void OnCollisionEnter(Collision collision)
    {
        collisionEntered.Invoke(collision);
    }
    void OnCollisionExit(Collision collision)
    {
        collisionExited.Invoke(collision);
    }
}