using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Photon.Pun;
using Photon.Realtime;

/// <summary>
/// Results page (Resources/UI/Results.uxml + Results.uss, rows from ResultRow.uxml).
/// Shown whenever a match ends: the master loads it for everyone at match end, and a
/// player who leaves early gets it with their stats at that moment.
/// Shows VICTORY / DEFEAT / DRAW, count-up team scores, both rosters with K / D / K-D,
/// the MVP and your own line. The room is left as soon as the snapshot is taken, so the
/// buttons react instantly and nobody keeps a finished room alive.
/// </summary>
public class ResultsScreen : MonoBehaviourPunCallbacks
{
    MatchResult result;
    VisualElement root;
    Label banner, blueValue, redValue, you;
    readonly System.Collections.Generic.List<VisualElement> rows = new System.Collections.Generic.List<VisualElement>();

    bool leavingRoom;
    string pendingScene;

    void Start()
    {
        UnityEngine.Cursor.visible = true;
        UnityEngine.Cursor.lockState = CursorLockMode.None;
        PlayerManager.InputBlocked = false;
        EnsureCamera();

        result = MatchResults.Pending ?? MatchResults.Capture(leftEarly: false);
        MatchResults.Pending = null;
        PlaceUnassignedPlayers(result);

        if (PhotonNetwork.InRoom)
        {
            leavingRoom = true;
            PhotonNetwork.LeaveRoom();
        }

        root = GameUI.CreateDocument(transform, "Results", 10).rootVisualElement;
        Fill();
        StartCoroutine(Animate());
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            Go(GameScenes.MainMenu);
    }

    // ---------------------------------------------------------------- content

    void Fill()
    {
        bool hasData = result != null;
        int winner = hasData ? result.winner : 0;
        int localTeam = hasData ? result.localTeam : 0;
        Color glow = winner == 0 ? GameUI.Accent : Teams.Color(winner);

        // Background on the document root so it still covers the notch strips
        // (the "results" content itself is inset to the safe area).
        root.style.backgroundImage = GameUI.VerticalGradient(new Color(0.02f, 0.03f, 0.05f), Color.Lerp(new Color(0.02f, 0.03f, 0.05f), glow, 0.28f));
        root.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
        root.Q("line").style.backgroundColor = glow;

        string title;
        Color titleColor;
        if (!hasData) { title = "MATCH OVER"; titleColor = GameUI.TextColor; }
        else if (result.leftEarly) { title = "YOU LEFT"; titleColor = GameUI.MutedText; }
        else if (winner == 0) { title = "DRAW"; titleColor = GameUI.TextColor; }
        else if (localTeam == 0) { title = Teams.Name(winner) + " WINS"; titleColor = Teams.Color(winner); }
        else if (result.LocalWon) { title = "VICTORY"; titleColor = GameUI.Accent; }
        else { title = "DEFEAT"; titleColor = Teams.RedColor; }

        banner = root.Q<Label>("banner");
        banner.text = title;
        banner.style.color = titleColor;

        root.Q<Label>("reason").text = !hasData ? "NO MATCH DATA"
            : result.leftEarly ? "SCORE WHEN YOU LEFT THE MATCH"
            : result.endReason == GameProps.EndReasonScore ? "SCORE LIMIT REACHED" : "TIME'S UP";

        blueValue = root.Q<Label>("blue-value");
        redValue = root.Q<Label>("red-value");
        StyleScore("blue", Teams.Blue, winner);
        StyleScore("red", Teams.Red, winner);

        MatchResult.Entry mvp = default;
        bool hasMvp = false;
        if (hasData)
        {
            foreach (MatchResult.Entry e in result.players) // already sorted by kills
            {
                if (e.kills > 0) { mvp = e; hasMvp = true; }
                break;
            }
        }

        Roster(root.Q("blue-roster"), Teams.Blue, hasMvp ? mvp.actor : -1);
        Roster(root.Q("red-roster"), Teams.Red, hasMvp ? mvp.actor : -1);

        you = root.Q<Label>("you");
        you.text = "";
        if (hasData)
        {
            foreach (MatchResult.Entry e in result.players)
            {
                if (e.actor != result.localActor)
                    continue;
                you.text = "YOUR MATCH:   <color=#FFB830>" + e.kills + "</color> KILLS    <color=#FFB830>" + e.deaths
                    + "</color> DEATHS    K/D <color=#FFB830>" + e.Ratio.ToString("0.00") + "</color>"
                    + (hasMvp && mvp.actor == e.actor ? "    <color=#FFD54A>[MVP]</color>" : "");
            }
        }

        root.Bind("menu", () => Go(GameScenes.MainMenu));
        root.Bind("quit", Quit);
    }

    void StyleScore(string prefix, int team, int winner)
    {
        Color c = Teams.Color(team);
        root.Q<Label>(prefix + "-label").style.color = c;
        root.Q<Label>(prefix + "-label").text = Teams.Name(team) + (winner == team ? "  WINS" : "");
        root.Q<Label>(prefix + "-value").style.color = c;
        root.Q(prefix + "-score").EnableInClassList("score--lost", winner != 0 && winner != team);
    }

    /// <summary>
    /// Every player (and their kills) must appear: anyone without a team is listed in the
    /// smaller roster instead of being dropped.
    /// </summary>
    static void PlaceUnassignedPlayers(MatchResult r)
    {
        if (r == null)
            return;
        int blue = 0, red = 0;
        foreach (MatchResult.Entry e in r.players)
        {
            if (e.team == Teams.Blue) blue++;
            else if (e.team == Teams.Red) red++;
        }
        for (int i = 0; i < r.players.Count; i++)
        {
            MatchResult.Entry e = r.players[i];
            if (e.team == Teams.Blue || e.team == Teams.Red)
                continue;
            e.team = blue <= red ? Teams.Blue : Teams.Red;
            if (e.team == Teams.Blue) blue++; else red++;
            r.players[i] = e;
        }
    }

    void Roster(VisualElement panel, int team, int mvpActor)
    {
        AddRow(panel, Teams.Name(team) + " TEAM", "K", "D", "K/D").AddToClassList("result-row--header");

        int count = 0;
        if (result != null)
        {
            foreach (MatchResult.Entry e in result.players)
            {
                if (e.team != team || count >= 6)
                    continue;
                count++;
                bool isYou = e.actor == result.localActor;
                string name = e.name + (isYou ? "  <color=#9AA4B2>(you)</color>" : "") + (e.actor == mvpActor ? "  <color=#FFD54A>[MVP]</color>" : "");
                VisualElement row = AddRow(panel, name, e.kills.ToString(), e.deaths.ToString(), e.Ratio.ToString("0.00"));
                if (isYou)
                    row.AddToClassList("result-row--you");
                rows.Add(row);
            }
        }

        if (count == 0)
        {
            Label none = new Label("No players") { pickingMode = PickingMode.Ignore };
            none.AddToClassList("results__empty");
            panel.Add(none);
        }
    }

    static VisualElement AddRow(VisualElement panel, string name, string k, string d, string kd)
    {
        VisualElement template = GameUI.Instantiate("ResultRow", panel);
        template.RemoveFromClassList("fill"); // rows stack; they are not full-screen
        VisualElement row = template.Q("row");
        row.Q<Label>("name").text = name;
        row.Q<Label>("kills").text = k;
        row.Q<Label>("deaths").text = d;
        row.Q<Label>("ratio").text = kd;
        return row;
    }

    IEnumerator Animate()
    {
        bool good = result != null && !result.leftEarly && (result.LocalWon || (result.localTeam == 0 && result.winner != 0));
        GameAudio.PlayUI(good ? GameAudio.Victory : GameAudio.Defeat, 0.9f);

        yield return null; // let the first layout happen so transitions start from the USS state
        banner.AddToClassList("results__banner--in");
        yield return new WaitForSeconds(0.45f);

        int blue = result != null ? result.blueScore : 0, red = result != null ? result.redScore : 0;
        int lastBlue = -1, lastRed = -1;
        for (float t = 0f; t < 1.2f; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / 1.2f);
            int b = Mathf.RoundToInt(blue * k), r = Mathf.RoundToInt(red * k);
            if (b != lastBlue || r != lastRed)
            {
                if (lastBlue >= 0)
                    GameAudio.PlayUI(GameAudio.Click, 0.25f);
                lastBlue = b;
                lastRed = r;
                blueValue.text = b.ToString();
                redValue.text = r.ToString();
            }
            yield return null;
        }
        blueValue.text = blue.ToString();
        redValue.text = red.ToString();

        WaitForSeconds step = new WaitForSeconds(0.09f);
        foreach (VisualElement row in rows)
        {
            row.AddToClassList("result-row--in"); // USS slide/fade
            yield return step;
        }
        you.AddToClassList("results__you--in");
    }

    // ---------------------------------------------------------------- navigation

    void Go(string scene)
    {
        if (pendingScene != null)
            return;
        pendingScene = scene;
        if (!leavingRoom)
            SceneManager.LoadScene(scene);
        else
            Invoke(nameof(LoadPending), 3f); // OnLeftRoom normally loads it first; never get stuck
    }

    void LoadPending()
    {
        if (pendingScene != null)
            SceneManager.LoadScene(pendingScene);
    }

    void Quit()
    {
        if (PhotonNetwork.IsConnected)
            PhotonNetwork.Disconnect();
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    public override void OnLeftRoom()
    {
        leavingRoom = false;
        if (pendingScene != null)
            SceneManager.LoadScene(pendingScene);
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        leavingRoom = false;
        if (pendingScene != null)
            SceneManager.LoadScene(pendingScene);
    }

    /// <summary>The scene has no camera of its own; a cheap clear-only camera avoids a stale frame on device.</summary>
    static void EnsureCamera()
    {
        if (Camera.main != null)
            return;
        GameObject go = new GameObject("Results Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
        Camera cam = go.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.02f, 0.03f, 0.05f);
        cam.cullingMask = 0;
    }
}
