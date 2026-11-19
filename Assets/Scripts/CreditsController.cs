using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class CreditsController : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Target scene to load upon exit or completion")]
    public string returnSceneName = "Menu";

    [Tooltip("Total duration of credits animation in seconds")]
    public float duration = 39f;

    [Tooltip("Reference to optional Animator")]
    public Animator creditsAnimator;

    private float _timer = 0f;
    private bool _hasTriggered = false;

    private void Start()
    {
        // Ensure cursor is visible and unlocked
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        if (creditsAnimator == null)
        {
            creditsAnimator = GetComponentInChildren<Animator>();
        }

        // Auto-detect duration from clip if available
        if (creditsAnimator != null && creditsAnimator.runtimeAnimatorController != null)
        {
            var clips = creditsAnimator.runtimeAnimatorController.animationClips;
            if (clips != null && clips.Length > 0 && clips[0].length > 0)
            {
                duration = clips[0].length;
            }
        }
    }

    private void Update()
    {
        if (_hasTriggered) return;

        _timer += Time.deltaTime;

        // Check if credits finished playing
        if (_timer >= duration)
        {
            ReturnToMenu();
            return;
        }

        // Check for Escape / Back / Cancel input
        if (IsExitRequested())
        {
            ReturnToMenu();
        }
    }

    private bool IsExitRequested()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            return true;
        }

        if (Gamepad.current != null)
        {
            if (Gamepad.current.buttonEast.wasPressedThisFrame ||
                Gamepad.current.startButton.wasPressedThisFrame ||
                Gamepad.current.selectButton.wasPressedThisFrame)
            {
                return true;
            }
        }

        return false;
    }

    public void ReturnToMenu()
    {
        if (_hasTriggered) return;
        _hasTriggered = true;

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        SceneManager.LoadScene(returnSceneName);
    }
}
