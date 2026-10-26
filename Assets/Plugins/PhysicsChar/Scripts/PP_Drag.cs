using System;
using UnityEngine;

public class PP_Drag : MonoBehaviour
{
	private Rig data;

	public PP_Drag_State[] dragStates;

	private void Start()
	{
		data = GetComponent<Rig>();
		if (data != null && data.allRigs != null)
		{
			for (int i = 0; i < data.allRigs.Count; i++)
			{
				if (data.allRigs[i] != null)
				{
					bool isArm = data.allRigs[i].name.Contains("Arm") || data.allRigs[i].name.Contains("Hand") || data.allRigs[i].name.Contains("Elbow");
					data.allRigs[i].maxAngularVelocity = isArm ? 100f : 30f;
				}
			}
		}
	}

	private void FixedUpdate()
	{
		if (data == null || data.allRigs == null) return;

		float num = 0f;
		float num2 = 0f;
		if (dragStates != null)
		{
			for (int i = 0; i < dragStates.Length; i++)
			{
				if (data.Currentstate == dragStates[i].animState)
				{
					num = dragStates[i].drag;
					num2 = dragStates[i].angularDrag;
					break;
				}
			}
		}

		float dtFactor = Time.fixedDeltaTime * 50f;
		float ctrl = data.control;

		for (int j = 0; j < data.allRigs.Count; j++)
		{
			Rigidbody rb = data.allRigs[j];
			if (rb == null) continue;

			Vector3 velocity = rb.linearVelocity;
			Vector3 angularVelocity = rb.angularVelocity;

			float speed = velocity.magnitude;
			if (speed > 0.001f)
			{
				// Soften drag scaling during high-velocity impulses (e.g. dashes, explosions)
				// so impulses carry through naturally instead of hitting a brick wall of artificial drag
				float speedFactor = (speed > 10f) ? Mathf.Pow(10f, 0.5f) : Mathf.Pow(speed, 0.5f);
				float linDragDelta = ctrl * num * speedFactor * dtFactor;
				linDragDelta = Mathf.Min(linDragDelta, speed);
				rb.linearVelocity -= velocity.normalized * linDragDelta;
			}

			float angSpeed = angularVelocity.magnitude;
			if (angSpeed > 0.001f)
			{
				float angDragDelta = ctrl * num2 * Mathf.Pow(angSpeed, 0.5f) * dtFactor;
				angDragDelta = Mathf.Min(angDragDelta, angSpeed);
				rb.angularVelocity -= angularVelocity.normalized * angDragDelta;
			}
		}
	}
}


[Serializable]
public class PP_Drag_State
{
    public StateType animState;

    public float drag;

    public float angularDrag;
}
