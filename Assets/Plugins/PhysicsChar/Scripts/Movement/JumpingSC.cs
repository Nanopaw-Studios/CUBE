using System.Collections.Generic;
using UnityEngine;

public class JumpingSC : MonoBehaviour
{
    Rig data;

    public float Force;

    public RigWithMultiplier[] rigs;

    public AnimationCurve curve;

    private void Start()
    {
        data = GetComponent<Rig>();
    }

    public void Jump()
    {
        if (data == null)
        {
            data = GetComponent<Rig>();
        }

        // Control scaling factor
        float ctrl = (data != null) ? Mathf.Clamp01(data.control) : 1f;

        // Evaluate curve at peak (t = 1) instead of arbitrary t = 10
        float curveMult = 1f;
        if (curve != null && curve.length > 0)
        {
            float eval = curve.Evaluate(1f);
            if (eval > 0f) curveMult = eval;
        }

        // Determine predictable upward launch direction (heavily weighted to world Up)
        Vector3 jumpDir = Vector3.up;
        if (data != null && data.NONROTATIONOBJ != null)
        {
            jumpDir = Vector3.Lerp(Vector3.up, data.NONROTATIONOBJ.up, 0.15f).normalized;
        }

        HashSet<Rigidbody> mainParts = new HashSet<Rigidbody>();

        // 1. Clear negative vertical velocity across all ragdoll parts so standing drag and downward momentum don't swallow the jump
        if (data != null && data.allRigs != null)
        {
            foreach (Rigidbody rb in data.allRigs)
            {
                if (rb != null && rb.linearVelocity.y < 0f)
                {
                    Vector3 vel = rb.linearVelocity;
                    vel.y = 0f;
                    rb.linearVelocity = vel;
                }
            }
        }

        // 2. Apply main jump impulse to designated rig parts (Hip, Torso)
        if (rigs != null)
        {
            foreach (RigWithMultiplier rig in rigs)
            {
                if (rig != null && rig.part != null)
                {
                    mainParts.Add(rig.part);
                    float impulse = Force * rig.multiplier * ctrl * curveMult;
                    rig.part.AddForce(jumpDir * impulse, ForceMode.Impulse);
                }
            }
        }

        // 3. Apply supporting upward impulse to all other ragdoll parts (feet, legs, arms) to unstick from ground friction
        if (data != null && data.allRigs != null)
        {
            foreach (Rigidbody rb in data.allRigs)
            {
                if (rb != null && !mainParts.Contains(rb))
                {
                    float supportImpulse = Force * ctrl * curveMult * rb.mass * 0.5f;
                    rb.AddForce(jumpDir * supportImpulse, ForceMode.Impulse);
                }
            }
        }
    }
}
