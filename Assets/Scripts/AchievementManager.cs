using UnityEngine;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

public class AchievementManager : MonoBehaviour
{
    public static AchievementManager Instance;

    private void Awake()
    {
        Instance = this;
    }

    public void Unlock(string achievementID)
    {
#if !DISABLESTEAMWORKS
        if (!CUBESteamManager.Initialized)
            return;

        SteamUserStats.SetAchievement(achievementID);
        SteamUserStats.StoreStats();
#endif
    }
}
