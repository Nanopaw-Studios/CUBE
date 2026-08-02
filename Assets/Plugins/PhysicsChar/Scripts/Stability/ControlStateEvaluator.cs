using UnityEngine;

namespace PhysicsChar.Stability
{
    public enum CharacterPoseState
    {
        Balanced,
        Stumbling,
        SittingOrKneeling,
        Uncontrolled
    }

    [DisallowMultipleComponent]
    public class ControlStateEvaluator : MonoBehaviour
    {
        [Header("References")]
        public Rig rig;
        public SupportDetector supportDetector;
        public BalanceDetector balanceDetector;
        public StabilityAnalyzer stabilityAnalyzer;

        [Header("Weights for Confidence Factors")]
        [Range(0f, 2f)] public float leanWeight = 1.0f;
        [Range(0f, 2f)] public float trappedFootWeight = 1.2f;
        [Range(0f, 2f)] public float angularVelocityWeight = 1.0f;
        [Range(0f, 2f)] public float centerOfMassWeight = 0.8f;
        [Range(0f, 2f)] public float footSupportWeight = 0.8f;
        [Range(0f, 2f)] public float motionMismatchWeight = 0.6f;

        [Header("Pose State Thresholds")]
        [Tooltip("Confidence threshold above which character is considered fully balanced.")]
        public float balancedThreshold = 0.80f;

        [Tooltip("Confidence threshold below which character is uncontrolled / falling.")]
        public float uncontrolledThreshold = 0.25f;

        [Header("Output State")]
        [Range(0f, 1f)] public float rawConfidence = 1.0f;
        public CharacterPoseState currentPose = CharacterPoseState.Balanced;
        public float standingLiftMultiplier = 1.0f;
        public float rotationTorqueMultiplier = 1.0f;
        public float movementForceMultiplier = 1.0f;

        private void Awake()
        {
            AutoInitializeReferences();
        }

        private void Start()
        {
            AutoInitializeReferences();
        }

        private void Reset()
        {
            AutoInitializeReferences();
        }

        public void AutoInitializeReferences()
        {
            if (rig == null) rig = GetComponent<Rig>();

            if (supportDetector == null)
            {
                supportDetector = GetComponent<SupportDetector>();
                if (supportDetector == null) supportDetector = gameObject.AddComponent<SupportDetector>();
            }

            if (balanceDetector == null)
            {
                balanceDetector = GetComponent<BalanceDetector>();
                if (balanceDetector == null) balanceDetector = gameObject.AddComponent<BalanceDetector>();
            }

            if (stabilityAnalyzer == null)
            {
                stabilityAnalyzer = GetComponent<StabilityAnalyzer>();
                if (stabilityAnalyzer == null) stabilityAnalyzer = gameObject.AddComponent<StabilityAnalyzer>();
            }
        }

        public void EvaluateState()
        {
            if (rig == null) return;

            AutoInitializeReferences();

            // 1. Run individual detectors
            if (supportDetector != null) supportDetector.EvaluateSupport();
            if (balanceDetector != null) balanceDetector.EvaluateBalance();
            if (stabilityAnalyzer != null) stabilityAnalyzer.EvaluateStability();

            // 2. Combine signals into continuous RawConfidence
            CalculateRawConfidence();

            // 3. Determine Pose State & Force Multipliers
            DeterminePoseState();
        }

        private void CalculateRawConfidence()
        {
            float leanConf = balanceDetector != null ? balanceDetector.leanConfidence : 1.0f;
            float angVelConf = balanceDetector != null ? balanceDetector.angVelConfidence : 1.0f;
            float comConf = balanceDetector != null ? balanceDetector.comConfidence : 1.0f;

            float footSuppConf = supportDetector != null ? supportDetector.footSupportRatio : 1.0f;
            bool pelvisSupp = supportDetector != null && supportDetector.isPelvisSupported;

            float trappedConf = stabilityAnalyzer != null ? stabilityAnalyzer.trappedFootConfidence : 1.0f;
            float mismatchConf = stabilityAnalyzer != null ? stabilityAnalyzer.motionMismatchConfidence : 1.0f;
            float impactConf = stabilityAnalyzer != null ? stabilityAnalyzer.impactConfidence : 1.0f;

            if (pelvisSupp)
            {
                footSuppConf = Mathf.Max(footSuppConf, 0.9f);
            }

            float weightedLean = Mathf.Pow(leanConf, leanWeight);
            float weightedAngVel = Mathf.Pow(angVelConf, angularVelocityWeight);
            float weightedCoM = Mathf.Pow(comConf, centerOfMassWeight);
            float weightedFootSupp = Mathf.Pow(footSuppConf, footSupportWeight);
            float weightedTrapped = Mathf.Pow(trappedConf, trappedFootWeight);
            float weightedMismatch = Mathf.Pow(mismatchConf, motionMismatchWeight);

            // Primary bottleneck (if character is severely tilted, trapped, or spinning)
            float primaryBottleneck = Mathf.Min(weightedLean, Mathf.Min(weightedTrapped, weightedAngVel));

            // Solid equilibrium deadzone: normal movement, running, and punching stay at crisp 1.0
            if (primaryBottleneck >= 0.85f)
            {
                rawConfidence = 1.0f;
            }
            else
            {
                float secondaryFactors = (weightedCoM + weightedFootSupp + weightedMismatch) / 3.0f;
                rawConfidence = primaryBottleneck * (0.5f + 0.5f * secondaryFactors) * impactConf;
            }
            rawConfidence = Mathf.Clamp01(rawConfidence);
        }

        private void DeterminePoseState()
        {
            bool pelvisSupp = supportDetector != null && supportDetector.isPelvisSupported;
            float footSuppRatio = supportDetector != null ? supportDetector.footSupportRatio : 1.0f;
            float leanAngle = balanceDetector != null ? balanceDetector.torsoAngle : 0.0f;

            // Detect genuine sitting / kneeling pose:
            // Pelvis supported on an elevated surface OR crouched low, while torso is upright
            if (pelvisSupp && leanAngle < 35f && footSuppRatio < 0.6f)
            {
                currentPose = CharacterPoseState.SittingOrKneeling;
                standingLiftMultiplier = 0.05f; // Disable standing lift so character rests seated
                rotationTorqueMultiplier = 0.7f;
                movementForceMultiplier = 0.3f;
                return;
            }

            if (rawConfidence >= balancedThreshold)
            {
                currentPose = CharacterPoseState.Balanced;
                standingLiftMultiplier = 1.0f;
                rotationTorqueMultiplier = 1.0f;
                movementForceMultiplier = 1.0f;
            }
            else if (rawConfidence < uncontrolledThreshold)
            {
                currentPose = CharacterPoseState.Uncontrolled;
                if (rig.Grounded)
                {
                    // Provide baseline recovery force when grounded so character can stand back up!
                    standingLiftMultiplier = 0.35f;
                    rotationTorqueMultiplier = 0.40f;
                }
                else
                {
                    standingLiftMultiplier = 0.0f;
                    rotationTorqueMultiplier = 0.05f;
                }
                movementForceMultiplier = 0.0f;
            }
            else
            {
                currentPose = CharacterPoseState.Stumbling;
                float t = (rawConfidence - uncontrolledThreshold) / (balancedThreshold - uncontrolledThreshold);
                standingLiftMultiplier = Mathf.Lerp(0.5f, 1.0f, t);
                rotationTorqueMultiplier = Mathf.Lerp(0.4f, 1.0f, t);
                movementForceMultiplier = Mathf.Lerp(0.3f, 1.0f, t);
            }
        }
    }
}
