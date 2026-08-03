using System.Collections.Generic;
using UnityEngine;

namespace CUBE.Achievements
{
    /// <summary>
    /// Lightweight UI component that renders on-screen toast notifications
    /// when achievements are unlocked or gain progress.
    /// Works out of the box using IMGUI (OnGUI) with zero scene setup required.
    /// </summary>
    public class AchievementNotificationUI : MonoBehaviour
    {
        public static AchievementNotificationUI Instance { get; private set; }

        public float ToastDuration = 4.0f;
        public bool ShowProgressToasts = false;

        private struct ToastItem
        {
            public string Title;
            public string Description;
            public Sprite Icon;
            public bool IsUnlock;
        }

        private readonly Queue<ToastItem> _queue = new();
        private ToastItem _currentToast;
        private bool _hasActiveToast;
        private float _toastTimer;

        private GUIStyle _boxStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _descStyle;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            if (AchievementService.Instance != null)
            {
                AchievementService.Instance.OnAchievementUnlocked += OnAchievementUnlocked;
                AchievementService.Instance.OnProgressUpdated += OnProgressUpdated;
            }
        }

        private void OnDisable()
        {
            if (AchievementService.Instance != null)
            {
                AchievementService.Instance.OnAchievementUnlocked -= OnAchievementUnlocked;
                AchievementService.Instance.OnProgressUpdated -= OnProgressUpdated;
            }
        }

        private void Update()
        {
            if (AchievementService.Instance != null)
            {
                AchievementService.Instance.OnAchievementUnlocked -= OnAchievementUnlocked;
                AchievementService.Instance.OnAchievementUnlocked += OnAchievementUnlocked;
            }

            if (_hasActiveToast)
            {
                _toastTimer -= Time.unscaledDeltaTime;
                if (_toastTimer <= 0f)
                {
                    _hasActiveToast = false;
                }
            }

            if (!_hasActiveToast && _queue.Count > 0)
            {
                _currentToast = _queue.Dequeue();
                _hasActiveToast = true;
                _toastTimer = ToastDuration;
            }
        }

        private void OnAchievementUnlocked(AchievementUnlockedEventArgs args)
        {
            if (args.Definition == null) return;
            _queue.Enqueue(new ToastItem
            {
                Title = args.Definition.DisplayName,
                Description = args.Definition.Description,
                Icon = args.Definition.Icon,
                IsUnlock = true
            });
        }

        private void OnProgressUpdated(AchievementProgressEventArgs args)
        {
            if (!ShowProgressToasts || args.Definition == null) return;
            _queue.Enqueue(new ToastItem
            {
                Title = args.Definition.DisplayName,
                Description = $"{args.CurrentProgress:0} / {args.TargetValue:0}",
                Icon = args.Definition.Icon,
                IsUnlock = false
            });
        }

        private void OnGUI()
        {
            // Temporary IMGUI toast disabled — replaced by Canvas UI in PlayerUIHandler
            return;
        }

        private void InitStyles()
        {
            if (_boxStyle != null) return;

            Texture2D bgTex = new Texture2D(1, 1);
            bgTex.SetPixel(0, 0, new Color(0.08f, 0.09f, 0.12f, 0.92f));
            bgTex.Apply();

            _boxStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = bgTex }
            };

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1.0f, 0.8f, 0.2f) }
            };

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            _descStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                normal = { textColor = new Color(0.8f, 0.85f, 0.9f) }
            };
        }
    }
}
