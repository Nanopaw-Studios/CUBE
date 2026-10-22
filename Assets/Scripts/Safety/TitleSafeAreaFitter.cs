using System;
using UnityEngine;

/// <summary>
/// XR-135: Adaptive Title Safe Area and Aspect Ratio Fitter.
/// 
/// Preserves 100% native, border-to-border UI layouts for standard 16:9 PC monitors (no awkward gaps),
/// while dynamically activating safe-area constraints ONLY when:
/// 1. The display is at an awkward / non-standard aspect ratio (e.g. 21:9 Ultra-Wide, 32:9 Super Ultra-Wide, 16:10, 4:3).
/// 2. Hardware displays report an active safe area inset (notches, cutouts, television overscan).
/// 3. The game is running on a native Xbox console platform (GameCoreXboxSeries, GameCoreXboxOne).
/// 4. The user explicitly enables TV Safe Area in settings.
/// </summary>
[DisallowMultipleComponent]
public class TitleSafeAreaFitter : MonoBehaviour
{
    [Header("Title Safe Settings")]
    [Tooltip("Target safe margin percentage when safe area is active (0.05 = 5% per side for 90% Title Safe Area).")]
    [Range(0.01f, 0.10f)]
    public float marginPercent = 0.05f;

    [Tooltip("Allow manual testing override in the Inspector.")]
    public bool testForceEnable = false;

    private RectTransform rectTransform;
    private Vector2 originalAnchorMin;
    private Vector2 originalAnchorMax;
    private Vector2 originalOffsetMin;
    private Vector2 originalOffsetMax;
    private bool cachedOriginal = false;
    private Vector2 lastScreenSize;

    /// <summary>
    /// Returns true if the current display conditions require safe area padding.
    /// Standard 16:9 PC monitors return false to keep the clean desktop layout intact.
    /// </summary>
    public static bool ShouldApplySafeArea(bool forceTesting = false)
    {
        if (forceTesting)
            return true;

        // Native console platforms (Xbox Series X|S, Xbox One) targeting televisions with potential overscan
        if (Application.platform == RuntimePlatform.GameCoreXboxSeries ||
            Application.platform == RuntimePlatform.GameCoreXboxOne)
        {
            return true;
        }

        // Hardware cutout / notch / display overscan reported by OS
        Rect safeArea = Screen.safeArea;
        if (safeArea.xMin > 0 || safeArea.yMin > 0 ||
            safeArea.width < Screen.width || safeArea.height < Screen.height)
        {
            return true;
        }

        // Detect awkward / non-standard aspect ratios (standard 16:9 is ~1.7778)
        float currentAspect = (float)Screen.width / Mathf.Max(1f, (float)Screen.height);
        bool isAwkwardAspect = currentAspect > 1.85f || currentAspect < 1.70f;
        if (isAwkwardAspect)
        {
            return true;
        }

        // User preference override from Settings
        if (PlayerPrefs.GetInt("CUBE.TVSafeArea", 0) == 1)
        {
            return true;
        }

        // Standard 16:9 PC monitor: do not apply safe area (preserves native design)
        return false;
    }

    private void Awake()
    {
        CacheOriginalValues();
    }

    private void Start()
    {
        RefreshLayout();
    }

    private void OnEnable()
    {
        RefreshLayout();
    }

    private void CacheOriginalValues()
    {
        if (cachedOriginal) return;
        rectTransform = GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            originalAnchorMin = rectTransform.anchorMin;
            originalAnchorMax = rectTransform.anchorMax;
            originalOffsetMin = rectTransform.offsetMin;
            originalOffsetMax = rectTransform.offsetMax;
            cachedOriginal = true;
        }
    }

    private void OnRectTransformDimensionsChange()
    {
        Vector2 currentScreen = new Vector2(Screen.width, Screen.height);
        if (currentScreen != lastScreenSize)
        {
            lastScreenSize = currentScreen;
            RefreshLayout();
        }
    }

    /// <summary>
    /// Evaluates display aspect ratio and safe area state, applying padding only when necessary.
    /// </summary>
    public void RefreshLayout()
    {
        CacheOriginalValues();
        if (rectTransform == null) return;

        bool apply = ShouldApplySafeArea(testForceEnable);

        if (!apply)
        {
            // Restore exact original layout (clean standard 16:9 monitor view)
            rectTransform.anchorMin = originalAnchorMin;
            rectTransform.anchorMax = originalAnchorMax;
            rectTransform.offsetMin = originalOffsetMin;
            rectTransform.offsetMax = originalOffsetMax;
            return;
        }

        // Apply gentle safe-area constraint for awkward aspect ratios or console overscan
        float padX = Screen.width * marginPercent;
        float padY = Screen.height * marginPercent;

        // If stretched across full width/height
        if (originalAnchorMin == Vector2.zero && originalAnchorMax == Vector2.one)
        {
            rectTransform.offsetMin = new Vector2(padX, padY);
            rectTransform.offsetMax = new Vector2(-padX, -padY);
        }
    }
}
