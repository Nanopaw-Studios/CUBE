using UnityEngine;

namespace PhysicsChar.Stability
{
    [DisallowMultipleComponent]
    public class StabilityAnalyzer : MonoBehaviour
    {
        [Header("References")]
        public Rig rig;
        public SupportDetector supportDetector;

        [Header("Trapped Feet / Limb Stretch Thresholds")]
        [Tooltip("Normal horizontal offset distance between torso/hips and feet.")]
        public float normalFootOffset = 0.35f;

        [Tooltip("Horizontal offset distance where character is considered severely trapped/folded.")]
        public float maxTrappedFootOffset = 0.85f;

        [Header("Movement Mismatch Thresholds")]
        [Tooltip("Minimum movement input magnitude required to evaluate direction mismatch.")]
        public float minInputThreshold = 0.15f;

        [Tooltip("Penalty multiplier when character is pushing into an obstacle but blocked.")]
        public float mismatchPenaltyFactor = 0.7f;

        [Header("Output Metrics (0 to 1)")]
        public float trappedFootConfidence = 1.0f;
        public float motionMismatchConfidence = 1.0f;
        public float impactConfidence = 1.0f;

        public float currentFootOffset = 0.0f;
        public float currentVelocityDot = 1.0f;
        public Vector3 currentHipVelocity;

        private Vector3 prevHipVelocity;

        private void Start()
        {
            if (rig == null) rig = GetComponent<Rig>();
            if (supportDetector == null) supportDetector = GetComponent<SupportDetector>();
        }

        public void EvaluateStability()
        {
            if (rig == null) return;

            EvaluateTrappedFeet();
            EvaluateMovementMismatch();
            EvaluateImpacts();
        }

        private void EvaluateTrappedFeet()
        {
            Vector3 feetPos = supportDetector != null
                ? supportDetector.averageFeetPosition
                : (rig.hip != null ? rig.hip.position : transform.position);

            Vector3 hipPos = rig.hip != null ? rig.hip.position : transform.position;

            Vector3 offsetXZ = Vector3.ProjectOnPlane(hipPos - feetPos, Vector3.up);
            currentFootOffset = offsetXZ.magnitude;

            if (currentFootOffset <= normalFootOffset)
            {
                trappedFootConfidence = 1.0f;
            }
            else if (currentFootOffset >= maxTrappedFootOffset)
            {
                trappedFootConfidence = 0.0f;
            }
            else
            {
                float t = (currentFootOffset - normalFootOffset) / (maxTrappedFootOffset - normalFootOffset);
                trappedFootConfidence = Mathf.Clamp01(1.0f - t);
            }
        }

        private void EvaluateMovementMismatch()
        {
            motionMismatchConfidence = 1.0f;
            currentVelocityDot = 1.0f;

            if (rig.hip != null && rig.hip.TryGetComponent<Rigidbody>(out Rigidbody hipRb))
            {
                currentHipVelocity = hipRb.linearVelocity;
            }
            else
            {
                currentHipVelocity = Vector3.zero;
            }

            if (rig.characterinput != null && rig.characterinput.isMoving)
            {
                Vector3 inputDir = rig.characterinput.movementInput;
                inputDir = Vector3.ProjectOnPlane(inputDir, Vector3.up);

                if (inputDir.sqrMagnitude > minInputThreshold * minInputThreshold)
                {
                    inputDir.Normalize();

                    Vector3 actualVelXZ = Vector3.ProjectOnPlane(currentHipVelocity, Vector3.up);
                    float velMag = actualVelXZ.magnitude;

                    if (velMag > 0.1f)
                    {
                        Vector3 actualVelDir = actualVelXZ / velMag;
                        currentVelocityDot = Vector3.Dot(inputDir, actualVelDir);

                        if (currentVelocityDot < 0f)
                        {
                            // Moving opposite to input direction (pushed backwards)
                            motionMismatchConfidence = Mathf.Clamp01(1.0f + currentVelocityDot * mismatchPenaltyFactor);
                        }
                        else
                        {
                            motionMismatchConfidence = 1.0f;
                        }
                    }
                    else
                    {
                        // Player is pushing input hard but actual velocity is almost zero (blocked against obstacle)
                        motionMismatchConfidence = 0.5f;
                    }
                }
            }
        }

        private void EvaluateImpacts()
        {
            Vector3 accel = (currentHipVelocity - prevHipVelocity) / Time.fixedDeltaTime;
            prevHipVelocity = currentHipVelocity;

            float accelMag = accel.magnitude;
            float maxAccelThreshold = 40.0f;

            if (accelMag < 15.0f)
            {
                impactConfidence = 1.0f;
            }
            else
            {
                float t = (accelMag - 15.0f) / (maxAccelThreshold - 15.0f);
                impactConfidence = Mathf.Clamp01(1.0f - t * 0.5f);
            }
        }
    }
}
