using System.IO;
using EasyButtons;
using UnityEngine;
using UnityEngine.Rendering.Universal;
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(Camera))]
public class SceneShot : MonoBehaviour
{
    private int i = 0;
    public int baseWidth = 1920;
    public int baseHeight = 1080;
    public int scaleFactor = 4;
    public string fileName = "sceneshot_";
    private Camera thisCamera;

    [Button]
    public void TakeSceneScreenShot()
    {
        AlignCamToSceneViewAndEnable();

        var cameraData = thisCamera.GetUniversalAdditionalCameraData();
        if (cameraData != null)
        {
            cameraData.renderPostProcessing = true;
            cameraData.volumeLayerMask = -1;
        }

        int resWidth = baseWidth * scaleFactor;
        int resHeight = baseHeight * scaleFactor;

        // 1. Capture in HDR/Linear for the Bloom math to work
        RenderTexture rt = RenderTexture.GetTemporary(
            resWidth,
            resHeight,
            24,
            RenderTextureFormat.DefaultHDR,
            RenderTextureReadWrite.Linear
        );
        thisCamera.targetTexture = rt;
        thisCamera.Render();

        // 2. Read pixels into a Linear Texture first
        Texture2D linearTex = new Texture2D(resWidth, resHeight, TextureFormat.RGBA32, false, true);
        RenderTexture.active = rt;
        linearTex.ReadPixels(new Rect(0, 0, resWidth, resHeight), 0, 0);
        linearTex.Apply();

        // 3. Fix the "Red Tint": Convert from Linear to Gamma (sRGB) manually
        // This is what Tonemapping usually does for your eyes
        Color[] pixels = linearTex.GetPixels();
        for (int p = 0; p < pixels.Length; p++)
        {
            // Simple Gamma 2.2 conversion (approximate tonemapping)
            pixels[p].r = Mathf.Pow(pixels[p].r, 1f / 2.2f);
            pixels[p].g = Mathf.Pow(pixels[p].g, 1f / 2.2f);
            pixels[p].b = Mathf.Pow(pixels[p].b, 1f / 2.2f);
        }

        Texture2D finalTex = new Texture2D(resWidth, resHeight, TextureFormat.RGB24, false);
        finalTex.SetPixels(pixels);
        finalTex.Apply();

        // 4. Save to Disk
        byte[] bytes = finalTex.EncodeToPNG();
        if (!Directory.Exists("screenshots"))
            Directory.CreateDirectory("screenshots");
        string fullPath = Path.Combine("screenshots", fileName + i + ".png");
        File.WriteAllBytes(fullPath, bytes);

        Debug.Log($"<color=green>Screenshot Saved (Fixed Colors):</color> {fullPath}");

        // Cleanup
        RenderTexture.active = null;
        thisCamera.targetTexture = null;
        RenderTexture.ReleaseTemporary(rt);
        DestroyImmediate(linearTex);
        DestroyImmediate(finalTex);
        i++;
    }

    private void AlignCamToSceneViewAndEnable()
    {
        thisCamera = GetComponent<Camera>();
#if UNITY_EDITOR
        if (SceneView.lastActiveSceneView != null)
        {
            this.transform.position = SceneView.lastActiveSceneView.camera.transform.position;
            this.transform.rotation = SceneView.lastActiveSceneView.camera.transform.rotation;
            thisCamera.fieldOfView = SceneView.lastActiveSceneView.camera.fieldOfView;
            thisCamera.orthographic = SceneView.lastActiveSceneView.orthographic;
        }
#endif
        thisCamera.enabled = true;
    }
}
