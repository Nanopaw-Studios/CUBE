using UnityEngine;
using UnityEngine.InputSystem;

public class Noclipper : MonoBehaviour
{
    private new Camera camera;
    public InputActionReference noclipAction;

    GameObject noclipCameraPrefab;
    GameObject noclipCameraObj;

    public bool noclipping = false;

    void OnEnable()
    {
        // Development-only cheat. Never ship free-fly in a release build.
        if (!Application.isEditor && !Debug.isDebugBuild)
        {
            enabled = false;
            return;
        }

        if (noclipAction == null || noclipAction.action == null)
            return;

        noclipAction.action.Enable();
        noclipAction.action.performed += NoclipPressed;
    }

    void OnDisable()
    {
        if (noclipAction == null || noclipAction.action == null)
            return;

        noclipAction.action.performed -= NoclipPressed;
        noclipAction.action.Disable();
    }

    void Start()
    {
        camera = Camera.main;
        noclipCameraPrefab = Resources.Load<GameObject>("Debug/NoclipCamera");
    }

    private void NoclipPressed(InputAction.CallbackContext context)
    {
        Debug.Log("Noclip action fired!");
        if (camera == null)
            camera = Camera.main;

        if (camera == null || noclipCameraPrefab == null)
            return;

        noclipping = !noclipping;

        if (noclipping)
        {
            // Hide player camera
            camera.gameObject.SetActive(false);

            // Create noclip cam if needed
            if (noclipCameraObj == null)
            {
                noclipCameraObj = Instantiate(noclipCameraPrefab);
            }

            noclipCameraObj.transform.SetPositionAndRotation(
                camera.transform.position,
                camera.transform.rotation
            );
            noclipCameraObj.SetActive(true);
        }
        else
        {
            // Restore player camera
            camera.gameObject.SetActive(true);

            // Disable noclip cam
            if (noclipCameraObj != null)
            {
                noclipCameraObj.SetActive(false);
            }
        }
    }
}
