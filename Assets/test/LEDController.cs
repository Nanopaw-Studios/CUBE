using System;
using System.Collections;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LEDController : MonoBehaviour
{
    UdpClient client;

    [Header("Connection")]
    public string piIP = "192.168.7.101";
    public int port = 4210;

    [Header("Scene")]
    public string mainSceneName = "SampleScene";

    [Header("LED Setup")]
    public int totalLEDs = 150;
    public int halfLEDs = 75;

    [Header("Performance")]
    public float sendRate = 30f; // updates per second

    private float sendTimer;

    BasePlayerStats stats;

    // =========================
    // JSON STRUCTS
    // =========================
    [Serializable]
    public class Segment
    {
        public int start;
        public int count;
        public int[] color;
        public float brightness;
        public float fill;
    }

    [Serializable]
    public class LEDPayload
    {
        public Segment[] segments;
    }

    // =========================
    // UNITY EVENTS
    // =========================
    void Awake()
    {
        SceneManager.sceneLoaded += sceneLoaded;
    }

    private void sceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == mainSceneName)
        {
            StartCoroutine(WaitForLoaded());
        }
    }

    private IEnumerator WaitForLoaded()
    {
        yield return new WaitForSecondsRealtime(2);

        var player = Camera.main.GetComponent<PlayerCameraReferences>().GetPlayer();
        stats = player.GetComponent<BasePlayerStats>();
    }

    void Start()
    {
        client = new UdpClient();
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= sceneLoaded;
        client?.Close();
    }

    // =========================
    // SEND LOGIC
    // =========================
    void SendLEDData(float health, float stamina)
    {
        Segment healthSeg = new Segment
        {
            start = 0,
            count = halfLEDs,
            color = new int[] { 255, 0, 0 }, // red
            brightness = 1.0f,
            fill = health
        };

        Segment staminaSeg = new Segment
        {
            start = halfLEDs,
            count = halfLEDs,
            color = new int[] { 0, 255, 200 }, // cyan
            brightness = 1.0f,
            fill = stamina
        };

        LEDPayload payload = new LEDPayload
        {
            segments = new Segment[] { healthSeg, staminaSeg }
        };

        string json = JsonUtility.ToJson(payload);
        byte[] data = Encoding.UTF8.GetBytes(json);

        client.Send(data, data.Length, piIP, port);
    }

    // =========================
    // UPDATE LOOP
    // =========================
    void Update()
    {
        if (stats == null) return;

        // Limit send rate (IMPORTANT)
        sendTimer += Time.deltaTime;
        if (sendTimer < 1f / sendRate) return;
        sendTimer = 0f;

        float healthT = Mathf.Clamp01(stats.health / 100f);
        float staminaT = Mathf.Clamp01(stats.stamina / 100f);

        SendLEDData(healthT, staminaT);
    }
}