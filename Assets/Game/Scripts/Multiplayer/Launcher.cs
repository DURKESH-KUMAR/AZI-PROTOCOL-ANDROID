using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using Photon.Realtime;
using Hashtable = ExitGames.Client.Photon.Hashtable;

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
    [Tooltip("The master can only start once this many players are in the room.")]
    [SerializeField] int minPlayersToStart = 2;
    [Tooltip("Allow starting alone when testing in the Unity editor.")]
    [SerializeField] bool allowSoloInEditor = true;
    [Tooltip("Seconds before retrying after an unexpected disconnect in the menu.")]
    [SerializeField] float reconnectDelay = 3f;

    public GameObject startButton;

    // Built at runtime by cloning startButton, so they match the menu's look.
    GameObject readyButton;
    GameObject switchTeamButton;

    // Photon only sends rooms that CHANGED in OnRoomListUpdate, so we keep the full list ourselves.
    readonly Dictionary<string, RoomInfo> cachedRoomList = new Dictionary<string, RoomInfo>();
    readonly List<RoomListItem> roomListItems = new List<RoomListItem>();

    bool reconnectRequested;

    /// <summary>
    /// True while the old uGUI menu is in use. With the UI Toolkit menu (MainMenuUI) the
    /// uGUI list rows / cloned buttons are not built at all; the new UI listens to the events below.
    /// </summary>
    public static bool LegacyUI => !MainMenuUI.Available;

    /// <summary>Lobby state changed (players, teams, ready flags, host).</summary>
    public static event System.Action LobbyChanged;
    /// <summary>The room browser list changed.</summary>
    public static event System.Action RoomListChanged;
    /// <summary>An error message to show on the error screen.</summary>
    public static event System.Action<string> ErrorShown;

    public static string LastError { get; private set; } = "";
    public IEnumerable<RoomInfo> Rooms => cachedRoomList.Values;
    public int RoomCount => cachedRoomList.Count;
    public int MaxPlayersPerRoom => maxPlayersPerRoom;
    public int RequiredPlayersToStart => RequiredPlayers;

    int RequiredPlayers => Application.isEditor && allowSoloInEditor ? 1 : Mathf.Max(1, minPlayersToStart);

    void Awake()
    {
        instance = this;

        // Remove any placeholder rows left in the scene; rows are pooled in roomListItems from here on.
        if (roomListContent != null)
            foreach (Transform child in roomListContent)
                Destroy(child.gameObject);

        if (LegacyUI)
            CreateLobbyButtons();
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
            Connect();
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

    // ===================================================================== CONNECTION

    void Connect()
    {
        CancelInvoke(nameof(Connect));
        if (PhotonNetwork.IsConnected)
            return;
        if (MenuManager.instance != null)
            MenuManager.instance.OpenMenu("LoadingMenu");

        string region = GameSettings.Region;
        Debug.Log("Connecting to master (" + GameSettings.RegionLabel(region) + ")....");
        // ConnectToRegion leaves PhotonServerSettings untouched (editing it at runtime would
        // permanently change the asset in the editor).
        if (string.IsNullOrEmpty(region))
            PhotonNetwork.ConnectUsingSettings();
        else
            PhotonNetwork.ConnectToRegion(region);
    }

    /// <summary>Called by the settings screen after the region changed.</summary>
    public void ReconnectForRegionChange()
    {
        if (PhotonNetwork.InRoom)
        {
            GameRuntime.Toast("Region will change after you leave the room.");
            return;
        }
        if (!PhotonNetwork.IsConnected)
        {
            Connect();
            return;
        }
        reconnectRequested = true;
        PhotonNetwork.Disconnect(); // OnDisconnected reconnects with the new region
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log("Connected to master (" + PhotonNetwork.CloudRegion + ")");
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
        RefreshRoomListUI();

        if (reconnectRequested)
        {
            reconnectRequested = false;
            Connect();
            return;
        }
        if (cause == DisconnectCause.DisconnectByClientLogic || cause == DisconnectCause.ApplicationQuit)
            return;

        ShowError("Disconnected: " + cause + "\nRetrying...");
        // Mobile networks drop often: retry automatically instead of leaving the player stuck.
        Invoke(nameof(Connect), reconnectDelay);
    }

    void ShowMenuAfterLobby()
    {
        // Only ask for a username the first time; afterwards go straight to the title menu.
        if (PlayerPrefs.HasKey(PlayerUsernameManager.UsernameKey) && !string.IsNullOrEmpty(PhotonNetwork.NickName))
            MenuManager.instance.OpenMenu("TitleMenu");
        else
            MenuManager.instance.OpenMenu("UserNameMenu");
    }

    // ===================================================================== ROOMS

    // Bound to the legacy uGUI button.
    public void CreateRoom()
    {
        if (roomNameInputField != null)
            CreateRoom(roomNameInputField.text);
    }

    public bool CreateRoom(string roomName)
    {
        roomName = (roomName ?? "").Trim();
        if (roomName.Length == 0 || !PhotonNetwork.IsConnectedAndReady)
            return false;

        PlayerTeams.ClearLocalTeam();
        RoomOptions options = new RoomOptions { MaxPlayers = maxPlayersPerRoom, IsVisible = true, IsOpen = true };
        PhotonNetwork.CreateRoom(roomName, options);
        MenuManager.instance.OpenMenu("LoadingMenu");
        return true;
    }

    void ShowError(string message)
    {
        LastError = message;
        if (errorText != null)
            errorText.text = message;
        ErrorShown?.Invoke(message);
        if (MenuManager.instance != null)
            MenuManager.instance.OpenMenu("ErrorMenu");
    }

    public override void OnJoinedRoom()
    {
        MenuManager.instance.OpenMenu("RoomMenu");
        if (roomNameText != null)
            roomNameText.text = PhotonNetwork.CurrentRoom.Name;

        // Fresh lobby state for this player (stats are reset by the master when the match starts).
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { GameProps.Ready, false } });

        if (LegacyUI && playerListContent != null)
        {
            foreach (Transform child in playerListContent)
                Destroy(child.gameObject);
            Player[] players = PhotonNetwork.PlayerList;
            for (int i = 0; i < players.Length; i++)
                AddPlayerListItem(players[i]);
        }

        PlayerTeams.AssignMissingTeams();
        RefreshLobbyButtons();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        AddPlayerListItem(newPlayer);
        PlayerTeams.AssignMissingTeams();
        RefreshLobbyButtons();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        PlayerTeams.Rebalance();
        RefreshLobbyButtons();
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        RefreshLobbyButtons();
    }

    void AddPlayerListItem(Player player)
    {
        if (!LegacyUI || playerListContent == null)
            return;
        Instantiate(playerListItemPrefab, playerListContent).GetComponent<PlayerListItem>().SetUp(player);
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        PlayerTeams.AssignMissingTeams();
        RefreshLobbyButtons();
    }

    public override void OnCreateRoomFailed(short returnCode, string errorMessage)
    {
        ShowError("Could not create the room: " + errorMessage);
    }

    public override void OnJoinRoomFailed(short returnCode, string errorMessage)
    {
        ShowError("Could not join the room: " + errorMessage);
    }

    public void JoinRoom(RoomInfo info)
    {
        if (info.MaxPlayers > 0 && info.PlayerCount >= info.MaxPlayers)
        {
            GameRuntime.Toast("That room is full.");
            return;
        }
        PlayerTeams.ClearLocalTeam();
        PhotonNetwork.JoinRoom(info.Name);
        MenuManager.instance.OpenMenu("LoadingMenu");
    }

    public void StartGame()
    {
        if (!PhotonNetwork.IsMasterClient)
            return;
        if (!CanStart(out string reason))
        {
            GameRuntime.Toast(reason);
            return;
        }

        // No late joiners once the match starts.
        PhotonNetwork.CurrentRoom.IsOpen = false;
        PhotonNetwork.CurrentRoom.IsVisible = false;
        PhotonNetwork.LoadLevel(GameScenes.OnBoarding);
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

    // ===================================================================== LOBBY: READY / TEAMS / START

    void CreateLobbyButtons()
    {
        if (startButton == null)
            return;
        RectTransform start = startButton.GetComponent<RectTransform>();
        float step = start != null ? start.rect.height + 16f : 0f;

        readyButton = CloneButton("ReadyButton", 1, step, ToggleReady);
        switchTeamButton = CloneButton("SwitchTeamButton", 2, step, () =>
        {
            if (!PlayerTeams.RequestSwitchLocalTeam())
                GameRuntime.Toast("Teams would be uneven - can't switch now.");
        });
    }

    /// <summary>Copies the Start button (same style), clears its click events and stacks it below.</summary>
    GameObject CloneButton(string name, int slot, float step, UnityEngine.Events.UnityAction onClick)
    {
        GameObject clone = Instantiate(startButton, startButton.transform.parent);
        clone.name = name;
        Button button = clone.GetComponent<Button>();
        if (button != null)
        {
            button.onClick = new Button.ButtonClickedEvent(); // drop the copied StartGame binding
            button.onClick.AddListener(onClick);
        }
        RectTransform rt = clone.GetComponent<RectTransform>();
        if (rt != null && step > 0f)
            rt.anchoredPosition -= new Vector2(0f, step * slot);
        clone.SetActive(true);
        return clone;
    }

    public void ToggleReady()
    {
        bool ready = GameProps.GetBool(PhotonNetwork.LocalPlayer.CustomProperties, GameProps.Ready);
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { GameProps.Ready, !ready } });
    }

    public bool CanStart(out string reason)
    {
        reason = "";
        if (PhotonNetwork.CurrentRoom == null)
            return false;

        Player[] players = PhotonNetwork.PlayerList;
        if (players.Length < RequiredPlayers)
        {
            reason = "WAITING FOR PLAYERS " + players.Length + "/" + RequiredPlayers;
            return false;
        }

        int notReady = 0;
        foreach (Player p in players)
            if (!p.IsMasterClient && !GameProps.GetBool(p.CustomProperties, GameProps.Ready))
                notReady++;
        if (notReady > 0)
        {
            reason = "WAITING: " + notReady + " NOT READY";
            return false;
        }
        return true;
    }

    /// <summary>Switch the local player to the other team (lobby only).</summary>
    public void SwitchTeam()
    {
        if (!PlayerTeams.RequestSwitchLocalTeam())
            GameRuntime.Toast("Teams would be uneven - can't switch now.");
    }

    void RefreshLobbyButtons()
    {
        if (!PhotonNetwork.InRoom)
            return;
        LobbyChanged?.Invoke();
        if (!LegacyUI || startButton == null)
            return;

        bool master = PhotonNetwork.IsMasterClient;
        startButton.SetActive(master);
        if (master)
        {
            bool canStart = CanStart(out string reason);
            GameUI.SetButtonLabel(startButton, canStart ? "START MATCH" : reason);
            Button b = startButton.GetComponent<Button>();
            if (b != null)
                b.interactable = canStart;
        }

        // The master starts the match, so only the others need a Ready button.
        if (readyButton != null)
        {
            readyButton.SetActive(!master);
            bool ready = GameProps.GetBool(PhotonNetwork.LocalPlayer.CustomProperties, GameProps.Ready);
            GameUI.SetButtonLabel(readyButton, ready ? "READY  (TAP TO CANCEL)" : "TAP WHEN READY");
        }

        if (switchTeamButton != null)
        {
            int team = PlayerTeams.GetTeam(PhotonNetwork.LocalPlayer);
            GameUI.SetButtonLabel(switchTeamButton, team == 0 ? "SWITCH TEAM" : "SWITCH TO " + Teams.Name(Teams.Other(team)));
            Button b = switchTeamButton.GetComponent<Button>();
            if (b != null)
                b.interactable = PlayerTeams.CanSwitchLocalTeam();
        }
    }

    // ===================================================================== ROOM LIST

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
        RoomListChanged?.Invoke();
        if (!LegacyUI || roomListContent == null)
            return;

        // Reuse existing rows instead of destroying and instantiating the whole list on every
        // lobby update (avoids GC spikes and layout rebuilds on Android).
        int index = 0;
        foreach (RoomInfo info in cachedRoomList.Values)
        {
            RoomListItem item;
            if (index < roomListItems.Count)
            {
                item = roomListItems[index];
            }
            else
            {
                item = Instantiate(roomListItemPrefab, roomListContent).GetComponent<RoomListItem>();
                roomListItems.Add(item);
            }
            item.SetUp(info);
            if (!item.gameObject.activeSelf)
                item.gameObject.SetActive(true);
            index++;
        }

        for (int i = index; i < roomListItems.Count; i++)
        {
            if (roomListItems[i].gameObject.activeSelf)
                roomListItems[i].gameObject.SetActive(false);
        }
    }
}
