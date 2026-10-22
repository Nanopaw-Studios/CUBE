using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// XR-011 / XR-015: Controller Disconnect Notification System.
/// 
/// Monitors hardware controller state via the Unity Input System. If the active gamepad
/// is disconnected or loses power during gameplay, displays an immediate, compliant on-screen
/// modal prompting the user to reconnect a controller.
/// 
/// Automatically dismisses when a gamepad is reconnected or any controller button is pressed.
/// In PC/Editor environments, also allows seamless fallback to Keyboard/Mouse by clicking or pressing Space.
/// </summary>
public class ControllerDisconnectHandler : MonoBehaviour
{
    public static ControllerDisconnectHandler Instance { get; private set; }

    [Header("UI References (Optional)")]
    [SerializeField] private GameObject disconnectOverlayRoot;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI messageText;

    private bool isOverlayVisible = false;
    private bool wasGamepadRecentlyUsed = false;
    private Canvas dynamicCanvas;
    private GameObject dynamicOverlayGO;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        EnsureExists();
    }

    public static ControllerDisconnectHandler EnsureExists()
    {
        if (Instance != null)
            return Instance;

        GameObject go = new GameObject("[ControllerDisconnectHandler]");
        if (Application.isPlaying)
            DontDestroyOnLoad(go);
        Instance = go.AddComponent<ControllerDisconnectHandler>();
        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            if (Application.isPlaying)
                Destroy(gameObject);
            else
                DestroyImmediate(gameObject);
            return;
        }
        Instance = this;
        if (Application.isPlaying)
            DontDestroyOnLoad(gameObject);

        // Pre-check if any gamepad is already connected
        if (Gamepad.all.Count > 0)
        {
            wasGamepadRecentlyUsed = true;
        }
    }

    private void OnEnable()
    {
        InputSystem.onDeviceChange += OnDeviceChange;
        InputSystem.onActionChange += OnActionChange;
    }

    private void OnDisable()
    {
        InputSystem.onDeviceChange -= OnDeviceChange;
        InputSystem.onActionChange -= OnActionChange;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnActionChange(object obj, InputActionChange change)
    {
        if (change == InputActionChange.ActionPerformed)
        {
            if (obj is InputAction action && action.activeControl != null)
            {
                if (action.activeControl.device is Gamepad)
                {
                    wasGamepadRecentlyUsed = true;
                }
                else if (action.activeControl.device is Keyboard || action.activeControl.device is Mouse)
                {
                    // PC player switched to KBM
                    if (isOverlayVisible && !IsConsolePlatform())
                    {
                        HideDisconnectPrompt();
                    }
                }
            }
        }
    }

    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (device is Gamepad)
        {
            switch (change)
            {
                case InputDeviceChange.Disconnected:
                case InputDeviceChange.Removed:
                case InputDeviceChange.Disabled:
                    HandleGamepadDisconnected(device as Gamepad);
                    break;

                case InputDeviceChange.Reconnected:
                case InputDeviceChange.Added:
                case InputDeviceChange.Enabled:
                    HandleGamepadConnected(device as Gamepad);
                    break;
            }
        }
    }

    private void HandleGamepadDisconnected(Gamepad gamepad)
    {
        Debug.LogWarning($"[ControllerDisconnectHandler] Gamepad disconnected: {gamepad?.displayName ?? "Unknown"}");

        // If no gamepads remain connected, evaluate if prompt should appear
        if (Gamepad.all.Count == 0 || (Gamepad.current == null || !Gamepad.current.added))
        {
            bool shouldPrompt = IsConsolePlatform() || wasGamepadRecentlyUsed;
            if (shouldPrompt)
            {
                ShowDisconnectPrompt();
            }
        }
    }

    private void HandleGamepadConnected(Gamepad gamepad)
    {
        Debug.Log($"[ControllerDisconnectHandler] Gamepad reconnected/added: {gamepad?.displayName ?? "Unknown"}");
        wasGamepadRecentlyUsed = true;

        if (isOverlayVisible)
        {
            HideDisconnectPrompt();
        }
    }

    private void Update()
    {
        if (isOverlayVisible)
        {
            // Auto-dismiss if any button is pressed on any connected gamepad
            if (Gamepad.current != null && Gamepad.current.added)
            {
                if (Gamepad.current.wasUpdatedThisFrame)
                {
                    HideDisconnectPrompt();
                    return;
                }
            }

            // In PC / Standalone / Editor, allow keyboard or mouse click to dismiss
            if (!IsConsolePlatform())
            {
                if (Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame ||
                                                 Keyboard.current.enterKey.wasPressedThisFrame ||
                                                 Keyboard.current.escapeKey.wasPressedThisFrame))
                {
                    wasGamepadRecentlyUsed = false;
                    HideDisconnectPrompt();
                    return;
                }

                if (Mouse.current != null && (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame))
                {
                    wasGamepadRecentlyUsed = false;
                    HideDisconnectPrompt();
                    return;
                }
            }
        }
    }

    private bool IsConsolePlatform()
    {
        return Application.platform == RuntimePlatform.GameCoreXboxSeries ||
               Application.platform == RuntimePlatform.GameCoreXboxOne ||
               (CrossPlatformManager.Instance != null && CrossPlatformManager.Instance.CurrentPlatform == PlatformType.Xbox);
    }

    /// <summary>
    /// Displays the controller disconnect prompt.
    /// </summary>
    public void ShowDisconnectPrompt()
    {
        if (isOverlayVisible) return;
        isOverlayVisible = true;

        if (disconnectOverlayRoot != null)
        {
            disconnectOverlayRoot.SetActive(true);
        }
        else
        {
            EnsureDynamicOverlay();
            if (dynamicOverlayGO != null)
                dynamicOverlayGO.SetActive(true);
        }

        Debug.Log("[ControllerDisconnectHandler] Controller disconnect prompt displayed (XR-011/XR-015 compliant).");
    }

    /// <summary>
    /// Hides the controller disconnect prompt.
    /// </summary>
    public void HideDisconnectPrompt()
    {
        if (!isOverlayVisible) return;
        isOverlayVisible = false;

        if (disconnectOverlayRoot != null)
        {
            disconnectOverlayRoot.SetActive(false);
        }

        if (dynamicOverlayGO != null)
        {
            dynamicOverlayGO.SetActive(false);
        }

        Debug.Log("[ControllerDisconnectHandler] Controller disconnect prompt dismissed.");
    }

    private void EnsureDynamicOverlay()
    {
        if (dynamicOverlayGO != null)
            return;

        // Create dedicated root canvas with highest sorting order
        GameObject canvasGO = new GameObject("[ControllerDisconnectCanvas]");
        if (Application.isPlaying)
            DontDestroyOnLoad(canvasGO);

        dynamicCanvas = canvasGO.AddComponent<Canvas>();
        dynamicCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        dynamicCanvas.sortingOrder = 32000; // Above all menus, pause screens, and HUDs

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        // Backdrop panel
        dynamicOverlayGO = new GameObject("DisconnectBackdrop", typeof(RectTransform), typeof(Image));
        dynamicOverlayGO.transform.SetParent(canvasGO.transform, false);

        RectTransform bgRT = dynamicOverlayGO.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.sizeDelta = Vector2.zero;

        Image bgImg = dynamicOverlayGO.GetComponent<Image>();
        bgImg.color = new Color(0f, 0f, 0f, 0.88f);
        bgImg.raycastTarget = true;

        // Modal Card
        GameObject cardGO = new GameObject("ModalCard", typeof(RectTransform), typeof(Image));
        cardGO.transform.SetParent(dynamicOverlayGO.transform, false);

        RectTransform cardRT = cardGO.GetComponent<RectTransform>();
        cardRT.anchorMin = new Vector2(0.5f, 0.5f);
        cardRT.anchorMax = new Vector2(0.5f, 0.5f);
        cardRT.pivot = new Vector2(0.5f, 0.5f);
        cardRT.sizeDelta = new Vector2(620f, 320f);

        Image cardImg = cardGO.GetComponent<Image>();
        cardImg.color = new Color(0.12f, 0.12f, 0.16f, 0.98f);

        // Icon + Title Text
        GameObject titleGO = new GameObject("TitleText", typeof(RectTransform));
        titleGO.transform.SetParent(cardGO.transform, false);
        RectTransform titleRT = titleGO.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0f, 1f);
        titleRT.anchorMax = new Vector2(1f, 1f);
        titleRT.pivot = new Vector2(0.5f, 1f);
        titleRT.anchoredPosition = new Vector2(0f, -28f);
        titleRT.sizeDelta = new Vector2(-40f, 50f);

        titleText = titleGO.AddComponent<TextMeshProUGUI>();
        titleText.text = "⚠️  CONTROLLER DISCONNECTED";
        titleText.fontSize = 24f;
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = new Color(1f, 0.82f, 0.2f, 1f);
        titleText.alignment = TextAlignmentOptions.Center;

        // Message Body Text
        GameObject msgGO = new GameObject("MessageText", typeof(RectTransform));
        msgGO.transform.SetParent(cardGO.transform, false);
        RectTransform msgRT = msgGO.GetComponent<RectTransform>();
        msgRT.anchorMin = new Vector2(0f, 0f);
        msgRT.anchorMax = new Vector2(1f, 1f);
        msgRT.pivot = new Vector2(0.5f, 0.5f);
        msgRT.anchoredPosition = new Vector2(0f, -10f);
        msgRT.sizeDelta = new Vector2(-60f, -100f);

        messageText = msgGO.AddComponent<TextMeshProUGUI>();
        string subPrompt = IsConsolePlatform()
            ? "Please reconnect your wireless controller or press any button on a connected controller to resume playing."
            : "Please reconnect your wireless controller to resume playing.\n\n<color=#A0A5B5><i>Or click anywhere / press [Space] to continue with Mouse & Keyboard.</i></color>";
        messageText.text = subPrompt;
        messageText.fontSize = 17f;
        messageText.color = Color.white;
        messageText.alignment = TextAlignmentOptions.Center;
        messageText.richText = true;

        dynamicOverlayGO.SetActive(false);
    }
}
