using UnityEngine;

namespace PhysicsChar.Stability
{
    [DisallowMultipleComponent]
    public class SupportDetector : MonoBehaviour
    {
        [Header("References")]
        public Rig rig;

        [Header("Pelvis / Sitting Detection")]
        [Tooltip("Layer mask for detecting pelvis support (ground, furniture, obstacles).")]
        public LayerMask supportLayer = ~0;

        [Tooltip("Max raycast distance down from hips/pelvis to detect sitting or pelvis support.")]
        public float pelvisRayDistance = 0.45f;

        [Tooltip("Max height of hips above feet level to consider as crouched/sitting.")]
        public float sittingHeightThreshold = 0.55f;

        [Header("Output State")]
        public bool isPelvisSupported;
        public Vector3 pelvisGroundPoint;
        public Vector3 pelvisGroundNormal = Vector3.up;
        public float pelvisGroundDistance;

        public int groundedFeetCount;
        public float footSupportRatio; // 0.0 to 1.0 based on feet grounded
        public Vector3 averageFeetPosition;
        public Vector3 supportCenter; // Combined center of foot/pelvis support

        private void Start()
        {
            if (rig == null)
            {
                rig = GetComponent<Rig>();
            }
        }

        public void EvaluateSupport()
        {
            if (rig == null) return;

            EvaluateFeetSupport();
            EvaluatePelvisSupport();
            CalculateSupportCenter();
        }

        private void EvaluateFeetSupport()
        {
            groundedFeetCount = 0;
            Vector3 feetPosSum = Vector3.zero;

            if (rig.Feet != null && rig.Feet.Length > 0)
            {
                foreach (CollisionHandler foot in rig.Feet)
                {
                    if (foot != null)
                    {
                        feetPosSum += foot.transform.position;
                        if (foot.TouchGround)
                        {
                            groundedFeetCount++;
                        }
                    }
                }
                averageFeetPosition = feetPosSum / rig.Feet.Length;
            }
            else
            {
                averageFeetPosition = rig.hip != null ? rig.hip.position : transform.position;
            }

            // Normal jumping or brief airborne falls (< 1.2s) keep full foot support
            if (rig.Grounded || rig.sinceGrounded < 1.2f)
            {
                footSupportRatio = 1.0f;
            }
            else
            {
                // Extended freefall: gradually taper off after 1.2 seconds
                float airTime = rig.sinceGrounded - 1.2f;
                footSupportRatio = Mathf.Clamp01(1.0f - airTime * 0.5f);
            }
        }

        private void EvaluatePelvisSupport()
        {
            isPelvisSupported = false;
            pelvisGroundDistance = float.MaxValue;
            pelvisGroundNormal = Vector3.up;

            Transform hipTransform = rig.hip != null ? rig.hip : (rig.torso != null ? rig.torso : transform);
            if (hipTransform == null) return;

            float currentHipHeightAboveFeet = hipTransform.position.y - averageFeetPosition.y;

            // Hips are only considered supported if:
            // 1. A raycast down from hips (0.45m max) hits an elevated surface above feet level (e.g. sitting on box/chair)
            // 2. OR hips have physically dropped low (< sittingHeightThreshold above feet)
            RaycastHit hit;
            if (Physics.Raycast(hipTransform.position, Vector3.down, out hit, pelvisRayDistance, supportLayer))
            {
                if (!hit.transform.IsChildOf(rig.transform))
                {
                    float hitHeightAboveFeet = hit.point.y - averageFeetPosition.y;
                    if (hitHeightAboveFeet > 0.15f || currentHipHeightAboveFeet < sittingHeightThreshold)
                    {
                        isPelvisSupported = true;
                        pelvisGroundPoint = hit.point;
                        pelvisGroundNormal = hit.normal;
                        pelvisGroundDistance = hit.distance;
                    }
                }
            }

            if (!isPelvisSupported && currentHipHeightAboveFeet < sittingHeightThreshold && rig.Grounded)
            {
                isPelvisSupported = true;
                pelvisGroundPoint = averageFeetPosition;
                pelvisGroundDistance = currentHipHeightAboveFeet;
            }
        }

        private void CalculateSupportCenter()
        {
            if (isPelvisSupported && footSupportRatio < 0.5f)
            {
                supportCenter = pelvisGroundPoint;
            }
            else if (isPelvisSupported)
            {
                supportCenter = (averageFeetPosition + pelvisGroundPoint) * 0.5f;
            }
            else
            {
                supportCenter = averageFeetPosition;
            }
        }
    }
}
