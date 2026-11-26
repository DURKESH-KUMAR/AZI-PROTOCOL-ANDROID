using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;
using Photon.Realtime;

/// <summary>
/// Match exits. Every way out of a match ends on the Results page:
/// - match ends normally: the master loads Results for everyone (ScoreBoard);
/// - player leaves early (pause menu / UI button): stats are snapshotted, the room is left,
///   then Results shows "YOU LEFT THE MATCH" with the score at that moment;
/// - connection lost: back to the main menu with a message (no room data is left to show).
/// The Android back button opens/closes the pause menu.
/// </summary>
public class GameManager : MonoBehaviourPunCallbacks
{
    bool isLeaving;

    void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape))
            return;
        if (MatchHUD.Instance != null)
            MatchHUD.Instance.OnBackPressed();
        else
            OpenScene4();
    }

    // Kept the original name so existing UI button bindings still work.
    public void OpenScene4()
    {
        if (isLeaving)
            return;
        isLeaving = true;
        PlayerManager.InputBlocked = false;

        if (PhotonNetwork.InRoom)
        {
            MatchResults.Pending = MatchResults.Capture(leftEarly: !MatchState.IsOver);
            PhotonNetwork.LeaveRoom(); // OnLeftRoom loads Results
        }
        else
        {
            SceneManager.LoadScene(GameScenes.AfterMatch);
        }
    }

    /// <summary>Leave from code that has no GameManager reference (e.g. the HUD).</summary>
    public static void LeaveToResults()
    {
        GameManager manager = FindAnyObjectByType<GameManager>();
        if (manager != null)
        {
            manager.OpenScene4();
            return;
        }
        if (PhotonNetwork.InRoom)
            MatchResults.Pending = MatchResults.Capture(leftEarly: !MatchState.IsOver);
        SceneManager.LoadScene(GameScenes.AfterMatch); // ResultsScreen leaves the room
    }

    public override void OnLeftRoom()
    {
        if (isLeaving)
            SceneManager.LoadScene(GameScenes.AfterMatch);
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        PlayerManager.InputBlocked = false;
        if (isLeaving)
        {
            SceneManager.LoadScene(GameScenes.AfterMatch);
            return;
        }
        isLeaving = true;
        GameRuntime.Toast("Connection lost (" + cause + "). Reconnecting...", 4f);
        // Launcher in the main menu reconnects and rejoins the lobby.
        SceneManager.LoadScene(GameScenes.MainMenu);
    }
}
