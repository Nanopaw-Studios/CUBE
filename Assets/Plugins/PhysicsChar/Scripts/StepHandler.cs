using UnityEngine;

public class StepHandler : MonoBehaviour
{

    public PartAnimation[] Legs;
    private float timer = 0f;
    public float interval = 0.5f;
    bool Left;
    private Rig data;

    private void Start()
    {
        data = GetComponent<Rig>();
    }

    private void FixedUpdate()
    {
        if (data != null && (!data.Grounded || data.Ragdolling || data.control <= 0.1f || data.Currentstate == StateType.Idle))
        {
            timer = 0f;
            return;
        }

        timer += Time.fixedDeltaTime;

        if (timer >= interval)
        {
            timer -= interval;

            OnStepInterval();
        }
    }

    private void OnStepInterval()
    {
        if (Legs == null || Legs.Length < 2) return;

        if (Left)
        {
            // left
            if (Legs[0] != null) Legs[0].Step();
            Left = false;
        }
        else
        {
            // right
            if (Legs[1] != null) Legs[1].Step();
            Left = true;
        }
    }
}