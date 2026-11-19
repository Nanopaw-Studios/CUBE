using UnityEngine;

public class CursorManager : MonoBehaviour
{
    private void Start()
    {
        SetLocked();
    }

    public enum CursorState
    {
        Gameplay,
        UI,
    }

    public static void SetState(CursorState state)
    {
        Debug.Log("Cursor state set to: " + state);
        switch (state)
        {
            case CursorState.Gameplay:
                SetLocked();
                break;

            case CursorState.UI:
                SetVisible();
                break;
        }
    }

    public static void SetLocked()
    {
        SetCameraInputEnabled(true);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public static void SetVisible()
    {
        SetCameraInputEnabled(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    /// <summary>
    /// Resolves the local camera input without assuming a camera hierarchy exists.
    /// Menu and loading scenes have no player camera, and the old unguarded
    /// Camera.main.transform.parent.parent chain threw there.
    /// </summary>
    private static void SetCameraInputEnabled(bool enabled)
    {
        CameraInput cameraInput = null;

        if (Camera.main != null)
        {
            Transform cameraTransform = Camera.main.transform;
            if (cameraTransform.parent != null && cameraTransform.parent.parent != null)
                cameraInput = cameraTransform.parent.parent.GetComponent<CameraInput>();
        }

        if (cameraInput == null && PhotonLauncher.LocalPlayerInstance != null)
            cameraInput = PhotonLauncher.LocalPlayerInstance.GetComponentInChildren<CameraInput>(true);

        if (cameraInput == null)
            cameraInput = Object.FindFirstObjectByType<CameraInput>();

        if (cameraInput != null)
            cameraInput.enabled = enabled;
    }
}
