using UnityEngine;
using UnityEngine.UIElements;
using Photon.Pun;
using Photon.Realtime;

/// <summary>
/// UI Toolkit version of the OnBoarding (pre-match) screen (Resources/UI/OnBoarding.uxml),
/// added automatically by GameRuntime. Replaces the scene's uGUI Canvas (random image +
/// "Loading..." text) with a deployment briefing in the game's style: full-screen art with
/// a slow zoom, image title, room, your team, team sizes, rotating gameplay tips and a progress
/// bar that switches to "waiting for host" when this phone is ready first.
/// Timing and the Photon scene switch stay in RandomLoadingScreen.
/// </summary>
public class OnBoardingUI : MonoBehaviour
{
    static readonly string[] Tips =
    {
        "Enemies show up on your radar only when they fire.",
        "Tap AIM for a steadier shot - look sensitivity drops while scoped.",
        "Moving and sprinting widen your crosshair. Stop to land your shots.",
        "After respawning you are protected for a moment - use it to reposition.",
        "Your team scores every time an enemy goes down. Stick together.",
        "An empty magazine reloads automatically - but reloading early is safer.",
        "Reach the score limit, or lead when time runs out, to win the match.",
        "The red arrow around your crosshair points to whoever is shooting you.",
    };

    const float TipInterval = 3.5f;

    RandomLoadingScreen loader;
    VisualElement root, art, briefing, progress, progressFill, progressSweep, teamCard, roomChip, versus;
    Label caption, roomName, teamName, blueCount, redCount, tip, progressLabel, progressPercent;

    int tipIndex;
    float tipTimer = TipInterval;
    float infoTimer;
    float dotsTimer;
    int dots;
    int shownPercent = -1;
    bool shownWaiting;
    float sweep;

    public static bool Available => GameUI.Load("OnBoarding") != null;

    void Awake()
    {
        HideLegacyCanvases();
        loader = FindAnyObjectByType<RandomLoadingScreen>();

        root = GameUI.CreateDocument(transform, "OnBoarding", 10).rootVisualElement;
        art = root.Q("art");
        briefing = root.Q("briefing");
        progress = root.Q("progress");
        progressFill = root.Q("progress-fill");
        progressSweep = root.Q("progress-sweep");
        teamCard = root.Q("team-card");
        roomChip = root.Q("room-chip");
        versus = root.Q("versus");
        caption = root.Q<Label>("caption");
        roomName = root.Q<Label>("room-name");
        teamName = root.Q<Label>("team-name");
        blueCount = root.Q<Label>("blue-count");
        redCount = root.Q<Label>("red-count");
        tip = root.Q<Label>("tip");
        progressLabel = root.Q<Label>("progress-label");
        progressPercent = root.Q<Label>("progress-percent");

        root.Q("shade").style.backgroundImage = Shade();

        Sprite image = loader != null ? loader.CurrentImage : null;
        if (image != null)
        {
            // On the root too, so the art also fills the notch strips outside the safe area.
            root.style.backgroundImage = new StyleBackground(image);
            root.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
            art.style.backgroundImage = new StyleBackground(image);
            caption.text = image.name.ToUpperInvariant();
        }
        else
        {
            root.style.backgroundColor = new Color(0.02f, 0.03f, 0.05f);
            caption.text = "";
        }

        tipIndex = Random.Range(0, Tips.Length);
        tip.text = Tips[tipIndex];
        RefreshInfo();

        // Entrance: briefing fades up, art starts its slow zoom.
        root.schedule.Execute(() =>
        {
            briefing.AddToClassList("briefing--in");
            art.AddToClassList("art--zoom");
        });
    }

    /// <summary>Stop the old canvas drawing; its scripts (LoadingTextAnimator) are harmless.</summary>
    static void HideLegacyCanvases()
    {
        foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (canvas.isRootCanvas)
                canvas.enabled = false;
        // Its text is hidden now; disabling also stops its dot-animation coroutine.
        LoadingTextAnimator dotsAnimator = FindAnyObjectByType<LoadingTextAnimator>();
        if (dotsAnimator != null)
            dotsAnimator.enabled = false;
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        UpdateProgress(dt);

        tipTimer -= dt;
        if (tipTimer <= 0f)
        {
            tipTimer = TipInterval;
            tip.AddToClassList("tip__text--out"); // fade out, swap, fade in
            tip.schedule.Execute(() =>
            {
                tipIndex = (tipIndex + 1) % Tips.Length;
                tip.text = Tips[tipIndex];
                tip.RemoveFromClassList("tip__text--out");
            }).StartingIn(250);
        }

        infoTimer -= dt;
        if (infoTimer <= 0f)
        {
            infoTimer = 0.5f;
            RefreshInfo();
        }
    }

    void UpdateProgress(float dt)
    {
        bool waiting = loader != null && loader.WaitingForHost;
        if (waiting != shownWaiting)
        {
            shownWaiting = waiting;
            progress.EnableInClassList("progress--waiting", waiting);
            progressPercent.text = waiting ? "" : progressPercent.text;
        }

        dotsTimer -= dt;
        if (dotsTimer <= 0f)
        {
            dotsTimer = 0.4f;
            dots = (dots + 1) % 4;
            string state = waiting ? "WAITING FOR HOST" : loader != null && loader.Progress >= 1f ? "ENTERING MATCH" : "LOADING";
            progressLabel.text = state + new string('.', dots);
        }

        if (waiting)
        {
            sweep = (sweep + dt * 0.8f) % 1f;
            progressSweep.style.left = Length.Percent(Mathf.Lerp(-22f, 100f, sweep));
            return;
        }

        int percent = Mathf.RoundToInt((loader != null ? loader.Progress : 1f) * 100f);
        if (percent != shownPercent)
        {
            shownPercent = percent;
            progressFill.style.width = Length.Percent(percent);
            progressPercent.text = percent + "%";
        }
    }

    void RefreshInfo()
    {
        Room room = PhotonNetwork.CurrentRoom;
        bool online = room != null;
        roomChip.SetVisible(online);
        teamCard.SetVisible(online);
        versus.SetVisible(online);
        if (!online)
            return;

        roomName.text = room.Name;

        int myTeam = PlayerTeams.GetTeam(PhotonNetwork.LocalPlayer);
        teamName.text = myTeam > 0 ? Teams.Name(myTeam) : "ASSIGNING...";
        teamName.style.color = Teams.Color(myTeam);
        teamCard.EnableInClassList("team-card--blue", myTeam == Teams.Blue);
        teamCard.EnableInClassList("team-card--red", myTeam == Teams.Red);

        int blue = 0, red = 0;
        foreach (Player p in PhotonNetwork.PlayerList)
        {
            int t = PlayerTeams.GetTeam(p);
            if (t == Teams.Blue) blue++;
            else if (t == Teams.Red) red++;
        }
        blueCount.text = blue.ToString();
        redCount.text = red.ToString();
    }

    /// <summary>Dark at the bottom (text), light in the middle (art), slightly dark at the top (brand).</summary>
    static Texture2D Shade()
    {
        Texture2D tex = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 64; y++)
        {
            float t = y / 63f; // 0 = bottom, 1 = top
            float a = t < 0.55f ? Mathf.Lerp(0.92f, 0.1f, Mathf.SmoothStep(0f, 1f, t / 0.55f))
                                : Mathf.Lerp(0.1f, 0.55f, Mathf.SmoothStep(0f, 1f, (t - 0.55f) / 0.45f));
            tex.SetPixel(0, y, new Color(0.01f, 0.02f, 0.04f, a));
        }
        tex.Apply(false, true);
        return tex;
    }
}
