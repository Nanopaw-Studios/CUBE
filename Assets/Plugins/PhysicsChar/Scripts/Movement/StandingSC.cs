
using UnityEngine;

public class StandingSC : MonoBehaviour
{
    public Rig rig;

    public LayerMask groundlayer;
    [Space]
    public AnimationCurve liftCurve;
    [Space]
    public RigWithMultiplier[] rigToLift;
    [Space]

    public float heightOffset;
    [Tooltip("Damping applied to vertical velocity to eliminate bouncy standing bobbing and instability.")]
    public float verticalDamping = 10f;

    private void Start()
    {
        rig = GetComponent<Rig>();
    }

    private void FixedUpdate()
    {
        if (rig == null || rig.torso == null) return;

        KeepStanding();
        LiftRigs();
    }

    void KeepStanding()
    {
        RaycastHit hit;
        if (Physics.Raycast(rig.torso.transform.position, Vector3.down, out hit, 1.9f, groundlayer, QueryTriggerInteraction.Ignore))
        {
            if (!hit.transform.IsChildOf(transform.root))
            {
                Debug.DrawRay(rig.torso.transform.position, Vector3.down * hit.distance, Color.yellow);
                rig.Ground = hit.point;
                return;
            }
        }

        rig.Ground = Vector3.zero;
    }

    void LiftRigs()
    {
        if (rigToLift == null || !rig.Grounded || rig.Ground == Vector3.zero) return;

        float liftMult = (rig.controlBlender != null) ? rig.controlBlender.StandingLiftMultiplier : 1.0f;
        float distance = rig.CalculateDistanceFromGround();
        float scaleX = base.transform.root != null ? base.transform.root.localScale.x : 1f;
        float curveVal = liftCurve != null ? liftCurve.Evaluate(distance / scaleX + heightOffset) : 1f;

        foreach (RigWithMultiplier rigw in rigToLift)
        {
            if (rigw != null && rigw.part != null)
            {
                float liftForce = rig.control * liftMult * 100f * rigw.multiplier * curveVal;
                // Vertical damping eliminates jitter, bouncy bobbing, and overshoot when standing
                float dampingForce = -rigw.part.linearVelocity.y * verticalDamping * rig.control;
                float totalLift = Mathf.Max(0f, liftForce + dampingForce);

                rigw.part.AddForce(Vector3.up * totalLift, ForceMode.Acceleration);
            }
        }
    }
}
