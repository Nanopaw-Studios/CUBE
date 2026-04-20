using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PhotonMenuConnector : MonoBehaviourPunCallbacks
{
    public static PhotonMenuConnector Instance;
    private const string ROOM_CHARS = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    // (no I, O, 0, 1 � avoids confusion)
    public static bool PlayFabPhotonAuthReady; // set true after AuthValues assigned

    private string GenerateRoomCode(int length = 5)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder(length);
        for (int i = 0; i < length; i++)
        {
            int index = Random.Range(0, ROOM_CHARS.Length);
            sb.Append(ROOM_CHARS[index]);
        }
        return sb.ToString();
    }


    public enum JoinIntent
    {
        None,
        Public,
        CreatePrivate,
        JoinPrivate
    }

    [Header("State")]
    [SerializeField] private JoinIntent intent = JoinIntent.None;
    [SerializeField] private string targetRoomName;
    [SerializeField] private TMP_InputField joinCode;

    private bool joiningInProgress;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Optional but recommended
        PhotonNetwork.AutomaticallySyncScene = true;

        SceneManager.sceneLoaded += sceneChange;
    }

    private void sceneChange(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "Menu")
        {
            joinCode = GameObject.Find("Canvas")
                .transform.Find("Join/List/InputField (TMP)")
                .GetComponent<TMP_InputField>();

            joiningInProgress = false;
            intent = JoinIntent.None;
            targetRoomName = null;

            
            // 🔥 Rebind buttons
            Button publicBtn = GameObject.Find("Canvas")
            .transform.Find("Play Menu/List/Public")
            .GetComponent<Button>();
            publicBtn.onClick.RemoveAllListeners();
            publicBtn.onClick.AddListener(ClickPublic);

            Button createBtn = GameObject.Find("Canvas")
            .transform.Find("Play Menu/List/Host Private Game")
            .GetComponent<Button>();
            createBtn.onClick.RemoveAllListeners();
            createBtn.onClick.AddListener(ClickCreatePrivate);

            Button joinBtn = GameObject.Find("Cavas")
            .transform.Find("Play Menu/List/Join Private Game")
            .GetComponent<Button>();
            joinBtn.onClick.RemoveAllListeners();
            joinBtn.onClick.AddListener(ClickJoinPrivate_InputField);

            Debug.Log("PhotonMenuConnector reset for menu.");
        }
    }

    private void EnsureConnected()
    {
        if (!PlayFabPhotonAuthReady)
        {
            Debug.Log("Waiting for PlayFab/Photon auth before connecting.");
            return;
        }

        if (PhotonNetwork.IsConnectedAndReady) return;

        if (!PhotonNetwork.IsConnected)
        {
            PhotonNetwork.ConnectUsingSettings();
        }
        else if (PhotonNetwork.NetworkClientState != ClientState.ConnectedToMasterServer)
        {
            Debug.Log("Not on Master yet, waiting...");
        }
    }

    // ====== UI calls ======

    public void ClickPublic()
    {
        intent = JoinIntent.Public;
        targetRoomName = null;
        joiningInProgress = false;
        EnsureConnected();
        TryExecuteIntent();
    }

    public void ClickCreatePrivate()
    {
        intent = JoinIntent.CreatePrivate;
        joiningInProgress = false;

        targetRoomName = GenerateRoomCode();
        EnsureConnected();
        TryExecuteIntent();
    }

    public void ClickJoinPrivate(string roomName)
    {
        intent = JoinIntent.JoinPrivate;
        targetRoomName = roomName;
        joiningInProgress = false;
        EnsureConnected();
        TryExecuteIntent();
    }

    public void ClickJoinPrivate_InputField()
    {
        if (joinCode == null) return;
        ClickJoinPrivate(joinCode.text.ToUpper().Trim());
    }

    // ====== Photon callbacks ======

    public override void OnConnectedToMaster()
    {
        Debug.Log("Connected to Master");
        TryExecuteIntent();
    }

    public override void OnJoinedRoom()
    {
        joiningInProgress = false;

        Debug.Log($"Joined room: {PhotonNetwork.CurrentRoom.Name}");

        if (PhotonNetwork.IsMasterClient)
        {
            GameObject.Find("LevelLoader")
                .GetComponent<LevelLoader>()
                .LoadLevel("SampleScene");
        }
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        Debug.Log($"JoinRandomFailed: {message} -> creating public room");
        PhotonNetwork.CreateRoom(null, new RoomOptions { MaxPlayers = 8 }, TypedLobby.Default);
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.LogWarning($"JoinRoomFailed ({returnCode}): {message}");
        joiningInProgress = false;
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Debug.LogWarning($"Room create failed ({returnCode}): {message}");

        if (intent == JoinIntent.CreatePrivate)
        {
            // Try again with a new code
            targetRoomName = GenerateRoomCode();
            joiningInProgress = false;
            TryExecuteIntent();
            return;
        }

        joiningInProgress = false;
    }

    // ====== Core logic ======

    private void TryExecuteIntent()
    {
        if (joiningInProgress) return;
        if (!PhotonNetwork.IsConnectedAndReady) return;
        if (intent == JoinIntent.None) return;

        joiningInProgress = true;

        switch (intent)
        {
            case JoinIntent.Public:
                PhotonNetwork.JoinRandomRoom();
                break;

            case JoinIntent.CreatePrivate:
                {
                    var opts = new RoomOptions
                    {
                        MaxPlayers = 8,
                        IsVisible = false,   // hidden from lobby lists
                        IsOpen = true
                    };
                    PhotonNetwork.CreateRoom(targetRoomName, opts, TypedLobby.Default);
                    break;
                }

            case JoinIntent.JoinPrivate:
                PhotonNetwork.JoinRoom(targetRoomName);
                break;
        }
    }
}
