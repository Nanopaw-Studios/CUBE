using UnityEngine;

public class WorldSpaceCanvasFinder : MonoBehaviour
{
    private Camera cam;

    private void Update()
    {
        if (cam != null)
            return;

        cam = Camera.main;
        if (cam == null)
            return;

        Canvas canvas = GetComponent<Canvas>();
        if (canvas != null)
            canvas.worldCamera = cam;
    }
}
