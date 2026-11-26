using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using Photon.Realtime;

public class Launcher : MonoBehaviourPunCallbacks
{
    public static Launcher instance;

    [SerializeField] InputField roomNameInputField;
    [SerializeField] Text roomNameText;
    [SerializeField] Text errorText;
    [SerializeField] Transform roomListContent;
    [SerializeField] GameObject roomListItemPrefab;
    [SerializeField] Transform playerListContent;
    [SerializeField] GameObject playerListItemPrefab;
    [SerializeField] int maxPlayersPerRoom = 10;
    [SerializeField] int onboardingSceneIndex = 2;

    public GameObject startButton;

    // Photon only sends rooms that CHANGED in OnRoomListUpdate, so we keep the full list ourselves.
    readonly Dictionary<string, RoomInfo> cachedRoomList = new Dictionary<string, RoomInfo>();

    void Awake()
    {
        instance = this;
    }

    void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    void Start()
    {
        PhotonNetwork.AutomaticallySyncScene = true;

        if (!PhotonNetwork.IsConnected)
        {
            Debug.Log("Connecting to master....");
            PhotonNetwork.ConnectUsingSettings();
        }
        else if (PhotonNetwork.InRoom)
        {
            // Came back to the menu while still in a room: leave first, OnConnectedToMaster rejoins the lobby.
            PhotonNetwork.LeaveRoom();
            MenuManager.instance.OpenMenu("LoadingMenu");
        }
        else if (PhotonNetwork.InLobby)
        {
            ShowMenuAfterLobby();
        }
        else if (PhotonNetwork.IsConnectedAndReady)
        {
            PhotonNetwork.JoinLobby();
        }
        // else: connection is mid-transition (e.g. returning from game server); OnConnectedToMaster will fire.
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log("Connected to master");
        PhotonNetwork.AutomaticallySyncScene = true;
        if (!PhotonNetwork.InLobby)
            PhotonNetwork.JoinLobby();
    }

    public override void OnJoinedLobby()
    {
        Debug.Log("Joined Lobby");
        cachedRoomList.Clear();
        ShowMenuAfterLobby();
    }

    public override void OnLeftLobby()
    {
        cachedRoomList.Clear();
        RefreshRoomListUI();
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        cachedRoomList.Clear();
        if (errorText != null)
            errorText.text = "Disconnected: " + cause;
        if (MenuManager.instance != null)
            MenuManager.instance.OpenMenu("ErrorMenu");
    }

    void ShowMenuAfterLobby()
    {
        // Only ask for a username the first time; afterwards go straight to the title menu.
        if (PlayerPrefs.HasKey("username") && !string.IsNullOrEmpty(PhotonNetwork.NickName))
            MenuManager.instance.OpenMenu("TitleMenu");
        else
            MenuManager.instance.OpenMenu("UserNameMenu");
    }

    public void CreateRoom()
    {
        if (string.IsNullOrEmpty(roomNameInputField.text))
            return;

        PlayerTeams.ClearLocalTeam();
        RoomOptions options = new RoomOptions { MaxPlayers = maxPlayersPerRoom, IsVisible = true, IsOpen = true };
        PhotonNetwork.CreateRoom(roomNameInputField.text, options);
        MenuManager.instance.OpenMenu("LoadingMenu");
    }

    public override void OnJoinedRoom()
    {
        MenuManager.instance.OpenMenu("RoomMenu");
        roomNameText.text = PhotonNetwork.CurrentRoom.Name;

        foreach (Transform child in playerListContent)
            Destroy(child.gameObject);

        Player[] players = PhotonNetwork.PlayerList;
        for (int i = 0; i < players.Length; i++)
            AddPlayerListItem(players[i]);

        PlayerTeams.AssignMissingTeams();
        startButton.SetActive(PhotonNetwork.IsMasterClient);
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        AddPlayerListItem(newPlayer);
        PlayerTeams.AssignMissingTeams();
    }

    void AddPlayerListItem(Player player)
    {
        Instantiate(playerListItemPrefab, playerListContent).GetComponent<PlayerListItem>().SetUp(player);
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        startButton.SetActive(PhotonNetwork.IsMasterClient);
        PlayerTeams.AssignMissingTeams();
    }

    public override void OnCreateRoomFailed(short returnCode, string errorMessage)
    {
        errorText.text = "Room Generation Unsuccessful: " + errorMessage;
        MenuManager.instance.OpenMenu("ErrorMenu");
    }

    public override void OnJoinRoomFailed(short returnCode, string errorMessage)
    {
        errorText.text = "Could not join room: " + errorMessage;
        MenuManager.instance.OpenMenu("ErrorMenu");
    }

    public void JoinRoom(RoomInfo info)
    {
        PlayerTeams.ClearLocalTeam();
        PhotonNetwork.JoinRoom(info.Name);
        MenuManager.instance.OpenMenu("LoadingMenu");
    }

    public void StartGame()
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        // No late joiners once the match starts.
        PhotonNetwork.CurrentRoom.IsOpen = false;
        PhotonNetwork.CurrentRoom.IsVisible = false;
        PhotonNetwork.LoadLevel(onboardingSceneIndex);
    }

    public void LeaveRoom()
    {
        PhotonNetwork.LeaveRoom();
        MenuManager.instance.OpenMenu("LoadingMenu");
    }

    public override void OnLeftRoom()
    {
        PlayerTeams.ClearLocalTeam();
        MenuManager.instance.OpenMenu("TitleMenu");
    }

    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        for (int i = 0; i < roomList.Count; i++)
        {
            RoomInfo info = roomList[i];
            if (info.RemovedFromList || !info.IsOpen || !info.IsVisible)
                cachedRoomList.Remove(info.Name);
            else
                cachedRoomList[info.Name] = info;
        }
        RefreshRoomListUI();
    }

    void RefreshRoomListUI()
    {
        if (roomListContent == null)
            return;

        foreach (Transform trans in roomListContent)
            Destroy(trans.gameObject);

        foreach (RoomInfo info in cachedRoomList.Values)
            Instantiate(roomListItemPrefab, roomListContent).GetComponent<RoomListItem>().SetUp(info);
    }
}
