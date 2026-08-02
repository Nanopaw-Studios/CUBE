using UnityEngine;

public class Gravity_MM : MonoBehaviour
{
    private Rig data;

    [Tooltip("Base extra downward gravity acceleration applied when airborne.")]
    public float gravity = 20f;

    [Tooltip("Additional multiplier applied when descending (falling downward) for crisp, responsive physics.")]
    public float fallMultiplier = 1.5f;

    private void Start()
    {
        data = GetComponent<Rig>();
    }

    private void FixedUpdate()
    {
        if (data == null || data.allRigs == null) return;

        if (!data.Grounded)
        {
            for (int i = 0; i < data.allRigs.Count; i++)
            {
                Rigidbody rb = data.allRigs[i];
                if (rb == null) continue;

                // Apply fall multiplier when descending to eliminate floaty low-gravity descent
                float mult = (rb.linearVelocity.y < 0f) ? fallMultiplier : 1f;

                rb.AddForce(Vector3.down * (gravity * mult), ForceMode.Acceleration);
            }
        }
    }
}
