using System;
using System.Collections;
using NUnit.Framework;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PauseMenu : MonoBehaviourPunCallbacks
{
    public static bool GameIsPaused = false;
    public InputActionReference pauseAction;

    public GameObject pauseMenu;
    public Button QuitButton;
    public LevelLoader loader;
    public TMP_Text roomCodeText;


    public UnityEvent onPause;
    public UnityEvent onResume;

    private Animator pauseMenuAnimator;
    private RoundManager roundManager;

    private bool isLeavingRoom = false;

    [Header("Dev Physics")]
    private GameObject devPhysicsButtonGO;
    private GameObject devPhysicsModalGO;

    [Header("Player Safety / Moderation")]
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button playersButton;
    [SerializeField] private PlayerModerationUI moderationUI;

    public override void OnEnable()
    {
        base.OnEnable();
        if (pauseAction != null)
            pauseAction.action.Enable();
    }

    public override void OnDisable()
    {
        base.OnDisable();
        if (pauseAction != null)
            pauseAction.action.Disable();
    }

    private void OnDestroy()
    {
        PlayfabManager.OnDeveloperStatusChanged -= OnDevStatusChanged;
    }

    void Start()
    {
        pauseMenuAnimator = GetComponent<Animator>();
        roundManager = FindFirstObjectByType<RoundManager>();

        if (resumeButton == null && pauseMenu != null)
        {
            Transform r = pauseMenu.transform.Find("List/Resume");
            if (r != null) resumeButton = r.GetComponent<Button>();
        }
        if (playersButton == null && pauseMenu != null)
        {
            Transform p = pauseMenu.transform.Find("List/Players");
            if (p != null) playersButton = p.GetComponent<Button>();
        }

        if (roomCodeText != null && PhotonNetwork.CurrentRoom != null)
        {
            if (!PhotonNetwork.CurrentRoom.IsVisible) // we know its a private room
            {
                roomCodeText.text = $"Private Room | {PhotonNetwork.CurrentRoom.Name}";
            }
            else
            {
                roomCodeText.text = $"Public Room | {PhotonNetwork.CurrentRoom.Name}";
            }
        }

        PlayfabManager.OnDeveloperStatusChanged += OnDevStatusChanged;
        if (PlayfabManager.IsDeveloper)
        {
            SetupDevOptions();
        }
        else if (PlayfabManager.Instance != null)
        {
            PlayfabManager.Instance.CheckDeveloperStatus(isDev =>
            {
                if (isDev)
                    SetupDevOptions();
            });
        }
    }

    private void OnDevStatusChanged(bool isDev)
    {
        if (isDev)
        {
            SetupDevOptions();
        }
        else
        {
            if (devPhysicsButtonGO != null)
                devPhysicsButtonGO.SetActive(false);
            if (devPhysicsModalGO != null)
                devPhysicsModalGO.SetActive(false);
        }
    }

    private void SetupDevOptions()
    {
        if (devPhysicsButtonGO != null)
        {
            devPhysicsButtonGO.SetActive(true);
            return;
        }

        if (QuitButton == null)
            return;

        // 1. Clone QuitButton as the Secondary Physics button
        devPhysicsButtonGO = Instantiate(QuitButton.gameObject, QuitButton.transform.parent);
        devPhysicsButtonGO.name = "DevPhysicsButton";
        // Place it right before Quit button
        devPhysicsButtonGO.transform.SetSiblingIndex(QuitButton.transform.GetSiblingIndex());

        // Update Text
        TextMeshProUGUI btnText = devPhysicsButtonGO.GetComponentInChildren<TextMeshProUGUI>(true);
        if (btnText != null)
        {
            btnText.text = "★ Dev Physics";
            btnText.color = new Color(1f, 0.85f, 0.25f, 1f); // subtle golden tint
            btnText.fontSize = 17f;
            btnText.alignment = TextAlignmentOptions.Center;
            btnText.raycastTarget = false;
        }

        // Recreate the Button component so it inherits ZERO persistent listeners from QuitButton
        Button oldBtn = devPhysicsButtonGO.GetComponent<Button>();
        if (oldBtn != null)
        {
            var colors = oldBtn.colors;
            var spriteState = oldBtn.spriteState;
            var navigation = oldBtn.navigation;
            var transition = oldBtn.transition;

            DestroyImmediate(oldBtn);

            Button devBtn = devPhysicsButtonGO.AddComponent<Button>();
            devBtn.colors = colors;
            devBtn.spriteState = spriteState;
            devBtn.navigation = navigation;
            devBtn.transition = transition;

            Image devImg = devPhysicsButtonGO.GetComponent<Image>();
            if (devImg != null)
            {
                devImg.raycastTarget = true;
                devBtn.targetGraphic = devImg;
            }

            devBtn.interactable = true;
            devBtn.onClick.AddListener(OpenDevPhysicsModal);
        }

        // 2. Pre-build the modal under pauseMenu root
        Transform modalParent = pauseMenu != null ? pauseMenu.transform : transform;
        GameObject templateSlider = null;
        if (pauseMenu != null)
        {
            Transform t = pauseMenu.transform.Find("Settings/Master Volume");
            if (t != null) templateSlider = t.gameObject;
        }

        devPhysicsModalGO = DevSettingsUIBuilder.BuildDevPhysicsModal(modalParent, QuitButton.gameObject, templateSlider);
        if (devPhysicsModalGO != null)
            devPhysicsModalGO.SetActive(false);
    }

    public void OpenDevPhysicsModal()
    {
        if (devPhysicsModalGO == null)
        {
            Transform modalParent = pauseMenu != null ? pauseMenu.transform : transform;
            GameObject templateSlider = null;
            if (pauseMenu != null)
            {
                Transform t = pauseMenu.transform.Find("Settings/Master Volume");
                if (t != null) templateSlider = t.gameObject;
            }
            devPhysicsModalGO = DevSettingsUIBuilder.BuildDevPhysicsModal(modalParent, QuitButton != null ? QuitButton.gameObject : null, templateSlider);
        }

        if (devPhysicsModalGO != null)
        {
            devPhysicsModalGO.SetActive(true);
            devPhysicsModalGO.transform.SetAsLastSibling();
            Selectable firstSel = devPhysicsModalGO.GetComponentInChildren<Selectable>();
            if (firstSel != null)
                SetFocus(firstSel.gameObject);
        }
    }

    public void OpenPlayersModal()
    {
        if (moderationUI != null)
        {
            moderationUI.OpenDialog();
        }
        else
        {
            Transform modalParent = pauseMenu != null ? pauseMenu.transform : transform;
            moderationUI = PlayerModerationUI.Open(modalParent, QuitButton != null ? QuitButton.gameObject : null);
        }
    }

    public void ClosePlayersModal()
    {
        if (moderationUI != null)
            moderationUI.CloseDialog();
        else
            PlayerModerationUI.Close();

        SetFocusToPlayersButton();
    }

    public void SetFocus(GameObject target)
    {
        StartCoroutine(SelectDelayed(target));
    }

    public void SetFocusToPlayersButton()
    {
        if (playersButton != null)
            SetFocus(playersButton.gameObject);
    }

    private IEnumerator SelectDelayed(GameObject target)
    {
        yield return null;
        if (target != null && target.activeInHierarchy && UnityEngine.EventSystems.EventSystem.current != null)
        {
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(target);
        }
    }

    void Update()
    {
        bool pausePressed = pauseAction != null && pauseAction.action.WasPressedThisFrame();
        bool cancelPressed = false;
        if (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame)
            cancelPressed = true;
        else if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            cancelPressed = true;

        if (pausePressed)
        {
            if (PlayerModerationUI.IsOpen)
            {
                ClosePlayersModal();
            }
            else if (devPhysicsModalGO != null && devPhysicsModalGO.activeSelf)
            {
                devPhysicsModalGO.SetActive(false);
                if (devPhysicsButtonGO != null)
                    SetFocus(devPhysicsButtonGO);
            }
            else
            {
                PauseStateChange();
            }
        }
        else if (cancelPressed && GameIsPaused)
        {
            if (PlayerModerationUI.IsOpen)
            {
                ClosePlayersModal();
            }
            else if (devPhysicsModalGO != null && devPhysicsModalGO.activeSelf)
            {
                devPhysicsModalGO.SetActive(false);
                if (devPhysicsButtonGO != null)
                    SetFocus(devPhysicsButtonGO);
            }
            else
            {
                PauseStateChange();
            }
        }

        if (QuitButton != null && roundManager != null)
            QuitButton.interactable = roundManager.intermissionActive;
    }

    public void PauseStateChange()
    {
        GameIsPaused = !GameIsPaused;

        ShopManager shopManager = FindFirstObjectByType<ShopManager>();

        if (GameIsPaused)
        {
            onPause.Invoke();
            pauseMenu.SetActive(true);
            pauseMenuAnimator.Play("PauseFadeIn");
            CursorManager.SetState(CursorManager.CursorState.UI);

            if (shopManager != null)
                shopManager.enabled = false;

            GameObject focusTarget = resumeButton != null ? resumeButton.gameObject : (QuitButton != null ? QuitButton.gameObject : null);
            SetFocus(focusTarget);
        }
        else
        {
            if (UnityEngine.EventSystems.EventSystem.current != null)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);

            if (devPhysicsModalGO != null)
                devPhysicsModalGO.SetActive(false);
            if (PlayerModerationUI.IsOpen)
                PlayerModerationUI.Close();

            onResume.Invoke();
            pauseMenuAnimator.Play("PauseFadeOut");
            CursorManager.SetState(CursorManager.CursorState.Gameplay);
            Cursor.visible = false;

            if (shopManager != null)
                shopManager.enabled = true;

            StartCoroutine(DisableAfterAnimation());
        }
    }

    IEnumerator DisableAfterAnimation()
    {
        yield return new WaitForSeconds(0.3f);
        if (devPhysicsModalGO != null)
            devPhysicsModalGO.SetActive(false);
        if (PlayerModerationUI.IsOpen)
            PlayerModerationUI.Close();
        pauseMenu.SetActive(false);
    }

    public void Resume()
    {
        PauseStateChange();
    }

    /// <summary>
    /// Leaves the current Photon room and returns to the main menu.
    /// OnLeftRoom fires before the room is fully torn down, which is safe for scene loads.
    /// If not in a room, loads the menu directly.
    /// </summary>
    public void Quit()
    {
        if (isLeavingRoom)
            return;
        isLeavingRoom = true;

        GameIsPaused = false;
        PlayerReportService.ResetSession();

        if (PhotonNetwork.NetworkClientState == Photon.Realtime.ClientState.Joined)
        {
            PhotonNetwork.LeaveRoom();
        }
        else
        {
            loader.LoadLevel("Menu");
        }
    }

    /// <summary>
    /// Photon fires this on the local client after the room has been left cleanly.
    /// At this point we are back on the Master Server and can safely load a new scene.
    /// </summary>
    public override void OnLeftRoom()
    {
        PlayerReportService.ResetSession();
        loader.LoadLevel("Menu");
    }

    public void OnRoomCodeCopyClicked()
    {
        ClipBoard = PhotonNetwork.CurrentRoom.Name;
    }

    /// <summary>
    /// Get or set the OS clipboard contents (plain text).
    /// </summary>
    public static string ClipBoard
    {
        get
        {
            try
            {
                return GUIUtility.systemCopyBuffer;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Clipboard get failed: {e.Message}");
                return string.Empty;
            }
        }
        set
        {
            try
            {
                GUIUtility.systemCopyBuffer = value;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Clipboard set failed: {e.Message}");
            }
        }
    }
}
