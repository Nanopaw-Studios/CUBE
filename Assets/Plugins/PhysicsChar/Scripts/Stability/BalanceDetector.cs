using UnityEngine;

namespace PhysicsChar.Stability
{
    [DisallowMultipleComponent]
    public class BalanceDetector : MonoBehaviour
    {
        [Header("References")]
        public Rig rig;
        public SupportDetector supportDetector;

        [Header("Torso Lean Thresholds")]
        [Tooltip("Torso lean angle (in degrees) at which lean confidence starts to drop.")]
        public float minLeanAngle = 15f;

        [Tooltip("Torso lean angle (in degrees) at which lean confidence becomes 0.")]
        public float maxLeanAngle = 70f;

        [Header("Angular Velocity Thresholds")]
        [Tooltip("Torso angular velocity (in rad/s) at which angular stability starts to drop.")]
        public float minAngularVelocity = 2.5f;

        [Tooltip("Torso angular velocity (in rad/s) at which angular confidence reaches 0.")]
        public float maxAngularVelocity = 12f;

        [Header("Center of Mass (CoM) Thresholds")]
        [Tooltip("CoM offset distance from support center where CoM confidence starts to drop.")]
        public float minCoMOffset = 0.15f;

        [Tooltip("CoM offset distance from support center where CoM confidence reaches 0.")]
        public float maxCoMOffset = 0.75f;

        [Header("Output Metrics (0 to 1)")]
        public float leanConfidence = 1.0f;
        public float angVelConfidence = 1.0f;
        public float comConfidence = 1.0f;
        public float torsoAngle = 0.0f;
        public float currentAngularVelocity = 0.0f;
        public float currentCoMOffset = 0.0f;
        public Vector3 worldCoM;

        private void Start()
        {
            if (rig == null) rig = GetComponent<Rig>();
            if (supportDetector == null) supportDetector = GetComponent<SupportDetector>();
        }

        public void EvaluateBalance()
        {
            if (rig == null) return;

            EvaluateTorsoLean();
            EvaluateAngularVelocity();
            EvaluateCenterOfMass();
        }

        private void EvaluateTorsoLean()
        {
            Transform torsoTransform = rig.torso != null ? rig.torso : transform;
            torsoAngle = Vector3.Angle(torsoTransform.up, Vector3.up);

            if (torsoAngle <= minLeanAngle)
            {
                leanConfidence = 1.0f;
            }
            else if (torsoAngle >= maxLeanAngle)
            {
                leanConfidence = 0.0f;
            }
            else
            {
                float t = (torsoAngle - minLeanAngle) / (maxLeanAngle - minLeanAngle);
                leanConfidence = Mathf.Clamp01(1.0f - t);
            }
        }

        private void EvaluateAngularVelocity()
        {
            currentAngularVelocity = 0f;

            if (rig.torso != null && rig.torso.TryGetComponent<Rigidbody>(out Rigidbody torsoRb))
            {
                currentAngularVelocity = torsoRb.angularVelocity.magnitude;
            }

            if (rig.hip != null && rig.hip.TryGetComponent<Rigidbody>(out Rigidbody hipRb))
            {
                currentAngularVelocity = Mathf.Max(currentAngularVelocity, hipRb.angularVelocity.magnitude);
            }

            if (currentAngularVelocity <= minAngularVelocity)
            {
                angVelConfidence = 1.0f;
            }
            else if (currentAngularVelocity >= maxAngularVelocity)
            {
                angVelConfidence = 0.0f;
            }
            else
            {
                float t = (currentAngularVelocity - minAngularVelocity) / (maxAngularVelocity - minAngularVelocity);
                angVelConfidence = Mathf.Clamp01(1.0f - t);
            }
        }

        private void EvaluateCenterOfMass()
        {
            if (rig.allRigs == null || rig.allRigs.Count == 0)
            {
                comConfidence = 1.0f;
                return;
            }

            Vector3 totalMassPos = Vector3.zero;
            float totalMass = 0f;

            foreach (Rigidbody rb in rig.allRigs)
            {
                if (rb != null)
                {
                    totalMassPos += rb.worldCenterOfMass * rb.mass;
                    totalMass += rb.mass;
                }
            }

            if (totalMass > 0.0001f)
            {
                worldCoM = totalMassPos / totalMass;
            }
            else
            {
                worldCoM = rig.hip != null ? rig.hip.position : transform.position;
            }

            Vector3 comXZ = Vector3.ProjectOnPlane(worldCoM, Vector3.up);

            Vector3 supportCenter = supportDetector != null
                ? supportDetector.supportCenter
                : (rig.hip != null ? rig.hip.position : transform.position);
            Vector3 supportXZ = Vector3.ProjectOnPlane(supportCenter, Vector3.up);

            currentCoMOffset = Vector3.Distance(comXZ, supportXZ);

            if (currentCoMOffset <= minCoMOffset)
            {
                comConfidence = 1.0f;
            }
            else if (currentCoMOffset >= maxCoMOffset)
            {
                comConfidence = 0.0f;
            }
            else
            {
                float t = (currentCoMOffset - minCoMOffset) / (maxCoMOffset - minCoMOffset);
                comConfidence = Mathf.Clamp01(1.0f - t);
            }
        }
    }
}
