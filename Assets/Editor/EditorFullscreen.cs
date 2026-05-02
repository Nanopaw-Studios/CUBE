using UnityEditor; using UnityEngine;

public class TrueFullscreen 
    {
        [MenuItem("Window/True Fullscreen _F11")]    
        static void Toggle()
        {         
            // This toggles the 'maximized' state of the currently focused window
            EditorWindow.focusedWindow.maximized = !EditorWindow.focusedWindow.maximized;
        }
    } 