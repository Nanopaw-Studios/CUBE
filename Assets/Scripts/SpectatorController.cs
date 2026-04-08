using System.Collections.Generic;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Handles spectating of other players when the local player is dead during an active round.
/// Attach to the local player's root GameObject (the one with PhotonView + CharacterInput, etc.).
/// </summary>
public class SpectatorController : MonoBehaviour
{
    // ── Inspector ──────────────────────────────────────────────────────────────

    [Header("References")]
    [Tooltip("The InputActionAsset from Assets/Settings.")]
    public InputActionAsset inputActions;

    [Tooltip("The player stats used to determine if this player is dead.")]
    public BasePlayerStats playerStats;

    [Tooltip("The RoundManager in the scene.")]
    public RoundManager roundManager;

    [Header("Spectate UI")]
    [Tooltip("Root panel of the spectate UI (Canvas/Specate).")]
    public GameObject spectateUIRoot;

    [Tooltip("TMP text that shows 'Spectating: <name>'.")]
    public TMP_Text spectatingText;

    [Header("Orbit Settings")]
    [Tooltip("Distance from the spectated player's hip/center.")]
    public float orbitDistance = 5f;

    [Tooltip("Vertical offset from the spectated player's position.")]
    public float orbitHeightOffset = 1.5f;

    [Tooltip("Mouse orbit sensitivity.")]
    public float mouseSensitivity = 2f;

    [Tooltip("Gamepad right-stick orbit sensitivity.")]
    public float gamepadSensitivity = 80f;

    [Tooltip("Minimum/maximum vertical angle in degrees.")]
    public float minPitch = -30f;
    public float maxPitch = 70f;

    [Tooltip("How quickly the orbit camera interpolates to the target position.")]
    public float orbitLerpSpeed = 10f;

    // ── State ──────────────────────────────────────────────────────────────────

    private bool isSpectating = false;

    // Index into the current list of spectatable players
    private int spectateIndex = 0;

    // All non-local PhotonViews that represent active players
    private readonly List<GameObject> spectateTargets = new List<GameObject>();

    // Current orbiting angles
    private float orbitYaw = 0f;
    private float orbitPitch = 20f;

    // Input actions
    private InputAction specateAction;
    private InputAction lookAction;
    private InputAction moveAction;

    // Cooldown to prevent rapid target-switching from held input
    private const float SwitchCooldown = 0.3f;
    private float switchCooldownTimer = 0f;

    // Cache the camera and CameraInput so we can disable/enable it
    private Camera mainCamera;
    private CameraInput cameraInput;

    // Track components to disable during spectating
    private CharacterInput characterInput;

    // ── Unity Lifecycle ────────────────────────────────────────────────────────

    private void Start()
    {
        // Only run spectator logic on the local player
        PhotonView pv = GetComponent<PhotonView>();
        if (pv != null && !pv.IsMine)
        {
            enabled = false;
            return;
        }

        ResolveInputActions();

        mainCamera = Camera.main;
        if (mainCamera != null)
        {
            cameraInput = mainCamera.transform.parent != null
                ? mainCamera.transform.parent.GetComponent<CameraInput>()
                : mainCamera.GetComponent<CameraInput>();
        }

        characterInput = GetComponent<CharacterInput>();

        // Start hidden
        SetSpectateUIVisible(false);
    }

    private void OnDestroy()
    {
        UnbindInputActions();
    }

    private void Update()
    {
        if (switchCooldownTimer > 0f)
            switchCooldownTimer -= Time.deltaTime;

        if (!isSpectating) return;

        RefreshTargetList();
        HandleOrbitInput();
        MoveCamera();
        HandleSwitchInput();
    }

    // ── Input Setup ────────────────────────────────────────────────────────────

    private void ResolveInputActions()
    {
        if (inputActions == null)
        {
            Debug.LogError("[SpectatorController] inputActions is not assigned.");
            return;
        }

        specateAction = inputActions.FindAction("Player/Specate", false);
        lookAction    = inputActions.FindAction("Player/Look",    false);
        moveAction    = inputActions.FindAction("Player/Move",    false);

        if (specateAction == null)
        {
            Debug.LogError("[SpectatorController] 'Specate' action not found in InputSystem_Actions.");
            return;
        }

        specateAction.performed += OnSpecatePerformed;
    }

    private void UnbindInputActions()
    {
        if (specateAction != null)
            specateAction.performed -= OnSpecatePerformed;
    }

    // ── Spectate Toggle ────────────────────────────────────────────────────────

    private void OnSpecatePerformed(InputAction.CallbackContext ctx)
    {
        if (!CanSpectate()) return;

        if (!isSpectating)
            BeginSpectating();
        else
            EndSpectating();
    }

    /// <summary>
    /// Returns true when the local player may enter spectate mode:
    /// the round must be active and the player must be dead (health at 0).
    /// </summary>
    private bool CanSpectate()
    {
        if (roundManager == null) return false;
        if (!roundManager.roundActive) return false;
        if (playerStats == null) return false;
        return playerStats.health <= 0;
    }

    private void BeginSpectating()
    {
        RefreshTargetList();
        if (spectateTargets.Count == 0)
        {
            Debug.Log("[SpectatorController] No players to spectate.");
            return;
        }

        isSpectating = true;
        spectateIndex = 0;

        // Seed orbit angles from the current camera orientation so there's no jump
        if (mainCamera != null)
        {
            Vector3 euler = mainCamera.transform.eulerAngles;
            orbitYaw   = euler.y;
            orbitPitch = euler.x > 180f ? euler.x - 360f : euler.x;
        }

        // Disable the normal camera input so spectate takes over
        SetNormalCameraEnabled(false);

        SetSpectateUIVisible(true);
        UpdateSpectateUI();
    }

    private void EndSpectating()
    {
        isSpectating = false;

        // Re-enable normal camera input
        SetNormalCameraEnabled(true);

        SetSpectateUIVisible(false);
    }

    // ── Camera Orbit ───────────────────────────────────────────────────────────

    private void HandleOrbitInput()
    {
        if (lookAction == null) return;

        Vector2 raw = lookAction.ReadValue<Vector2>();

        bool isGamepad = lookAction.activeControl != null &&
                         lookAction.activeControl.device is Gamepad;

        float sensitivity = isGamepad ? gamepadSensitivity * Time.deltaTime : mouseSensitivity * 0.1f;

        orbitYaw   += raw.x * sensitivity;
        orbitPitch -= raw.y * sensitivity;
        orbitPitch  = Mathf.Clamp(orbitPitch, minPitch, maxPitch);
    }

    private void MoveCamera()
    {
        if (mainCamera == null) return;

        // Get the current target (may be null if all players left)
        GameObject target = GetCurrentTarget();
        if (target == null)
        {
            EndSpectating();
            return;
        }

        // Orbit position calculation
        Quaternion rotation = Quaternion.Euler(orbitPitch, orbitYaw, 0f);
        Vector3 pivotPos    = target.transform.position + Vector3.up * orbitHeightOffset;
        Vector3 desiredPos  = pivotPos - rotation * Vector3.forward * orbitDistance;

        // Smooth interpolation
        mainCamera.transform.position = Vector3.Lerp(
            mainCamera.transform.position,
            desiredPos,
            Time.deltaTime * orbitLerpSpeed
        );
        mainCamera.transform.LookAt(pivotPos);
    }

    // ── Target Switching ───────────────────────────────────────────────────────

    private void HandleSwitchInput()
    {
        if (moveAction == null || switchCooldownTimer > 0f) return;

        Vector2 input = moveAction.ReadValue<Vector2>();

        if (input.x > 0.5f)
        {
            CycleTarget(+1);
            switchCooldownTimer = SwitchCooldown;
        }
        else if (input.x < -0.5f)
        {
            CycleTarget(-1);
            switchCooldownTimer = SwitchCooldown;
        }
    }

    private void CycleTarget(int direction)
    {
        RefreshTargetList();
        if (spectateTargets.Count == 0) return;

        spectateIndex = (spectateIndex + direction + spectateTargets.Count) % spectateTargets.Count;
        UpdateSpectateUI();
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Rebuilds the list of spectatable players — all networked player GameObjects
    /// except the local player's own object.
    /// </summary>
    private void RefreshTargetList()
    {
        spectateTargets.Clear();

        foreach (GameObject go in GameObject.FindGameObjectsWithTag("Player"))
        {
            PhotonView pv = go.GetComponent<PhotonView>();
            if (pv == null || pv.IsMine) continue;

            // Optionally skip dead players (health <= 0) if you want to spectate only alive ones.
            // For now we allow spectating everyone to match the request.
            spectateTargets.Add(go);
        }

        // Clamp index in case players leave
        if (spectateTargets.Count > 0)
            spectateIndex = Mathf.Clamp(spectateIndex, 0, spectateTargets.Count - 1);
    }

    private GameObject GetCurrentTarget()
    {
        if (spectateTargets.Count == 0) return null;
        spectateIndex = Mathf.Clamp(spectateIndex, 0, spectateTargets.Count - 1);
        return spectateTargets[spectateIndex];
    }

    private void UpdateSpectateUI()
    {
        if (spectatingText == null) return;

        GameObject target = GetCurrentTarget();
        if (target == null)
        {
            spectatingText.text = "Spectating: —";
            return;
        }

        // Pull the Photon nickname from the PhotonView owner
        PhotonView pv = target.GetComponent<PhotonView>();
        string playerName = pv != null && pv.Owner != null ? pv.Owner.NickName : target.name;

        spectatingText.text = $"Spectating: {playerName}";
    }

    private void SetSpectateUIVisible(bool visible)
    {
        if (spectateUIRoot != null)
            spectateUIRoot.SetActive(visible);
    }

    private void SetNormalCameraEnabled(bool enabled)
    {
        if (cameraInput != null)
            cameraInput.enabled = enabled;
    }
}
