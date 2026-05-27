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
    }

    void OnCollisionEnter(Collision collision)
    {
        if (rootPV == null || !rootPV.IsMine)
            return;

        collisionEntered?.Invoke(collision);
    }

    void OnCollisionExit(Collision collision)
    {
        if (rootPV == null || !rootPV.IsMine)
            return;

        collisionExited?.Invoke(collision);
    }
}
