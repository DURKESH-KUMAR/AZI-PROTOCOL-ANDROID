using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Photon.Pun;
using Photon.Realtime;

/// <summary>
/// UI Toolkit replacement for the uGUI Canvas in MainMenu.unity (Resources/UI/MainMenu.uxml).
/// Added automatically by GameRuntime. Same screens as the original canvas (Loading, UserName,
/// Title, CreateRoom, FindRoom, Room, Error) plus settings, a live connection status and a
/// team-based lobby. Launcher / MenuManager / PlayerUsernameManager keep all the logic:
/// MenuManager.OpenMenu(name) switches screens here too, and Launcher's events refresh lists.
/// The old canvas stays in the scene but is hidden (its scripts keep running).
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    static bool? available;
    public static bool Available => available ?? (available = GameUI.Load("MainMenu") != null).Value;

    VisualElement root, spinner, settingsHost, settings;
    readonly Dictionary<string, VisualElement> screens = new Dictionary<string, VisualElement>();
    VisualElement current;

    TextField usernameField, roomField;
    Label usernameError, createError, errorText, welcome, statusText, loadingSub;
    VisualElement statusDot;

    ScrollView roomList;
    Label roomEmpty;
    readonly List<VisualElement> roomCards = new List<VisualElement>();

    Label lobbyName, lobbyCount, lobbyStatus;
    VisualElement teamBlue, teamRed;
    Button lobbyMain, lobbySwitch;
    readonly List<VisualElement> playerRows = new List<VisualElement>();

    float statusTimer;
    float spinAngle;

    Label loadingText;
    float dotsTimer;
    int dots;
    VisualElement lobbyProgress;
    readonly List<VisualElement> openSlots = new List<VisualElement>();
    readonly List<VisualElement> staggered = new List<VisualElement>();

    static readonly Dictionary<string, string> ScreenOf = new Dictionary<string, string>
    {
        { "LoadingMenu", "screen-loading" },
        { "UserNameMenu", "screen-username" },
        { "TitleMenu", "screen-title" },
        { "CreateRoomMenu", "screen-create" },
        { "FindRoomMenu", "screen-find" },
        { "RoomMenu", "screen-room" },
        { "ErrorMenu", "screen-error" },
    };

    void Awake()
    {
        HideLegacyCanvas();

        root = GameUI.CreateDocument(transform, "MainMenu", 20).rootVisualElement;
        root.Q("shade").style.backgroundImage = HorizontalShade();
        foreach (KeyValuePair<string, string> pair in ScreenOf)
            screens[pair.Key] = root.Q(pair.Value);

        spinner = root.Q("spinner");
        loadingText = root.Q<Label>("loading-text");
        root.Q<Label>("version").text = "V" + Application.version;
        root.Query(className: "stagger").ToList(staggered);
        loadingSub = root.Q<Label>("loading-sub");
        settingsHost = root.Q("settings-host");
        statusDot = root.Q("status-dot");
        statusText = root.Q<Label>("status-text");

        BindUsername();
        BindTitle();
        BindCreate();
        BindFind();
        BindLobby();
        errorText = root.Q<Label>("error-text");
        root.Bind("error-ok", OnErrorOk);
    }

    void OnEnable()
    {
        MenuManager.MenuOpened += Show;
        Launcher.LobbyChanged += RefreshLobby;
        Launcher.RoomListChanged += RefreshRooms;
        Launcher.ErrorShown += OnError;
    }

    void OnDisable()
    {
        MenuManager.MenuOpened -= Show;
        Launcher.LobbyChanged -= RefreshLobby;
        Launcher.RoomListChanged -= RefreshRooms;
        Launcher.ErrorShown -= OnError;
    }

    void Start()
    {
        Show(MenuManager.Current ?? "LoadingMenu");
    }

    /// <summary>The original canvas also hosts Launcher/MenuManager, so only stop it drawing and taking touches.</summary>
    static void HideLegacyCanvas()
    {
        MenuManager manager = MenuManager.instance != null ? MenuManager.instance : FindAnyObjectByType<MenuManager>();
        if (manager == null)
            return;
        Canvas canvas = manager.GetComponent<Canvas>();
        if (canvas != null)
            canvas.enabled = false;
        UnityEngine.UI.GraphicRaycaster raycaster = manager.GetComponent<UnityEngine.UI.GraphicRaycaster>();
        if (raycaster != null)
            raycaster.enabled = false;
    }

    // ===================================================================== SCREENS

    void Show(string menuName)
    {
        if (!screens.TryGetValue(menuName, out VisualElement next) || next == null)
            return;
        if (current == next)
            return;
        if (current != null)
        {
            current.RemoveFromClassList("screen--in");
            current.SetVisible(false);
        }
        current = next;
        next.SetVisible(true);
        next.schedule.Execute(() => next.AddToClassList("screen--in")); // fade/slide in next frame

        switch (menuName)
        {
            case "UserNameMenu":
                usernameField.SetValueWithoutNotify(PlayerUsernameManager.SavedName);
                usernameError.text = "";
                break;
            case "TitleMenu":
                welcome.text = "Welcome, <color=#FFB830>" + PhotonNetwork.NickName + "</color>";
                StaggerIn();
                break;
            case "CreateRoomMenu":
                createError.text = "";
                if (string.IsNullOrWhiteSpace(roomField.value))
                    roomField.SetValueWithoutNotify(PhotonNetwork.NickName + "'s room");
                break;
            case "FindRoomMenu":
                RefreshRooms();
                break;
            case "RoomMenu":
                RefreshLobby();
                break;
            case "ErrorMenu":
                errorText.text = Launcher.LastError;
                break;
            case "LoadingMenu":
                loadingSub.text = PhotonNetwork.IsConnected ? "Please wait..." : "Finding the best server...";
                break;
        }
    }

    /// <summary>Title buttons slide in one after another.</summary>
    void StaggerIn()
    {
        for (int i = 0; i < staggered.Count; i++)
        {
            VisualElement e = staggered[i];
            e.RemoveFromClassList("stagger--in");
            float delay = 0.12f + i * 0.07f;
            e.style.transitionDelay = new List<TimeValue> { new TimeValue(delay), new TimeValue(delay), new TimeValue(delay), new TimeValue(delay) };
            e.schedule.Execute(() => e.AddToClassList("stagger--in"));
            // Afterwards, no delay on hover/press feedback.
            e.schedule.Execute(() => e.style.transitionDelay = StyleKeyword.Null).StartingIn((long)((delay + 0.4f) * 1000f));
        }
    }

    static void Open(string menuName)
    {
        if (MenuManager.instance != null)
            MenuManager.instance.OpenMenu(menuName);
    }

    // ===================================================================== USERNAME / TITLE / CREATE

    void BindUsername()
    {
        usernameField = root.Q<TextField>("username-field");
        usernameError = root.Q<Label>("username-error");
        usernameField.maxLength = PlayerUsernameManager.MaxUsernameLength;
        usernameField.RegisterCallback<KeyDownEvent>(e =>
        {
            if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                SubmitUsername();
        });
        root.Bind("username-next", SubmitUsername);
    }

    void SubmitUsername()
    {
        if (PlayerUsernameManager.TryApply(usernameField.value, out string error))
            Open("TitleMenu");
        else
            usernameError.text = error;
    }

    void BindTitle()
    {
        welcome = root.Q<Label>("welcome");
        root.Bind("change-name", () => Open("UserNameMenu"));
        root.Bind("title-find", () => Open("FindRoomMenu"));
        root.Bind("title-create", () => Open("CreateRoomMenu"));
        root.Bind("title-settings", OpenSettings);
        root.Bind("title-quit", Quit);
    }

    void BindCreate()
    {
        roomField = root.Q<TextField>("room-field");
        createError = root.Q<Label>("create-error");
        root.Bind("create-back", () => Open("TitleMenu"));
        root.Bind("create-confirm", () =>
        {
            if (Launcher.instance == null || !Launcher.instance.CreateRoom(roomField.value))
                createError.text = PhotonNetwork.IsConnectedAndReady ? "Enter a room name." : "Not connected yet - try again in a moment.";
        });
    }

    // ===================================================================== ROOM BROWSER

    void BindFind()
    {
        roomList = root.Q<ScrollView>("room-list");
        roomEmpty = root.Q<Label>("room-empty");
        root.Bind("find-back", () => Open("TitleMenu"));
        root.Bind("find-create", () => Open("CreateRoomMenu"));
    }

    void RefreshRooms()
    {
        if (roomList == null)
            return;
        int used = 0;
        if (Launcher.instance != null)
        {
            foreach (RoomInfo info in Launcher.instance.Rooms)
            {
                VisualElement card = RoomCard(used++);
                bool full = info.MaxPlayers > 0 && info.PlayerCount >= info.MaxPlayers;
                card.userData = info;
                card.Q<Label>("name").text = info.Name;
                card.Q<Label>("count").text = info.PlayerCount + (info.MaxPlayers > 0 ? " / " + info.MaxPlayers : "");
                card.EnableInClassList("room-card--full", full);
                Button join = card.Q<Button>("join");
                join.text = full ? "FULL" : "JOIN";
                join.SetEnabled(!full);
            }
        }
        for (int i = 0; i < roomCards.Count; i++)
            roomCards[i].SetVisible(i < used);
        roomEmpty.SetVisible(used == 0);
    }

    VisualElement RoomCard(int index)
    {
        while (roomCards.Count <= index)
        {
            VisualElement card = new VisualElement { pickingMode = PickingMode.Ignore };
            card.AddToClassList("room-card");
            card.Add(new Label { name = "name", pickingMode = PickingMode.Ignore });
            card.Q<Label>("name").AddToClassList("room-card__name");
            Label count = new Label { name = "count", pickingMode = PickingMode.Ignore };
            count.AddToClassList("room-card__count");
            card.Add(count);
            Button join = new Button { name = "join", text = "JOIN" };
            join.AddToClassList("btn");
            join.AddToClassList("btn--primary");
            join.clicked += () =>
            {
                GameAudio.PlayUI(GameAudio.Click, 0.5f);
                if (card.userData is RoomInfo info && Launcher.instance != null)
                    Launcher.instance.JoinRoom(info);
            };
            card.Add(join);
            roomList.Add(card);
            roomCards.Add(card);
        }
        return roomCards[index];
    }

    // ===================================================================== LOBBY

    void BindLobby()
    {
        lobbyName = root.Q<Label>("lobby-name");
        lobbyCount = root.Q<Label>("lobby-count");
        lobbyStatus = root.Q<Label>("lobby-status");
        teamBlue = root.Q("team-blue");
        teamRed = root.Q("team-red");
        lobbyProgress = root.Q("lobby-progress");
        root.Bind("lobby-leave", () => { if (Launcher.instance != null) Launcher.instance.LeaveRoom(); });
        lobbySwitch = root.Bind("lobby-switch", () => { if (Launcher.instance != null) Launcher.instance.SwitchTeam(); });
        lobbyMain = root.Bind("lobby-main", () =>
        {
            if (Launcher.instance == null)
                return;
            if (PhotonNetwork.IsMasterClient)
                Launcher.instance.StartGame();
            else
                Launcher.instance.ToggleReady();
        });
    }

    void RefreshLobby()
    {
        Room room = PhotonNetwork.CurrentRoom;
        if (room == null || lobbyName == null)
            return;

        Player[] players = PhotonNetwork.PlayerList;
        lobbyName.text = room.Name;
        lobbyCount.text = players.Length + " / " + room.MaxPlayers + " PLAYERS";

        int used = 0;
        for (int i = 0; i < players.Length; i++)
        {
            Player p = players[i];
            int team = PlayerTeams.GetTeam(p);
            VisualElement row = PlayerRow(used++);
            VisualElement parent = team == Teams.Red ? teamRed : teamBlue;
            if (row.parent != parent)
                parent.Add(row);
            row.Q<Label>("name").text = p.NickName + (team == 0 ? "  <color=#9AA4B2>(assigning...)</color>" : "");
            row.EnableInClassList("player-row--you", p.IsLocal);
            bool ready = p.IsMasterClient || GameProps.GetBool(p.CustomProperties, GameProps.Ready);
            Label badge = row.Q<Label>("badge");
            badge.text = p.IsMasterClient ? "HOST" : ready ? "READY" : "NOT READY";
            badge.EnableInClassList("badge--host", p.IsMasterClient);
            badge.EnableInClassList("badge--ready", !p.IsMasterClient && ready);
        }
        for (int i = 0; i < playerRows.Count; i++)
            playerRows[i].SetVisible(i < used);

        // Empty seats make the lobby read as "waiting for N more" at a glance.
        lobbyProgress.style.width = Length.Percent(room.MaxPlayers > 0 ? 100f * players.Length / room.MaxPlayers : 0f);
        int perTeam = Mathf.Clamp(Mathf.CeilToInt(room.MaxPlayers / 2f), 1, 5);
        int blue = 0, red = 0;
        for (int i = 0; i < players.Length; i++)
        {
            int t = PlayerTeams.GetTeam(players[i]);
            if (t == Teams.Red) red++; else blue++;
        }
        int open = 0;
        open = FillOpenSlots(teamBlue, Mathf.Max(0, perTeam - blue), open);
        open = FillOpenSlots(teamRed, Mathf.Max(0, perTeam - red), open);
        for (int i = 0; i < openSlots.Count; i++)
            openSlots[i].SetVisible(i < open);

        Launcher launcher = Launcher.instance;
        if (launcher == null)
            return;
        bool master = PhotonNetwork.IsMasterClient;
        if (master)
        {
            bool canStart = launcher.CanStart(out string reason);
            lobbyMain.text = canStart ? "START MATCH" : "START";
            lobbyMain.SetEnabled(canStart);
            lobbyStatus.text = canStart ? "Everyone is ready." : reason;
        }
        else
        {
            bool ready = GameProps.GetBool(PhotonNetwork.LocalPlayer.CustomProperties, GameProps.Ready);
            lobbyMain.text = ready ? "READY!" : "READY UP";
            lobbyMain.SetEnabled(true);
            lobbyMain.EnableInClassList("btn--primary", !ready);
            lobbyStatus.text = ready ? "Waiting for the host to start..." : "Tap READY UP when you're set.";
        }

        int myTeam = PlayerTeams.GetTeam(PhotonNetwork.LocalPlayer);
        lobbySwitch.text = myTeam == 0 ? "SWITCH TEAM" : "JOIN " + Teams.Name(Teams.Other(myTeam));
        lobbySwitch.SetEnabled(PlayerTeams.CanSwitchLocalTeam());
    }

    int FillOpenSlots(VisualElement team, int count, int start)
    {
        for (int i = 0; i < count; i++)
        {
            while (openSlots.Count <= start + i)
            {
                VisualElement slot = new VisualElement { pickingMode = PickingMode.Ignore };
                slot.AddToClassList("player-row");
                slot.AddToClassList("player-row--open");
                Label label = new Label("OPEN SLOT") { pickingMode = PickingMode.Ignore };
                label.AddToClassList("player-row__name");
                slot.Add(label);
                openSlots.Add(slot);
            }
            VisualElement s = openSlots[start + i];
            team.Add(s); // re-adding moves it to the end, after the real players
        }
        return start + count;
    }

    VisualElement PlayerRow(int index)
    {
        while (playerRows.Count <= index)
        {
            VisualElement row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList("player-row");
            Label name = new Label { name = "name", pickingMode = PickingMode.Ignore };
            name.AddToClassList("player-row__name");
            Label badge = new Label { name = "badge", pickingMode = PickingMode.Ignore };
            badge.AddToClassList("badge");
            row.Add(name);
            row.Add(badge);
            teamBlue.Add(row);
            playerRows.Add(row);
        }
        return playerRows[index];
    }

    // ===================================================================== ERROR / SETTINGS / QUIT

    void OnError(string message)
    {
        if (errorText != null)
            errorText.text = message;
    }

    void OnErrorOk()
    {
        if (PhotonNetwork.InLobby)
            Open("TitleMenu");
        else
            Open("LoadingMenu"); // Launcher is already reconnecting
    }

    void OpenSettings()
    {
        if (settings != null)
            return;
        settings = SettingsPanel.Open(settingsHost, true, () => settings = null, () =>
        {
            if (Launcher.instance != null)
                Launcher.instance.ReconnectForRegionChange();
            GameRuntime.Toast("Server region: " + GameSettings.RegionLabel(GameSettings.Region));
        });
    }

    void CloseSettings()
    {
        if (settings == null)
            return;
        GameSettings.Save();
        settings.RemoveFromHierarchy();
        settings = null;
    }

    static void Quit()
    {
        if (PhotonNetwork.IsConnected)
            PhotonNetwork.Disconnect();
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    // ===================================================================== FRAME

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            OnBack();

        if (current == screens["LoadingMenu"])
        {
            spinAngle = (spinAngle + 360f * Time.deltaTime) % 360f;
            spinner.style.rotate = new Rotate(spinAngle);
            dotsTimer -= Time.unscaledDeltaTime;
            if (dotsTimer <= 0f)
            {
                dotsTimer = 0.4f;
                dots = (dots + 1) % 4;
                loadingText.text = "CONNECTING" + new string('.', dots);
            }
        }

        statusTimer -= Time.unscaledDeltaTime;
        if (statusTimer <= 0f)
        {
            statusTimer = 0.5f;
            bool online = PhotonNetwork.IsConnectedAndReady;
            statusDot.EnableInClassList("status__dot--online", online);
            statusText.text = online
                ? "ONLINE  •  " + (PhotonNetwork.CloudRegion ?? "").ToUpperInvariant().TrimEnd('/', '*') + "  •  " + PhotonNetwork.GetPing() + " MS"
                : PhotonNetwork.IsConnected ? "CONNECTING..." : "OFFLINE";
        }
    }

    /// <summary>Android back button.</summary>
    void OnBack()
    {
        if (settings != null)
        {
            CloseSettings();
            return;
        }
        string name = MenuManager.Current;
        if (name == "FindRoomMenu" || name == "CreateRoomMenu" || name == "ErrorMenu")
            Open(PhotonNetwork.InLobby ? "TitleMenu" : "LoadingMenu");
    }

    static Texture2D HorizontalShade()
    {
        Texture2D tex = new Texture2D(64, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int x = 0; x < 64; x++)
        {
            float t = x / 63f;
            tex.SetPixel(x, 0, new Color(0.02f, 0.03f, 0.05f, Mathf.Lerp(0.88f, 0f, t * t)));
        }
        tex.Apply(false, true);
        return tex;
    }
}
