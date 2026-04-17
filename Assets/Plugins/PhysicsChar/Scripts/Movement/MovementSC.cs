using UnityEngine;

public class MovementSC : MonoBehaviour
{
    Rig data;
    public float Force;
    public float BackwardsSpeedMultiplier = 0.9f;

    private void Awake()
    {
        data = GetComponent<Rig>();
    }

    public void Move(Vector3 move)
    {
        float speedMultiplier = 1f;

        Vector3 camForward = Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up).normalized;
        
        if (Vector3.Dot(move.normalized, camForward) < 0)
        {
            speedMultiplier = BackwardsSpeedMultiplier;
        }

        foreach (Rigidbody rb in data.allRigs)
        {
            rb.AddForce(move * Force * data.control * speedMultiplier, ForceMode.Acceleration);
        }
    }

    public void ResetSpeed(float original)
    {
        Force = original;
    }

    public void ResetAfter(float time, float original)
    {
        Invoke("ResetSpeed", time);
    }
}