using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Photon.Pun;
using Hashtable = ExitGames.Client.Photon.Hashtable;

/// <summary>
/// Match countdown (lives in the match scene).
/// The master publishes a shared start time = now + warm-up, so every client shows the same
/// time and slow-loading phones get a few seconds to arrive before the shooting starts.
/// When the time runs out the master ends the match through ScoreBoard (banner -> Results).
/// </summary>
public class TimerScript : MonoBehaviourPunCallbacks
{
    public static TimerScript Instance { get; private set; }

    const string StartTimeKey = GameProps.MatchStart;

    public float timeRemaining = 60f; // Match duration in seconds (scene value: 600)
    public Text timerText;
    [Tooltip("Pre-match countdown in seconds (players can move but not shoot).")]
    [SerializeField] float warmupSeconds = 5f;
    [Tooltip("Timer text turns this colour in the final seconds.")]
    [SerializeField] Color warningColor = new Color(1f, 0.35f, 0.3f);
    [SerializeField] float warningSeconds = 30f;

    float duration;
    bool timerIsRunning = true;
    int lastShownSeconds = -1;
    Color normalColor = Color.white;

    bool hasStartTime;
    double startTime;

    public float Duration => duration;

    /// <summary>True until the shared start time is reached (or while it is still unknown).</summary>
    public bool IsWarmup => PhotonNetwork.InRoom && (!hasStartTime || PhotonNetwork.Time < startTime);

    public float WarmupRemaining => hasStartTime ? Mathf.Max(0f, (float)(startTime - PhotonNetwork.Time)) : warmupSeconds;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Start()
    {
        duration = timeRemaining;
        if (timerText != null)
            normalColor = timerText.color;

        if (PhotonNetwork.IsMasterClient && PhotonNetwork.CurrentRoom != null)
        {
            startTime = PhotonNetwork.Time + warmupSeconds;
            hasStartTime = true;
            PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { StartTimeKey, startTime } });
        }
        else
        {
            ReadStartTime();
        }
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        if (propertiesThatChanged.ContainsKey(StartTimeKey))
            ReadStartTime();
    }

    void ReadStartTime()
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(StartTimeKey, out object start) &&
            start is double value)
        {
            startTime = value;
            hasStartTime = true;
        }
    }

    void Update()
    {
        if (!timerIsRunning)
            return;

        if (MatchState.IsOver)
        {
            timerIsRunning = false;
            return;
        }

        bool inRoom = PhotonNetwork.InRoom;
        if (inRoom)
        {
            if (hasStartTime)
                timeRemaining = Mathf.Clamp(duration - (float)(PhotonNetwork.Time - startTime), 0f, duration);
            // else: still waiting for the master's start time - keep showing the full duration.
        }
        else
        {
            timeRemaining = Mathf.Max(0f, timeRemaining - Time.deltaTime);
        }

        UpdateTimerDisplay(timeRemaining);

        if (timeRemaining > 0f)
            return;

        if (!inRoom)
        {
            timerIsRunning = false;
            SceneManager.LoadScene(GameScenes.AfterMatch);
        }
        else if (PhotonNetwork.IsMasterClient)
        {
            // If the master left, the new master's timer reaches here instead.
            timerIsRunning = false;
            if (ScoreBoard.instance != null)
                ScoreBoard.instance.EndMatch(GameProps.EndReasonTime);
            else
                PhotonNetwork.LoadLevel(GameScenes.AfterMatch);
        }
    }

    void UpdateTimerDisplay(float timeToDisplay)
    {
        int totalSeconds = Mathf.CeilToInt(timeToDisplay);
        if (totalSeconds == lastShownSeconds)
            return; // only rebuild the string when the shown second changes
        lastShownSeconds = totalSeconds;
        if (timerText == null)
            return;
        timerText.text = string.Format("{0:00}:{1:00}", totalSeconds / 60, totalSeconds % 60);
        bool warning = !IsWarmup && totalSeconds <= warningSeconds;
        timerText.color = warning ? warningColor : normalColor;
        if (warning && totalSeconds <= 10 && totalSeconds > 0)
            GameAudio.PlayUI(GameAudio.Beep, 0.35f);
    }
}
