using Unity.Services.RemoteConfig;
using UnityEngine;

public class CUBEEventSystem : MonoBehaviour
{
    public struct userAttributes { }
    public struct appAttributes { }

    public bool isLoreEvent = false;
    public bool isCubesmas = false;
    public bool isHalloween = false;

    public GameObject halloweenStuff;
    public GameObject cubesmasStuff;
    public GameObject loreStuff;

    void Awake()
    {
        RemoteConfigService.Instance.FetchConfigs<userAttributes, appAttributes>(new userAttributes(), new appAttributes());
    }

    void SetEvent(ConfigResponse response)
    {
        isHalloween = RemoteConfigService.Instance.appConfig.GetBool("isHalloweenEvent");
        halloweenStuff.SetActive(isHalloween);

        isCubesmas = RemoteConfigService.Instance.appConfig.GetBool("isCubesmasEvent");
        cubesmasStuff.SetActive(isCubesmas);

        isLoreEvent = RemoteConfigService.Instance.appConfig.GetBool("isLoreEvent");
        loreStuff.SetActive(isLoreEvent);
    }
}