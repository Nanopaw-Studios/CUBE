using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class DebugHideUI : MonoBehaviour
{
    private GameObject canvas;

    void Awake()
    {
        // Ensure this persists across scenes
        DontDestroyOnLoad(gameObject);
    }

    void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Find the canvas in the new scene
        canvas = null;
        GameObject[] rootObjects = scene.GetRootGameObjects();
        foreach (GameObject obj in rootObjects)
        {
            if (obj.name == "Canvas")
            {
                canvas = obj;
                break; 
            }
        }
    }

    void Update()
    {
        // Null check avoids errors if a scene has no canvas
        if (Keyboard.current?.f1Key.wasPressedThisFrame == true && canvas != null)
        {
            canvas.SetActive(!canvas.activeSelf);
        }
    }
}
