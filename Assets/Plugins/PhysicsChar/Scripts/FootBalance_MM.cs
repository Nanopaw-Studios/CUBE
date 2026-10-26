using UnityEngine;

public class FootBalance_MM : MonoBehaviour
{
    public Rig data;

    public Rigidbody rig;

    public Rigidbody fR;

    public Rigidbody fL;

    public float force;

    public void Start()
    {
        rig = GetComponent<Rigidbody>();
        data = GetComponentInParent<Rig>();
        fR = base.transform.parent.GetComponentInChildren<FootRight_MM>().GetComponent<Rigidbody>();
        fL = base.transform.parent.GetComponentInChildren<FootLeft_MM>().GetComponent<Rigidbody>();
    }

    public void FixedUpdate()
    {
        if (data == null || rig == null || fR == null || fL == null || data.torso == null || data.allRigs == null)
            return;

        // Ground-balance forces must only act when grounded. Applying 1500 accel while airborne
        // kicks the feet forward and pops knee joints into hyperextension during jumps/dashes!
        if (!data.Grounded || data.Ragdolling || data.control <= 0.05f)
            return;

        Vector3 zero = Vector3.zero;
        int count = data.allRigs.Count;
        for (int i = 0; i < count; i++)
        {
            if (data.allRigs[i] != null)
                zero += data.allRigs[i].position;
        }
        if (count > 0) zero /= count;

        Vector3 vector = (fR.position + fL.position) * 0.5f;
        zero.y = 0f;
        vector.y = 0f;

        Vector3 diff = Vector3.ClampMagnitude(zero - vector, 0.4f);

        float y = rig.transform.position.y;
        float y2 = data.torso.transform.position.y;
        float num = Mathf.Clamp(y2 - y, 0f, 1f);

        Vector3 accel = Vector3.ClampMagnitude(diff * num * data.control * force, 250f);
        rig.AddForce(accel, ForceMode.Acceleration);
    }
}
