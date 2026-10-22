using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using UnityEngine.Events;

public class CollisionCallback : MonoBehaviour
{
    public UnityEvent<Collision> collisionEntered = new();
    public UnityEvent<Collision> collisionExited = new();

    private PhotonView rootPV;

    void Awake()
    {
        rootPV = GetComponentInParent<PhotonView>();

        // In Unity, child colliders without a Rigidbody do not receive OnCollisionEnter;
        // physics delivers the collision to the parent GameObject that owns the Rigidbody.
        // Attach a forwarder to the parent Rigidbody so collisions are reliably routed here.
        if (GetComponent<Rigidbody>() == null)
        {
            Rigidbody parentRb = GetComponentInParent<Rigidbody>();
            if (parentRb != null && parentRb.gameObject != gameObject)
            {
                var forwarder = parentRb.GetComponent<CollisionCallbackForwarder>();
                if (forwarder == null)
                {
                    forwarder = parentRb.gameObject.AddComponent<CollisionCallbackForwarder>();
                }
                forwarder.RegisterCallback(this);
            }
        }
    }

    public void OnCollisionEnter(Collision collision)
    {
        if (rootPV == null)
            rootPV = GetComponentInParent<PhotonView>();

        if (rootPV == null || !rootPV.IsMine)
            return;

        collisionEntered?.Invoke(collision);
    }

    public void OnCollisionExit(Collision collision)
    {
        if (rootPV == null)
            rootPV = GetComponentInParent<PhotonView>();

        if (rootPV == null || !rootPV.IsMine)
            return;

        collisionExited?.Invoke(collision);
    }
}

/// <summary>
/// Automatically attached to parent rigidbodies to forward Unity physics collision
/// events down to bone CollisionCallback components on child colliders.
/// </summary>
public class CollisionCallbackForwarder : MonoBehaviour
{
    private readonly List<CollisionCallback> callbacks = new();

    public void RegisterCallback(CollisionCallback callback)
    {
        if (callback != null && !callbacks.Contains(callback))
        {
            callbacks.Add(callback);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        for (int i = 0; i < callbacks.Count; i++)
        {
            if (callbacks[i] != null)
            {
                callbacks[i].OnCollisionEnter(collision);
            }
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        for (int i = 0; i < callbacks.Count; i++)
        {
            if (callbacks[i] != null)
            {
                callbacks[i].OnCollisionExit(collision);
            }
        }
    }
}
