using UnityEngine;
using UnityEngine.InputSystem;

public class MenuCameraController : MonoBehaviour
{
    [Header("Sway Settings")]
    public float swayAmount = 0.02f;
    public float swaySpeed = 1.5f;

    [Header("Mouse Look Offset")]
    public float mouseOffsetAmount = 2f;
    public float mouseSmoothTime = 5f;

    [Header("Rotation Limits")]
    public float maxTiltX = 2f;
    public float maxTiltY = 2f;

    private Vector3 initialPosition;
    private Quaternion initialRotation;

    private Vector2 currentMouseOffset;

    void Start()
    {
        initialPosition = transform.localPosition;
        initialRotation = transform.localRotation;
    }

    void Update()
    {
        HandleSway();
        HandleMouseOffset();
    }

    void HandleSway()
    {
        float time = Time.time * swaySpeed;

        float swayX = Mathf.Sin(time) * swayAmount;
        float swayY = Mathf.Cos(time * 0.8f) * swayAmount;

        Vector3 sway = new Vector3(swayX, swayY, 0f);
        transform.localPosition = initialPosition + sway;
    }

    void HandleMouseOffset()
    {
        if (Mouse.current == null)
            return;

        Vector2 mousePos = Mouse.current.position.ReadValue();

        // Convert to [-1, 1] range
        float mouseX = (mousePos.x / Screen.width) * 2f - 1f;
        float mouseY = (mousePos.y / Screen.height) * 2f - 1f;

        Vector2 targetOffset = new Vector2(mouseX, mouseY) * mouseOffsetAmount;

        // Smooth interpolation
        currentMouseOffset = Vector2.Lerp(
            currentMouseOffset,
            targetOffset,
            Time.deltaTime * mouseSmoothTime
        );

        float tiltX = Mathf.Clamp(-currentMouseOffset.y, -maxTiltX, maxTiltX);
        float tiltY = Mathf.Clamp(currentMouseOffset.x, -maxTiltY, maxTiltY);

        Quaternion targetRot = Quaternion.Euler(tiltX, tiltY, 0f);

        transform.localRotation = Quaternion.Slerp(
            transform.localRotation,
            initialRotation * targetRot,
            Time.deltaTime * mouseSmoothTime
        );
    }
}