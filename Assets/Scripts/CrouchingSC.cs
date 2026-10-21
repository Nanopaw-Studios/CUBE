using System.Collections;
using Photon.Pun;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controls the crouching state for the active ragdoll player.
/// Lowers the ragdoll stance by adjusting StandingSC.heightOffset and
/// slows movement speed by applying a multiplier to MovementSC.Force.
/// </summary>
public class CrouchingSC : MonoBehaviour
{
    private Rig data;
    private MovementSC movement;
    private StandingSC standing;
    private SprintingSC sprinting;
    private BasePlayerStats playerStats;
    private CharacterInput characterInput;
    private PlayerInput playerInput;
    private PhotonView photonView;

    [Header("Crouch State")]
    [Tooltip("Is the player currently in a crouched state?")]
    public bool isCrouched = false;

    [Tooltip("Movement speed multiplier applied while crouched (e.g. 0.5 = 50% speed).")]
    [Range(0.1f, 1f)]
    public float crouchSpeedMultiplier = 0.5f;

    [Header("Physics Stance")]
    [Tooltip("Offset applied to StandingSC.heightOffset to compress the stance down into a crouch.")]
    [Range(0f, 1.5f)]
    public float crouchHeightOffset = 0.55f;

    [Tooltip("Speed at which the ragdoll transitions into and out of crouch (units/second).")]
    public float crouchTransitionSpeed = 2.5f;

    [Header("Controls")]
    [Tooltip("If true, pressing crouch toggles crouch on/off. If false, crouch must be held.")]
    public bool toggleCrouch = false;

    [Tooltip("Optional direct reference to the Crouch InputAction.")]
    public InputActionReference crouchAction;

    private InputAction resolvedAction;
    private float preCrouchForce;

    private void Awake()
    {
        photonView = GetComponentInParent<PhotonView>();
        if (photonView != null && !photonView.IsMine)
        {
            enabled = false;
            return;
        }
    }

    private void Start()
    {
        movement = GetComponent<MovementSC>();
        if (movement == null)
            Debug.LogError("[CrouchingSC] MovementSC component not found on GameObject.", this);

        data = GetComponent<Rig>();
        if (data == null)
            Debug.LogError("[CrouchingSC] Rig component not found on GameObject.", this);

        standing = GetComponent<StandingSC>();
        if (standing == null)
            Debug.LogError("[CrouchingSC] StandingSC component not found on GameObject.", this);

        sprinting = GetComponent<SprintingSC>();
        characterInput = GetComponent<CharacterInput>();
        playerInput = GetComponent<PlayerInput>();
        playerStats = GetComponentInParent<BasePlayerStats>();

        ResolveCrouchAction();
    }

    private void ResolveCrouchAction()
    {
        if (crouchAction != null && crouchAction.action != null)
        {
            resolvedAction = crouchAction.action;
        }
        else if (playerInput != null && playerInput.actions != null)
        {
            resolvedAction = playerInput.actions.FindAction("Player/Crouch", false)
                          ?? playerInput.actions.FindAction("Crouch", false);
        }

        if (resolvedAction != null)
        {
            resolvedAction.Enable();
        }
    }

    private void OnEnable()
    {
        if (photonView != null && !photonView.IsMine)
        {
            enabled = false;
            return;
        }

        if (resolvedAction != null)
        {
            resolvedAction.Enable();
        }
    }

    private void OnDisable()
    {
        if (isCrouched)
        {
            ResetCrouchState();
        }
    }

    private void Update()
    {
        // Guard against remote player instances
        if (photonView != null && !photonView.IsMine)
            return;

        // Ensure action is resolved if PlayerInput was initialized after Start
        if (resolvedAction == null && playerInput != null && playerInput.actions != null)
        {
            ResolveCrouchAction();
        }

        // Prevent crouching if ragdolled or dead
        if ((data != null && data.Ragdolling) || (playerStats != null && playerStats.health <= 0))
        {
            if (isCrouched)
                SetCrouchState(false);
            return;
        }

        bool inputHeld = false;
        bool inputTriggeredThisFrame = false;

        // 1. Check resolved InputAction (InputSystem)
        if (resolvedAction != null && resolvedAction.enabled)
        {
            inputHeld = resolvedAction.IsPressed();
            inputTriggeredThisFrame = resolvedAction.WasPressedThisFrame();
        }

        // 2. Direct Left Control keyboard fallback
        if (Keyboard.current != null)
        {
            if (Keyboard.current.leftCtrlKey.isPressed)
                inputHeld = true;
            if (Keyboard.current.leftCtrlKey.wasPressedThisFrame)
                inputTriggeredThisFrame = true;
        }

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKey(KeyCode.LeftControl))
            inputHeld = true;
        if (Input.GetKeyDown(KeyCode.LeftControl))
            inputTriggeredThisFrame = true;
#endif

        if (toggleCrouch)
        {
            if (inputTriggeredThisFrame)
            {
                SetCrouchState(!isCrouched);
            }
        }
        else
        {
            if (inputHeld != isCrouched)
            {
                SetCrouchState(inputHeld);
            }
        }
    }

    private void FixedUpdate()
    {
        // Smoothly transition standing height offset to settle the ragdoll into/out of crouch
        if (standing != null)
        {
            float targetOffset = isCrouched ? crouchHeightOffset : 0f;
            standing.heightOffset = Mathf.MoveTowards(
                standing.heightOffset,
                targetOffset,
                crouchTransitionSpeed * Time.fixedDeltaTime
            );
        }
    }

    /// <summary>
    /// Changes the crouching state, adjusting speed and canceling sprint if necessary.
    /// </summary>
    public void SetCrouchState(bool active)
    {
        if (isCrouched == active)
            return;

        isCrouched = active;

        if (isCrouched)
        {
            // Cancel active sprint immediately
            if (sprinting != null && sprinting.isSprinting)
            {
                sprinting.ResetSprintState();
            }

            float baseSpeed = playerStats != null && playerStats.speed > 0
                ? playerStats.speed
                : (movement != null ? movement.Force : 35f);

            preCrouchForce = baseSpeed;

            if (movement != null)
            {
                movement.Force = baseSpeed * crouchSpeedMultiplier;
            }
        }
        else
        {
            float restoreSpeed = playerStats != null && playerStats.speed > 0
                ? playerStats.speed
                : (preCrouchForce > 0 ? preCrouchForce : 35f);

            if (movement != null)
            {
                movement.Force = restoreSpeed;
            }
        }
    }

    /// <summary>
    /// Completely resets crouch state and restores normal standing height and speed.
    /// </summary>
    public void ResetCrouchState()
    {
        isCrouched = false;

        if (standing != null)
        {
            standing.heightOffset = 0f;
        }

        float defaultSpeed = playerStats != null && playerStats.speed > 0
            ? playerStats.speed
            : (preCrouchForce > 0 ? preCrouchForce : 35f);

        if (movement != null)
        {
            movement.Force = defaultSpeed;
        }
    }
}
