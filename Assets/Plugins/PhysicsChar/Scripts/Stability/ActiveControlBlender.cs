using UnityEngine;

namespace PhysicsChar.Stability
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-10)] // Run before Movement/Standing controllers
    public class ActiveControlBlender : MonoBehaviour
    {
        [Header("References")]
        public Rig rig;
        public ControlStateEvaluator evaluator;

        [Header("Blending Dynamics")]
        [Tooltip("How fast control confidence drops when character becomes unstable (units/sec).")]
        public float dropSpeed = 12f;

        [Tooltip("How smoothly control confidence recovers when character regains stability (units/sec).")]
        public float recoverSpeed = 10f;

        [Header("Live Smoothed State")]
        [Range(0f, 1f)] public float smoothedConfidence = 1.0f;
        public CharacterPoseState activePose = CharacterPoseState.Balanced;

        // Modulated force multipliers for other controllers
        public float StandingLiftMultiplier => evaluator != null ? evaluator.standingLiftMultiplier : 1.0f;
        public float RotationTorqueMultiplier => evaluator != null ? evaluator.rotationTorqueMultiplier : 1.0f;
        public float MovementForceMultiplier => evaluator != null ? evaluator.movementForceMultiplier : 1.0f;

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

            if (evaluator == null)
            {
                evaluator = GetComponent<ControlStateEvaluator>();
                if (evaluator == null && rig != null)
                {
                    evaluator = rig.gameObject.AddComponent<ControlStateEvaluator>();
                }
            }
        }

        private void FixedUpdate()
        {
            if (rig == null) return;

            // 1. Evaluate physical stability state
            if (evaluator != null)
            {
                evaluator.EvaluateState();
                activePose = evaluator.currentPose;
                float targetConf = evaluator.rawConfidence;

                if (rig.Grounded)
                {
                    // Ensure baseline recovery floor when grounded so ragdoll can get back up!
                    targetConf = Mathf.Max(targetConf, 0.30f);
                }

                // 2. Asymmetric blending (fast drop, gradual recovery)
                if (targetConf < smoothedConfidence)
                {
                    smoothedConfidence = Mathf.MoveTowards(smoothedConfidence, targetConf, dropSpeed * Time.fixedDeltaTime);
                }
                else
                {
                    smoothedConfidence = Mathf.MoveTowards(smoothedConfidence, targetConf, recoverSpeed * Time.fixedDeltaTime);
                }
            }
            else
            {
                smoothedConfidence = 1.0f;
            }

            // 3. Apply to rig.control unless manual ragdoll is active
            if (rig.Ragdolling)
            {
                rig.control = 0f;
            }
            else
            {
                rig.control = smoothedConfidence;
            }
        }
    }
}
