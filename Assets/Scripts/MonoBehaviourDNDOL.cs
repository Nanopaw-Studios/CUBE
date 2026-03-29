using UnityEngine;

public class MonoBehaviourDNDOL : MonoBehaviour
{
    void Awake()
    {
        DontDestroyOnLoad(this.gameObject);
    }
}