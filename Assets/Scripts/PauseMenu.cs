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

    void Start()
    {
        pauseMenuAnimator = GetComponent<Animator>();
        roundManager = FindFirstObjectByType<RoundManager>();

        if(!PhotonNetwork.CurrentRoom.IsVisible) // we know its a private room
        {
            roomCodeText.text = $"Private Room | {PhotonNetwork.CurrentRoom.Name}";
        }
        else
        {
            roomCodeText.text = $"Public Room | {PhotonNetwork.CurrentRoom.Name}";
        }
    }

    void Update()
    {
        if (pauseAction != null && pauseAction.action.WasPressedThisFrame())
        {
            PauseStateChange();
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
        }
        else
        {
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
