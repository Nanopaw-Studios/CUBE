using UnityEngine;

[CreateAssetMenu(fileName = "GlitchDash", menuName = "ScriptableObjects/Killer Abilities/GlitchDash", order = 1)]
public class GlitchDash : Ability
{
    public int dashDistance = 5;
    public AudioClip dashSound;
    public GameObject DashEffectsPrefab;

    public override void ActivateAbility(BasePlayer player)
    {
        base.ActivateAbility(player);
        Dash(player);
    }

    void Dash(BasePlayer player)
    {
        Transform torso = player.transform.Find("RIG")?.Find("Torso");
        Vector3 soundPos = torso != null ? torso.position : player.transform.position;

        // Ensure dash sound is loaded
        if (dashSound == null)
        {
            dashSound = Resources.Load<AudioClip>("SFX/punch_swing") ?? Resources.Load<AudioClip>("SFX/woosh");
        }

        // Play dash sound locally at the character's body position
        if (dashSound != null)
        {
            AudioSource.PlayClipAtPoint(dashSound, soundPos, 1f);
        }

        // Broadcast dash sound and visual effects to remote players
        GlitchDashRunner runner = player.GetComponent<GlitchDashRunner>();
        if (runner == null)
            runner = player.gameObject.AddComponent<GlitchDashRunner>();
        runner.BroadcastDashEffects(soundPos);

        // Instantiate dash effects locally
        if (DashEffectsPrefab != null && torso != null)
        {
            GameObject dashEffects = Instantiate(
                DashEffectsPrefab,
                new Vector3(torso.position.x, 0.5f, torso.position.z),
                Quaternion.Euler(torso.rotation.eulerAngles + new Vector3(-180, 0, 0)),
                null);
            Object.Destroy(dashEffects, 2f); // Destroy effects after 2 seconds
        }

        // Determine uniform dash direction based on camera / player forward (avoid bone-local forward)
        Transform camTransform = player.transform.Find("CameraHead")?.Find("Cam")?.Find("ShakePivot")?.Find("C")
            ?? (Camera.main != null ? Camera.main.transform : null);

        Vector3 forward = camTransform != null ? camTransform.forward : (torso != null ? torso.forward : player.transform.forward);
        Vector3 horizontal = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
        if (horizontal.sqrMagnitude < 0.001f)
        {
            horizontal = torso != null ? Vector3.ProjectOnPlane(torso.forward, Vector3.up).normalized : player.transform.forward;
        }

        // Slight upward bias so dashing on slopes or curbs doesn't dig into ground
        Vector3 dashDir = (horizontal + Vector3.up * 0.08f).normalized;
        Vector3 dashVelocity = dashDir * dashDistance;

        Transform rigTransform = player.transform.Find("RIG");
        if (rigTransform != null)
        {
            Rigidbody[] bodies = rigTransform.GetComponentsInChildren<Rigidbody>();

            // 1. Clear any downward vertical velocity so gravity momentum doesn't eat into the dash
            foreach (Rigidbody rb in bodies)
            {
                if (rb != null && rb.linearVelocity.y < 0f)
                {
                    Vector3 vel = rb.linearVelocity;
                    vel.y = 0f;
                    rb.linearVelocity = vel;
                }
            }

            // 2. Apply uniform velocity to ALL bones in the EXACT same direction
            // Applying identical velocity change prevents joint stretching, hyperextension, and leg inversion!
            foreach (Rigidbody rb in bodies)
            {
                if (rb != null)
                {
                    rb.AddForce(dashVelocity, ForceMode.VelocityChange);
                }
            }
        }
    }
}
