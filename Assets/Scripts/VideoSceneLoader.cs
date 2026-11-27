using UnityEngine;
using UnityEngine.Video;
using UnityEngine.SceneManagement;
using Photon.Pun;

/// <summary>
/// Plays a video, then loads the next scene. Never gets stuck: the scene also moves on if
/// the video fails to load/play on the device, or if it simply takes too long, and a tap
/// skips it (after a short moment, so a stray touch doesn't).
/// In a Photon room only the master switches scene (PhotonNetwork.LoadLevel), so every client
/// moves together and no spawn messages are lost.
/// </summary>
public class VideoSceneLoader : MonoBehaviour
{
    [Header("Video Settings")]
    public VideoPlayer videoPlayer;

    [Header("Scene Settings")]
    public int nextSceneIndex = 1;

    [Header("Safety")]
    [Tooltip("Tap / click to skip the video (offline only - in a room the master decides).")]
    public bool allowSkip = true;
    [Tooltip("Seconds before a tap can skip.")]
    public float skipAfter = 1f;
    [Tooltip("Move on after this many seconds even if the video never reports its end (0 = video length + 2s).")]
    public float maxWait = 0f;

    bool loading;
    bool videoFinished;
    float startTime;
    float deadline;

    void Start()
    {
        startTime = Time.unscaledTime;
        deadline = startTime + (maxWait > 0f ? maxWait : 30f); // until the real length is known

        if (videoPlayer == null)
            videoPlayer = GetComponent<VideoPlayer>();

        if (videoPlayer == null)
        {
            Debug.LogWarning("[VideoSceneLoader] No VideoPlayer - skipping straight to the next scene.");
            Finish();
            return;
        }

        videoPlayer.isLooping = false;
        videoPlayer.skipOnDrop = true; // slow phones drop frames instead of slowing the video down
        videoPlayer.loopPointReached += OnVideoFinished;
        videoPlayer.errorReceived += OnVideoError;
        videoPlayer.prepareCompleted += OnPrepared;
        videoPlayer.Play();
    }

    void OnPrepared(VideoPlayer vp)
    {
        if (maxWait <= 0f && vp.length > 0)
            deadline = startTime + (float)vp.length + 2f;
    }

    void OnVideoError(VideoPlayer vp, string message)
    {
        Debug.LogWarning("[VideoSceneLoader] Video error (" + message + ") - continuing without it.");
        Finish();
    }

    void OnVideoFinished(VideoPlayer vp) => Finish();

    void Update()
    {
        if (!videoFinished)
        {
            if (Time.unscaledTime >= deadline)
                Finish();
            else if (allowSkip && !PhotonNetwork.InRoom && Time.unscaledTime - startTime >= skipAfter && Input.GetMouseButtonDown(0))
                Finish(); // also fires for the first touch on phones
            return;
        }

        // If the master left during the video, the new master moves everyone on.
        if (!loading && PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient)
            Finish();
    }

    void Finish()
    {
        videoFinished = true;
        if (loading)
            return;

        if (PhotonNetwork.InRoom)
        {
            if (!PhotonNetwork.IsMasterClient)
                return; // the master's LoadLevel brings this client along
            loading = true;
            PhotonNetwork.LoadLevel(nextSceneIndex);
        }
        else
        {
            loading = true;
            if (videoPlayer != null)
                videoPlayer.Stop();
            SceneManager.LoadScene(nextSceneIndex);
        }
    }

    void OnDestroy()
    {
        if (videoPlayer == null)
            return;
        videoPlayer.loopPointReached -= OnVideoFinished;
        videoPlayer.errorReceived -= OnVideoError;
        videoPlayer.prepareCompleted -= OnPrepared;
    }
}
