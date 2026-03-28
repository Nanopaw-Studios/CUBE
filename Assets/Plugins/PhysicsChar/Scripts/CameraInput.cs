using UnityEngine;
using UnityEngine.InputSystem;

public class CameraInput : MonoBehaviour
{
    [Header("Sensitivity Settings")]
    public float mouseSensitivity = 20f;
    public float gamepadSensitivity = 100f;

    [Header("Smoothing")]
    public float smoothTime = 0.05f;
    public Rotation_MM PlayerBody;

    [Header("Input System")]
    public InputActionAsset inputActions;

    private InputAction lookAction;

    private float xRotation;
    private float yRotation;
    private float xRotationVelocity;
    private float yRotationVelocity;

    private const string KEY_MOUSE_SENSITIVITY = "MouseSensitivity";
    private const string KEY_GAMEPAD_SENSITIVITY = "GamepadSensitivity";
    private const string OLD_KEY_MOUSE_SENSITIVITY = "MouseSensivity";
    private const string OLD_KEY_GAMEPAD_SENSITIVITY = "GamepadSensivity";

    private void Awake()
    {
        mouseSensitivity = LoadFloatWithMigration(KEY_MOUSE_SENSITIVITY, OLD_KEY_MOUSE_SENSITIVITY, 20f);
        gamepadSensitivity = LoadFloatWithMigration(KEY_GAMEPAD_SENSITIVITY, OLD_KEY_GAMEPAD_SENSITIVITY, 100f);

        ResolveInput();
    }

    private void ResolveInput()
    {
        if (inputActions == null)
        {
            Debug.LogError("[CameraInput] inputActions is NULL");
            return;
        }

        // Try common patterns
        lookAction = inputActions.FindAction("Player/Look", false);

        if (lookAction == null)
            lookAction = inputActions.FindAction("Look", false);

        if (lookAction == null)
        {
            Debug.LogError("[CameraInput] Look action not found!");
            DumpActions();
            return;
        }

        Debug.Log($"[CameraInput] Found Look action: {lookAction.name}");

        inputActions.Enable();
        lookAction.Enable();
    }

    private void Start()
    {
        Vector3 euler = transform.localEulerAngles;
        xRotation = NormalizeAngle(euler.x);
        yRotation = NormalizeAngle(euler.y);
    }

    private void OnEnable()
    {
        lookAction?.Enable();
    }

    private void OnDisable()
    {
        lookAction?.Disable();
    }

    private void Update()
    {
        if (lookAction == null)
            return;

        Vector2 lookInput = lookAction.ReadValue<Vector2>();

        if (lookInput.sqrMagnitude <= 0.000001f)
            return;

        bool isGamepad = lookAction.activeControl != null &&
                         lookAction.activeControl.device is Gamepad;

        float sensitivity = isGamepad ? gamepadSensitivity : mouseSensitivity;

        float multiplier = isGamepad ? Time.deltaTime : 1f;

        float mouseX = lookInput.x * sensitivity * multiplier;
        float mouseY = lookInput.y * sensitivity * multiplier;

        yRotation += mouseX;
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        float currentX = NormalizeAngle(transform.localEulerAngles.x);
        float currentY = NormalizeAngle(transform.localEulerAngles.y);

        float smoothX = Mathf.SmoothDampAngle(currentX, xRotation, ref xRotationVelocity, smoothTime);
        float smoothY = Mathf.SmoothDampAngle(currentY, yRotation, ref yRotationVelocity, smoothTime);

        transform.localRotation = Quaternion.Euler(smoothX, smoothY, 0f);

        if (PlayerBody != null)
        {
            PlayerBody.TargetRotation = new Vector3(0f, smoothY, 0f);
        }
    }

    private void DumpActions()
    {
        foreach (var map in inputActions.actionMaps)
        {
            Debug.Log($"Map: {map.name}");
            foreach (var action in map.actions)
            {
                Debug.Log($"  Action: {action.name}");
            }
        }
    }

    private float LoadFloatWithMigration(string key, string oldKey, float defaultValue)
    {
        if (PlayerPrefs.HasKey(key))
            return PlayerPrefs.GetFloat(key);

        if (!string.IsNullOrEmpty(oldKey) && PlayerPrefs.HasKey(oldKey))
        {
            float migrated = PlayerPrefs.GetFloat(oldKey);
            PlayerPrefs.SetFloat(key, migrated);
            PlayerPrefs.Save();
            return migrated;
        }

        PlayerPrefs.SetFloat(key, defaultValue);
        PlayerPrefs.Save();
        return defaultValue;
    }

    private float NormalizeAngle(float angle)
    {
        if (angle > 180f) angle -= 360f;
        return angle;
    }
}