using System.Threading.Tasks;
using Unity.Services.Authentication; // Needed for sign-in
using Unity.Services.Core;
using Unity.Services.RemoteConfig;
using UnityEngine;

public class CUBEEventSystem : MonoBehaviour
{
    public struct userAttributes { }

    public struct appAttributes { }

    [Header("Event Flags")]
    public bool isLoreEvent = false;
    public bool isCubesmas = false;
    public bool isHalloween = false;

    [Header("Game Objects")]
    public GameObject halloweenStuff;
    public GameObject cubesmasStuff;
    public GameObject loreStuff;

    async void Awake()
    {
        // 1. Initialize Unity Services
        await UnityServices.InitializeAsync();

        // 2. Sign in (MANDATORY for Remote Config to work)
        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        // 3. Optional: Force the environment if "production" isn't working
        // RemoteConfigService.Instance.SetEnvironmentID("your-environment-id-here");

        // 4. Subscribe and Fetch
        RemoteConfigService.Instance.FetchCompleted += SetEvent;
        RemoteConfigService.Instance.FetchConfigs(new userAttributes(), new appAttributes());
    }

    void SetEvent(ConfigResponse response)
    {
        Debug.Log($"Remote Config Response: {response.status} from {response.requestOrigin}");

        if (response.status == ConfigRequestStatus.Success)
        {
            isHalloween = RemoteConfigService.Instance.appConfig.GetBool("isHalloweenEvent", false);
            if (halloweenStuff != null)
                halloweenStuff.SetActive(isHalloween);

            isCubesmas = RemoteConfigService.Instance.appConfig.GetBool("isCubesmasEvent", false);
            if (cubesmasStuff != null)
                cubesmasStuff.SetActive(isCubesmas);

            isLoreEvent = RemoteConfigService.Instance.appConfig.GetBool("isLoreEvent", false);
            if (loreStuff != null)
                loreStuff.SetActive(isLoreEvent);
        }
    }

    void OnDestroy()
    {
        RemoteConfigService.Instance.FetchCompleted -= SetEvent;
    }
}
