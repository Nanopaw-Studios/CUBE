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
        JoinPrivate,
    }

    [Header("State")]
    [SerializeField]
    private JoinIntent intent = JoinIntent.None;

    [SerializeField]
    private string targetRoomName;

    [SerializeField]
    private TMP_InputField joinCode;

    private bool joiningInProgress;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Optional but recommended
        PhotonNetwork.AutomaticallySyncScene = true;

        SceneManager.sceneLoaded += sceneChange;
    }

    public static string LastDisconnectReason = string.Empty;

    private void sceneChange(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "Menu")
        {
            var joinCodeTransform = GameObject.Find("Canvas")?.transform.Find("Join/List/InputField (TMP)");
            if (joinCodeTransform != null)
                joinCode = joinCodeTransform.GetComponent<TMP_InputField>();

            joiningInProgress = false;
            intent = JoinIntent.None;
            targetRoomName = null;

            // 🔥 Rebind buttons safely
            var canvas = GameObject.Find("Canvas");
            if (canvas != null)
            {
                var publicBtnTransform = canvas.transform.Find("Play Menu/List/Public");
                if (publicBtnTransform != null)
                {
                    Button publicBtn = publicBtnTransform.GetComponent<Button>();
                    if (publicBtn != null)
                    {
                        publicBtn.onClick.RemoveAllListeners();
                        publicBtn.onClick.AddListener(ClickPublic);
                    }
                }

                var createBtnTransform = canvas.transform.Find("Play Menu/List/Host Private Game");
                if (createBtnTransform != null)
                {
                    Button createBtn = createBtnTransform.GetComponent<Button>();
                    if (createBtn != null)
                    {
                        createBtn.onClick.RemoveAllListeners();
                        createBtn.onClick.AddListener(ClickCreatePrivate);
                    }
                }

                var joinBtnTransform = canvas.transform.Find("Play Menu/List/Join Private Game");
                if (joinBtnTransform != null)
                {
                    Button joinBtn = joinBtnTransform.GetComponent<Button>();
                    if (joinBtn != null)
                    {
                        joinBtn.onClick.RemoveAllListeners();
                        joinBtn.onClick.AddListener(ClickJoinPrivate_InputField);
                    }
                }
            }

            if (!string.IsNullOrEmpty(LastDisconnectReason))
            {
                Debug.LogWarning($"[PhotonMenuConnector] Returned to Menu due to: {LastDisconnectReason}");
                LastDisconnectReason = string.Empty;
            }

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

        if (PhotonNetwork.IsConnectedAndReady)
            return;

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
        if (joinCode == null)
            return;
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
            GameObject.Find("LevelLoader").GetComponent<LevelLoader>().LoadLevel("SampleScene");
        }
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        Debug.Log($"JoinRandomFailed: {message} -> creating public room");
        // PublishUserId: lets room members read each other's PlayFabId (Photon UserId)
        // for reporting and persistent muting. See PlayerSafetyIdentity.
        PhotonNetwork.CreateRoom(null, new RoomOptions { MaxPlayers = 8, PublishUserId = true }, TypedLobby.Default);
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

    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.LogWarning($"[PhotonMenuConnector] OnDisconnected: {cause}");
        joiningInProgress = false;
        intent = JoinIntent.None;

        if (cause != DisconnectCause.DisconnectByClientLogic && cause != DisconnectCause.None)
        {
            LastDisconnectReason = $"Disconnected from match ({cause})";
        }

        string activeScene = SceneManager.GetActiveScene().name;
        if (activeScene != "Menu")
        {
            Debug.LogWarning($"[PhotonMenuConnector] Disconnected while in '{activeScene}'. Recovering to Menu.");
            CursorManager.SetState(CursorManager.CursorState.UI);
            SceneManager.LoadScene("Menu");
        }
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        // When resuming from suspend / Quick Resume (pauseStatus == false)
        if (!pauseStatus)
        {
            Debug.Log("[PhotonMenuConnector] Resumed from application pause. Checking connection status...");
            if (!PhotonNetwork.IsConnected)
            {
                string activeScene = SceneManager.GetActiveScene().name;
                if (activeScene != "Menu")
                {
                    Debug.LogWarning("[PhotonMenuConnector] Network dropped during suspension. Recovering to Menu.");
                    LastDisconnectReason = "Session expired while suspended.";
                    CursorManager.SetState(CursorManager.CursorState.UI);
                    SceneManager.LoadScene("Menu");
                }
            }
        }
    }

    // ====== Core logic ======

    private void TryExecuteIntent()
    {
        if (joiningInProgress)
            return;
        if (!PhotonNetwork.IsConnectedAndReady)
            return;
        if (intent == JoinIntent.None)
            return;

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
                    IsVisible = false, // hidden from lobby lists
                    IsOpen = true,
                    PublishUserId = true, // required for reporting / persistent muting
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
