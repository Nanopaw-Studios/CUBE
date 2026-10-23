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

        Transform[] bones = player.transform.Find("RIG").GetComponentsInChildren<Transform>();
        foreach (Transform bone in bones)
        {
            if (bone.GetComponent<Rigidbody>() == null) continue;

            Vector3 dashDirection = bone.transform.forward * dashDistance; // Dash forward by units
            bone.GetComponent<Rigidbody>().AddForce(dashDirection, ForceMode.VelocityChange);
        }

        // Apply brief speed status effect surge
        StatusEffectManager sem = player.GetComponent<StatusEffectManager>();
        sem?.ApplyEffect(StatusEffectType.Speed, 3, 1.5f);
    }
}
