using UnityEngine;

public class CollisionHandler : MonoBehaviour
{
    public bool TouchGround;
    public float Length = 0.25f;
    public LayerMask groundMask = ~0;

    private Transform rootTransform;

    private void Awake()
    {
        rootTransform = transform.root;
    }

    private void Update()
    {
        CheckGround();
    }

    private void FixedUpdate()
    {
        CheckGround();
    }

    private void CheckGround()
    {
        float checkDist = Mathf.Max(Length, 0.22f);

        // Raycast down from foot center ignoring triggers and self colliders
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, checkDist, groundMask, QueryTriggerInteraction.Ignore))
        {
            if (rootTransform == null || !hit.transform.IsChildOf(rootTransform))
            {
                Debug.DrawRay(transform.position, Vector3.down * hit.distance, Color.red);
                TouchGround = true;
                return;
            }
        }

        // Small spherecast fallback to handle curved obstacles or uneven stepping terrain
        if (Physics.SphereCast(transform.position, 0.06f, Vector3.down, out RaycastHit sphereHit, checkDist, groundMask, QueryTriggerInteraction.Ignore))
        {
            if (rootTransform == null || !sphereHit.transform.IsChildOf(rootTransform))
            {
                TouchGround = true;
                return;
            }
        }

        TouchGround = false;
    }
}
