using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;

public class SceneBehaviour : MonoBehaviour
{
    public void Start()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void LoadScene()
    {
        // Safety net: never arrive at the menu still inside a room.
        // (Launcher.Start also handles this case and rejoins the lobby.)
        if (PhotonNetwork.InRoom)
            PhotonNetwork.LeaveRoom();
        SceneManager.LoadScene("MainMenu");
    }

    public void Quit()
    {
        if (PhotonNetwork.IsConnected)
            PhotonNetwork.Disconnect();
        Application.Quit();
    }
}
