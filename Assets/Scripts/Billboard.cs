using System.Collections;
using UnityEngine;

public class CameraFacingBillboard : MonoBehaviour
{
    private Camera cam;

    void LateUpdate()
    {
        if (cam == null)
            cam = Camera.main;

        if (cam == null)
            return;

        transform.LookAt(
            transform.position + cam.transform.rotation * Vector3.forward,
            cam.transform.rotation * Vector3.up
        );
    }
}
