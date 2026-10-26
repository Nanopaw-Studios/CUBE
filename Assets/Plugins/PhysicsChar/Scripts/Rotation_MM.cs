using UnityEngine;

public class Rotation_MM : MonoBehaviour
{
    Rig data;
    public float torque;
    [Tooltip("Angular velocity damping to prevent rotational overshoot and jitter.")]
    public float damping = 25f;
    public RigWithMultiplier[] rigs;
    public Vector3 TargetRotation;

    private void Start()
    {
        data = GetComponent<Rig>();
    }

    private void FixedUpdate()
    {
        if (data == null || rigs == null) return;
        Quaternion targetQuaternion = Quaternion.Euler(TargetRotation);
        float torqueMult = (data.controlBlender != null) ? data.controlBlender.RotationTorqueMultiplier : 1.0f;

        for (int i = 0; i < rigs.Length; i++)
        {
            if (rigs[i].part == null) continue;

            Quaternion currentRotation = rigs[i].part.rotation;
            Quaternion neededRotation = targetQuaternion * Quaternion.Inverse(currentRotation);

            Vector3 torqueDirection;
            float torqueMagnitude;
            neededRotation.ToAngleAxis(out torqueMagnitude, out torqueDirection);

            if (torqueMagnitude > 180f)
                torqueMagnitude -= 360f;

            Vector3 torqueToApply = torqueDirection * (torqueMagnitude * Mathf.Deg2Rad);

            // PD controller: Proportional torque toward target + Derivative damping against angular velocity
            Vector3 angVel = rigs[i].part.angularVelocity;
            Vector3 dampingTorque = -angVel * damping;
            Vector3 totalTorque = (torqueToApply * torque * rigs[i].multiplier + dampingTorque) * (data.control * torqueMult);

            rigs[i].part.AddTorque(totalTorque, ForceMode.Acceleration);
        }
    }
}