using System.Collections.Generic;
using UnityEngine;

public class Rig : MonoBehaviour
{

    public StateType Currentstate;



    public Transform hip;
    public Transform torso;
    public Transform head;
    
    public Transform NONROTATIONOBJ;

    public CollisionHandler[] Feet;


    public List<Rigidbody> allRigs;
    public List<Rigidbody> Arms;



    public float control = 1;
    public float distancetoground = 0;
    public float sinceGrounded = 0;

    public StandingSC standing;
    public MovementSC movement;
    public CharacterInput characterinput;
    public JumpingSC jumping;
    public StepHandler stepHandler;
    public PhysicsChar.Stability.ActiveControlBlender controlBlender;

    public Vector3 Ground = new Vector3();
    public bool Grounded;
    public bool Ragdolling;

    private void Start()
    {
        standing = GetComponent<StandingSC>();
        movement = GetComponent<MovementSC>();
        characterinput = GetComponent<CharacterInput>();
        jumping = GetComponent<JumpingSC>();
        stepHandler = GetComponent<StepHandler>();
        controlBlender = GetComponent<PhysicsChar.Stability.ActiveControlBlender>();

        if (controlBlender == null)
        {
            controlBlender = gameObject.AddComponent<PhysicsChar.Stability.ActiveControlBlender>();
        }

        EnforceJointStability();
    }

    /// <summary>
    /// Configures all ragdoll joints with rock-solid projection and solver iterations.
    /// Prevents knees/limbs from inverting, hyper-extending, or locking up under high forces!
    /// </summary>
    public void EnforceJointStability()
    {
        if (allRigs == null) return;

        foreach (Rigidbody rb in allRigs)
        {
            if (rb == null) continue;

            // Stable max angular velocity clamp to prevent high-impulse spin glitches;
            // arms receive higher angular headroom so punches and arm poses are crisp and responsive
            bool isArm = rb.name.Contains("Arm") || rb.name.Contains("Hand") || rb.name.Contains("Elbow");
            rb.maxAngularVelocity = isArm ? 100f : 30f;
            rb.solverIterations = 12;
            rb.solverVelocityIterations = 4;

            ConfigurableJoint joint = rb.GetComponent<ConfigurableJoint>();
            if (joint != null)
            {
                bool isLegOrKnee = rb.name.Contains("Leg") || rb.name.Contains("Knee");

                if (isLegOrKnee)
                {
                    // For legs and knees: tight projection prevents knees from inverting/hyperextending under high impulses
                    joint.projectionMode = JointProjectionMode.PositionAndRotation;
                    joint.projectionDistance = 0.05f;
                    joint.projectionAngle = 10f;
                }
                else
                {
                    // For arms, hands, head, torso: full angular freedom so arms can swing freely without being clamped
                    joint.projectionMode = JointProjectionMode.PositionAndRotation;
                    joint.projectionDistance = 0.1f;
                    joint.projectionAngle = 180f;
                }
            }
        }
    }

    public float CalculateDistanceFromGround()
    {
        if (head == null) return 0f;
        float distance = Mathf.Abs(Ground.y - head.position.y);
        distancetoground = distance;
        return distance;
    }

    private void Update()
    {
        UpdateGroundedState();
    }

    private void FixedUpdate()
    {
        UpdateGroundedState();

        if (Grounded)
        {
            sinceGrounded = 0f;
        }
        else
        {
            sinceGrounded += Time.fixedDeltaTime;
        }
    }

    private void UpdateGroundedState()
    {
        if (Feet == null || Feet.Length == 0) return;

        int amountTouching = 0;
        foreach (CollisionHandler handler in Feet)
        {
            if (handler != null && handler.TouchGround)
            {
                amountTouching++;
            }
        }
        Grounded = amountTouching > 0;
    }

}
