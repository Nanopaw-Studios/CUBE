using UnityEngine;

public class MovementSC : MonoBehaviour
{
    Rig data;
    public float Force;
    public float SpeedModifier = 1f;
    public float BackwardsSpeedMultiplier = 0.9f;

    private void Awake()
    {
        data = GetComponent<Rig>();
    }

    public void Move(Vector3 move)
    {
        if (SpeedModifier <= 0f)
            return; // Completely frozen/immobilized

        float speedMultiplier = 1f;

        if (Camera.main != null)
        {
            Vector3 camForward = Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up).normalized;
            if (Vector3.Dot(move.normalized, camForward) < 0)
            {
                speedMultiplier = BackwardsSpeedMultiplier;
            }
        }

        float moveMult = (data != null && data.controlBlender != null) ? data.controlBlender.MovementForceMultiplier : 1.0f;

        foreach (Rigidbody rb in data.allRigs)
        {
            if (rb != null)
            {
                rb.AddForce(move * Force * SpeedModifier * data.control * moveMult * speedMultiplier, ForceMode.Acceleration);
            }
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