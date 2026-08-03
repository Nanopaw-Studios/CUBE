using UnityEngine;
using UnityEngine.InputSystem;

public class CameraInput : MonoBehaviour
{
    [Header("Sensitivity Settings")]
    public float mouseSensitivity = 2f;
    public float gamepadSensitivity = 100f;

    [Header("Input Smoothing")]
    public float inputSmoothTime = 0.03f;
    private Vector2 currentInput;
    private Vector2 inputVelocity;

    [Header("Rotation Smoothing (Optional)")]
    public float rotationSmoothTime = 0.05f;
    private float xRotationVelocity;
    private float yRotationVelocity;

    [Header("References")]
    public Rotation_MM PlayerBody;
    public InputActionAsset inputActions;

    private InputAction lookAction;

    private float xRotation;
    private float yRotation;

    private const string KEY_MOUSE_SENSITIVITY = "MouseSensitivity";
    private const string KEY_GAMEPAD_SENSITIVITY = "GamepadSensitivity";
    private const string OLD_KEY_MOUSE_SENSITIVITY = "MouseSensivity";
    private const string OLD_KEY_GAMEPAD_SENSITIVITY = "GamepadSensivity";

    private void Awake()
    {
        RefreshSensitivity();
        ResolveInput();
    }

    private void OnEnable()
    {
        RefreshSensitivity();
    }

    public void RefreshSensitivity()
    {
        mouseSensitivity = LoadFloatWithMigration(KEY_MOUSE_SENSITIVITY, OLD_KEY_MOUSE_SENSITIVITY, 2f);
        gamepadSensitivity = LoadFloatWithMigration(KEY_GAMEPAD_SENSITIVITY, OLD_KEY_GAMEPAD_SENSITIVITY, 100f);
    }

    private void ResolveInput()
    {
        if (inputActions == null)
        {
            Debug.LogError("[CameraInput] inputActions is NULL");
            return;
        }

        lookAction = inputActions.FindAction("Player/Look", false)
                   ?? inputActions.FindAction("Look", false);

        if (lookAction == null)
        {
            Debug.LogError("[CameraInput] Look action not found!");
            DumpActions();
            return;
        }

        inputActions.Enable();
        lookAction.Enable();
    }

    private void Start()
    {
        Vector3 euler = transform.localEulerAngles;
        xRotation = NormalizeAngle(euler.x);
        yRotation = NormalizeAngle(euler.y);
    }

    private void Update()
    {
        if (lookAction == null) return;

        RefreshSensitivity();

        Vector2 rawInput = lookAction.ReadValue<Vector2>();

        bool isGamepad = lookAction.activeControl != null &&
                         lookAction.activeControl.device is Gamepad;

        float sensitivity = isGamepad ? gamepadSensitivity : mouseSensitivity;
        float multiplier = isGamepad ? Time.deltaTime : 0.0025f;

        // 🎯 Smooth INPUT instead of rotation
        currentInput = Vector2.SmoothDamp(
            currentInput,
            rawInput,
            ref inputVelocity,
            inputSmoothTime
        );

        Vector2 processedInput = currentInput * sensitivity * multiplier;

        yRotation += processedInput.x;
        xRotation -= processedInput.y;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        // Optional extra smoothing layer (can set to 0 if you want raw feel)
        float smoothX = Mathf.SmoothDampAngle(
            transform.localEulerAngles.x,
            xRotation,
            ref xRotationVelocity,
            rotationSmoothTime
        );

        float smoothY = Mathf.SmoothDampAngle(
            transform.localEulerAngles.y,
            yRotation,
            ref yRotationVelocity,
            rotationSmoothTime
        );

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