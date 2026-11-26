using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Photon.Pun;

/// <summary>
/// OnBoarding (pre-match) screen logic: picks a random loading image, waits
/// <see cref="loadingDuration"/> seconds, then moves on to the next build scene (the match).
/// In multiplayer only the master switches, through Photon, so every client enters the match
/// together and no player-spawn messages are lost. Exposes its state (image, progress,
/// waiting) so the UI Toolkit screen (OnBoardingUI) can display it; the legacy uGUI Image
/// is still filled if assigned.
/// </summary>
public class RandomLoadingScreen : MonoBehaviour
{
    [Header("Loading Screen Images")]
    [SerializeField]
    private List<Sprite> loadingImages = new List<Sprite>();

    [Header("UI Reference")]
    [SerializeField]
    private Image loadingImage;

    [Header("Loading Settings")]
    [SerializeField]
    private float loadingDuration = 8f;

    [SerializeField]
    private bool useUnscaledTime = true;

    [Header("Scene Settings")]
    [SerializeField]
    private bool loadNextScene = true;

    int nextSceneIndex;
    float elapsed;
    bool done;
    bool loadStarted;

    /// <summary>The image chosen for this visit (null if none are assigned).</summary>
    public Sprite CurrentImage { get; private set; }

    /// <summary>0..1 over the loading duration.</summary>
    public float Progress => loadingDuration > 0f ? Mathf.Clamp01(elapsed / loadingDuration) : 1f;

    /// <summary>Timer finished, but this client waits for the master to start the match.</summary>
    public bool WaitingForHost => done && !loadStarted;

    void Awake()
    {
        // Chosen in Awake so the UI can show it on its very first frame.
        if (loadingImages != null && loadingImages.Count > 0)
            CurrentImage = loadingImages[Random.Range(0, loadingImages.Count)];

        if (loadingImage != null && CurrentImage != null)
        {
            loadingImage.sprite = CurrentImage;
            loadingImage.enabled = true;
        }
    }

    void Start()
    {
        nextSceneIndex = SceneManager.GetActiveScene().buildIndex + 1;
    }

    void Update()
    {
        if (!done)
        {
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            if (elapsed < loadingDuration)
                return;
            done = true;
            if (!loadNextScene)
                return;
        }

        // Runs once when the timer ends, and keeps running for non-masters: if the master left
        // while everyone waited here, the new master moves the room on.
        if (!loadStarted && loadNextScene)
            TryLoadNext();
    }

    void TryLoadNext()
    {
        if (nextSceneIndex < 0 || nextSceneIndex >= SceneManager.sceneCountInBuildSettings)
        {
            Debug.LogWarning("Next scene not found in Build Settings!");
            loadStarted = true;
            return;
        }

        if (PhotonNetwork.InRoom)
        {
            if (!PhotonNetwork.IsMasterClient)
                return; // wait: the master's LoadLevel brings this client along
            loadStarted = true;
            PhotonNetwork.LoadLevel(nextSceneIndex);
        }
        else
        {
            loadStarted = true;
            SceneManager.LoadScene(nextSceneIndex);
        }
    }
}
