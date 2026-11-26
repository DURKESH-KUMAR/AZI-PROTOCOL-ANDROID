using UnityEngine;
using UnityEngine.Video;
using UnityEngine.SceneManagement;
using Photon.Pun;

public class VideoSceneLoader : MonoBehaviour
{
    [Header("Video Settings")]
    public VideoPlayer videoPlayer;

    [Header("Scene Settings")]
    public int nextSceneIndex = 1;

    bool loading;

    private void Start()
    {
        if (videoPlayer == null)
            videoPlayer = GetComponent<VideoPlayer>();

        if (videoPlayer != null)
        {
            videoPlayer.isLooping = false;
            videoPlayer.loopPointReached += OnVideoFinished;
            videoPlayer.Play();
        }
        else
        {
            Debug.LogError("VideoPlayer not assigned!");
        }
    }

    bool videoFinished;

    private void Update()
    {
        // If the master left during the video, the new master moves everyone on.
        if (videoFinished && !loading && PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient)
            OnVideoFinished(videoPlayer);
    }

    private void OnVideoFinished(VideoPlayer vp)
    {
        videoFinished = true;
        if (loading)
            return;

        if (PhotonNetwork.InRoom)
        {
            // Multiplayer (OnBoarding -> match): only the master switches scene, through Photon,
            // so all clients enter the match together and no spawn messages are lost.
            if (!PhotonNetwork.IsMasterClient)
                return;
            loading = true;
            PhotonNetwork.LoadLevel(nextSceneIndex);
        }
        else
        {
            loading = true;
            SceneManager.LoadScene(nextSceneIndex);
        }
    }

    private void OnDestroy()
    {
        if (videoPlayer != null)
            videoPlayer.loopPointReached -= OnVideoFinished;
    }
}
