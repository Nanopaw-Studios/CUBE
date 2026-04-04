using System.Collections;
using Photon.Pun;
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

    public UnityEvent onPause;
    public UnityEvent onResume;

    private Animator pauseMenuAnimator;
    private RoundManager roundManager;

    // MonoBehaviourPunCallbacks registers/deregisters Photon callbacks in
    // OnEnable/OnDisable via base. We must call base here so OnDisconnected fires.
    public override void OnEnable()
    {
        base.OnEnable();
        if (pauseAction != null) pauseAction.action.Enable();
    }

    public override void OnDisable()
    {
        base.OnDisable();
        if (pauseAction != null) pauseAction.action.Disable();
    }

    void Start()
    {
        pauseMenuAnimator = GetComponent<Animator>();
        roundManager = FindFirstObjectByType<RoundManager>();
    }    

    void Update()
    {
        if(pauseAction.action.WasPressedThisFrame()) // may cause spam issues when holding
        {  PauseStateChange();  }

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
        yield return new WaitForSeconds(0.3f); // match your animation length
        pauseMenu.SetActive(false);
    }

    public void Resume() {  PauseStateChange();  }

    public void Quit()
    {
        if (PhotonNetwork.InRoom)
        {
            // Disconnect first; LoadLevel is called from OnDisconnected
            // so the scene change doesn't happen while Photon is mid-teardown.
            PhotonNetwork.Disconnect();
        }
        else
        {
            loader.LoadLevel("Menu");
        }
    }

    public override void OnDisconnected(Photon.Realtime.DisconnectCause cause)
    {
        // Safe to load the menu now that Photon has fully disconnected.
        loader.LoadLevel("Menu");
    }
}