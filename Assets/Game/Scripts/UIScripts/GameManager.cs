using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;

/// <summary>
/// Back button (Escape on Android) leaves the match: first leave the Photon room,
/// THEN load the End scene. Loading a scene while still in the room left ghost
/// players behind for others and broke the main menu afterwards.
/// </summary>
public class GameManager : MonoBehaviourPunCallbacks
{
    [SerializeField] int endSceneIndex = 4;
    bool isLeaving;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            OpenScene4();
    }

    // Kept the original name so existing UI button bindings still work.
    public void OpenScene4()
    {
        if (isLeaving)
            return;
        isLeaving = true;

        if (PhotonNetwork.InRoom)
            PhotonNetwork.LeaveRoom();
        else
            SceneManager.LoadScene(endSceneIndex);
    }

    public override void OnLeftRoom()
    {
        if (isLeaving)
            SceneManager.LoadScene(endSceneIndex);
    }
}
