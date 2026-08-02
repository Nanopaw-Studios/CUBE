using UnityEngine;
using CUBE.Achievements;

/// <summary>
/// Backward-compatible facade delegating to <see cref="AchievementService"/>.
/// Legacy code calling <c>AchievementManager.Instance.Unlock(id)</c> will continue to function without edits.
/// </summary>
public class AchievementManager : MonoBehaviour
{
    public static AchievementManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void Unlock(string achievementID)
    {
        if (AchievementService.Instance != null)
            AchievementService.Instance.Unlock(achievementID);
        else
            Debug.LogWarning($"[AchievementManager] AchievementService unavailable. Could not unlock '{achievementID}'.");
    }

    public void AddProgress(string achievementID, float amount)
    {
        if (AchievementService.Instance != null)
            AchievementService.Instance.AddProgress(achievementID, amount);
    }
}
