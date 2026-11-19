using Photon.Pun;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Monitors local player input while inside a networked match and kicks the player
/// back to the Main Menu after 10 minutes (600 seconds) of inactivity.
/// </summary>
public class AfkProtectionManager : MonoBehaviourPunCallbacks
{
    public static AfkProtectionManager Instance { get; private set; }

    [Tooltip("Inactivity timeout in seconds before kicking the player from the room (default 600s = 10 minutes).")]
    public float afkTimeoutSeconds = 600f;

    [Tooltip("Seconds of inactivity before showing an on-screen warning (default 540s = 9 minutes).")]
    public float afkWarningSeconds = 540f;

    private float _idleTimer = 0f;
    private bool _warningShown = false;
    private bool _kickingForAfk = false;

    public float CurrentIdleSeconds => _idleTimer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoBootstrap()
    {
        EnsureExists();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    public static AfkProtectionManager EnsureExists()
    {
        if (Instance != null)
            return Instance;

        AfkProtectionManager existing = FindFirstObjectByType<AfkProtectionManager>();
        if (existing != null)
        {
            Instance = existing;
            return Instance;
        }

        GameObject servicesGO = GameObject.Find("[Services]");
        if (servicesGO != null)
        {
            Instance = servicesGO.AddComponent<AfkProtectionManager>();
            return Instance;
        }

        GameObject go = new GameObject("[AfkProtectionManager]");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<AfkProtectionManager>();
        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        if (transform.parent == null)
            DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public override void OnJoinedRoom()
    {
        ResetIdleTimer();
        _kickingForAfk = false;
    }

    public override void OnLeftRoom()
    {
        ResetIdleTimer();
        if (_kickingForAfk)
        {
            _kickingForAfk = false;
            LoadMainMenuAfterAfkKick();
        }
    }

    public void ResetIdleTimer()
    {
        _idleTimer = 0f;
        _warningShown = false;
    }

    private void Update()
    {
        if (!PhotonNetwork.InRoom || _kickingForAfk)
        {
            _idleTimer = 0f;
            _warningShown = false;
            return;
        }

        if (HasUserInputThisFrame())
        {
            _idleTimer = 0f;
            _warningShown = false;
            return;
        }

        _idleTimer += Time.unscaledDeltaTime;

        if (!_warningShown && _idleTimer >= afkWarningSeconds)
        {
            _warningShown = true;
            int remaining = Mathf.Max(1, Mathf.RoundToInt(afkTimeoutSeconds - _idleTimer));
            CUBEStatusText.CreateText(
                $"<color=orange><b>AFK WARNING:</b> Move or press any input within {remaining}s or you will be kicked for inactivity.</color>"
            );
            Debug.Log($"[AfkProtection] Player inactive for {_idleTimer:F0}s — warning displayed.");
        }

        if (_idleTimer >= afkTimeoutSeconds)
        {
            KickLocalPlayerForAfk();
        }
    }

    private static bool HasUserInputThisFrame()
    {
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
            return true;

        if (Mouse.current != null)
        {
            if (
                Mouse.current.leftButton.wasPressedThisFrame
                || Mouse.current.rightButton.wasPressedThisFrame
                || Mouse.current.middleButton.wasPressedThisFrame
            )
            {
                return true;
            }

            if (Mouse.current.delta.ReadValue().sqrMagnitude > 4f)
                return true;

            if (Mouse.current.scroll.ReadValue().sqrMagnitude > 0.01f)
                return true;
        }

        if (Gamepad.current != null)
        {
            var gp = Gamepad.current;
            if (
                gp.buttonSouth.wasPressedThisFrame
                || gp.buttonNorth.wasPressedThisFrame
                || gp.buttonEast.wasPressedThisFrame
                || gp.buttonWest.wasPressedThisFrame
                || gp.startButton.wasPressedThisFrame
                || gp.selectButton.wasPressedThisFrame
                || gp.leftShoulder.wasPressedThisFrame
                || gp.rightShoulder.wasPressedThisFrame
                || gp.leftTrigger.ReadValue() > 0.2f
                || gp.rightTrigger.ReadValue() > 0.2f
                || gp.leftStick.ReadValue().sqrMagnitude > 0.04f
                || gp.rightStick.ReadValue().sqrMagnitude > 0.04f
                || gp.dpad.ReadValue().sqrMagnitude > 0.04f
            )
            {
                return true;
            }
        }

        return false;
    }

    public void KickLocalPlayerForAfk()
    {
        if (_kickingForAfk)
            return;

        _kickingForAfk = true;
        Debug.LogWarning("[AfkProtection] Local player reached 10-minute AFK timeout. Kicking from room.");

        PauseMenu.GameIsPaused = false;
        PlayerReportService.ResetSession();
        CursorManager.SetState(CursorManager.CursorState.UI);

        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
        }
        else
        {
            _kickingForAfk = false;
            LoadMainMenuAfterAfkKick();
        }
    }

    private static void LoadMainMenuAfterAfkKick()
    {
        if (SceneManager.GetActiveScene().name == "Menu")
            return;

        LevelLoader loader = FindFirstObjectByType<LevelLoader>();
        if (loader != null)
        {
            loader.LoadLevel("Menu");
        }
        else
        {
            SceneManager.LoadScene("Menu");
        }
    }
}
