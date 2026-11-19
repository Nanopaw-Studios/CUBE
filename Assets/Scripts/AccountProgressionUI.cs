using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Binds to ProgressionManager and PlayfabManager to display the local player's
/// Account Level, XP progress bar, and Cube Coins on the Main Menu or HUD.
/// </summary>
public class AccountProgressionUI : MonoBehaviour
{
    [Header("Account Level & XP")]
    public TMP_Text levelText;
    public TMP_Text xpText;
    public Slider xpSlider;

    [Header("Optional Currency Display")]
    public TMP_Text cubeCoinsText;

    private bool _subscribedProgression;
    private bool _subscribedPlayFab;

    private void Start()
    {
        TrySubscribe();
        RefreshUI();
    }

    private void OnEnable()
    {
        TrySubscribe();
        RefreshUI();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    private void Update()
    {
        if (!_subscribedProgression || !_subscribedPlayFab)
        {
            TrySubscribe();
        }
    }

    private void TrySubscribe()
    {
        if (!_subscribedProgression)
        {
            ProgressionManager prog = ProgressionManager.EnsureExists();
            if (prog != null)
            {
                prog.OnAccountXPChanged += HandleAccountXPChanged;
                prog.OnProgressionSynced += RefreshUI;
                _subscribedProgression = true;
                RefreshUI();
            }
        }

        if (!_subscribedPlayFab && PlayfabManager.Instance != null)
        {
            PlayfabManager.Instance.OnCubeCoinsChanged += HandleCubeCoinsChanged;
            _subscribedPlayFab = true;
            HandleCubeCoinsChanged(PlayfabManager.Instance.CachedCC);
        }
    }

    private void Unsubscribe()
    {
        if (_subscribedProgression && ProgressionManager.Instance != null)
        {
            ProgressionManager.Instance.OnAccountXPChanged -= HandleAccountXPChanged;
            ProgressionManager.Instance.OnProgressionSynced -= RefreshUI;
            _subscribedProgression = false;
        }

        if (_subscribedPlayFab && PlayfabManager.Instance != null)
        {
            PlayfabManager.Instance.OnCubeCoinsChanged -= HandleCubeCoinsChanged;
            _subscribedPlayFab = false;
        }
    }

    private void HandleAccountXPChanged(int level, int currentXP, int requiredXP)
    {
        RefreshUI();
    }

    private void HandleCubeCoinsChanged(int coins)
    {
        if (cubeCoinsText != null)
            cubeCoinsText.text = $"{coins} CC";
    }

    public void RefreshUI()
    {
        ProgressionManager prog = ProgressionManager.Instance;
        int lvl = prog != null ? prog.AccountLevel : 1;
        int curXp = prog != null ? prog.AccountXP : 0;
        int reqXp = prog != null ? prog.AccountXPForNextLevel : 500;

        if (levelText != null)
            levelText.text = $"LVL {lvl}";

        if (xpText != null)
            xpText.text = $"{curXp} / {reqXp} XP";

        if (xpSlider != null)
        {
            xpSlider.minValue = 0f;
            xpSlider.maxValue = Mathf.Max(1, reqXp);
            xpSlider.value = curXp;
        }

        if (cubeCoinsText != null && PlayfabManager.Instance != null)
        {
            cubeCoinsText.text = $"{PlayfabManager.Instance.CachedCC} CC";
        }
    }
}
