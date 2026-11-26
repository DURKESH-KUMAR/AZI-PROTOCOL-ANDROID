using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Photon.Pun;
using Hashtable = ExitGames.Client.Photon.Hashtable;

/// <summary>
/// Match countdown (lives in the game scene). Class name now matches the file name
/// (TimerScript.cs) so Unity can load it as a component.
/// In a room the start time is shared through a room property, so every client shows
/// the same time, and only the master client changes scene via PhotonNetwork.LoadLevel
/// (scene sync takes everyone along together).
/// </summary>
public class TimerScript : MonoBehaviour
{
    const string StartTimeKey = "MatchStart";

    public float timeRemaining = 60f; // Match duration in seconds (scene value: 600)
    public Text timerText;
    [Tooltip("Scene loaded when the timer ends. 4 = End. (It used to reload scene 3, the match itself.)")]
    [SerializeField] int nextSceneIndex = 4;

    float duration;
    bool timerIsRunning = true;
    int lastShownSeconds = -1;

    void Start()
    {
        duration = timeRemaining;
        if (PhotonNetwork.IsMasterClient && PhotonNetwork.CurrentRoom != null)
            PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { StartTimeKey, PhotonNetwork.Time } });
    }

    void Update()
    {
        if (!timerIsRunning)
            return;

        if (PhotonNetwork.InRoom)
        {
            if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(StartTimeKey, out object start) && start is double startTime)
                timeRemaining = Mathf.Max(0f, duration - (float)(PhotonNetwork.Time - startTime));
            // else: still waiting for the master's start time - keep showing the full duration.
        }
        else
        {
            timeRemaining = Mathf.Max(0f, timeRemaining - Time.deltaTime);
        }

        UpdateTimerDisplay(timeRemaining);

        if (timeRemaining > 0f)
            return;

        if (!PhotonNetwork.InRoom)
        {
            timerIsRunning = false;
            SceneManager.LoadScene(nextSceneIndex);
        }
        else if (PhotonNetwork.IsMasterClient)
        {
            // If the master left, the new master's timer reaches here instead.
            timerIsRunning = false;
            PhotonNetwork.LoadLevel(nextSceneIndex);
        }
    }

    void UpdateTimerDisplay(float timeToDisplay)
    {
        int totalSeconds = Mathf.CeilToInt(timeToDisplay);
        if (totalSeconds == lastShownSeconds)
            return; // don't allocate a new string every frame
        lastShownSeconds = totalSeconds;
        if (timerText != null)
            timerText.text = string.Format("{0:00}:{1:00}", totalSeconds / 60, totalSeconds % 60);
    }
}
