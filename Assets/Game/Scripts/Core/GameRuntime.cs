using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Photon.Pun;

/// <summary>
/// Created automatically before the first scene loads (no scene setup needed).
/// Applies device settings (60 fps, no screen sleep), owns the UI audio source and the
/// persistent overlay (toast + FPS counter, Overlay.uxml), and adds the per-scene runtime
/// UI: MatchHUD in the match, settings button in the main menu, ResultsScreen in Results.
/// </summary>
public class GameRuntime : MonoBehaviour
{
    static GameRuntime instance;

    public static AudioSource UISource => instance != null ? instance.uiSource : null;

    AudioSource uiSource;
    Label toast, fps;
    float toastHideAt = -1f;
    float fpsTimer;
    int fpsFrames;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null)
            return;
        Screen.sleepTimeout = SleepTimeout.NeverSleep; // don't dim mid-match while only using the joystick
        GameSettings.Apply();
        GameObject go = new GameObject("[GameRuntime]");
        DontDestroyOnLoad(go);
        go.AddComponent<GameRuntime>();
    }

    void Awake()
    {
        instance = this;
        uiSource = gameObject.AddComponent<AudioSource>();
        uiSource.playOnAwake = false;
        uiSource.spatialBlend = 0f;
        uiSource.ignoreListenerPause = true;

        VisualElement root = GameUI.CreateDocument(transform, "Overlay", 1000).rootVisualElement;
        toast = root.Q<Label>("toast");
        fps = root.Q<Label>("fps");

        GameSettings.Changed += OnSettingsChanged;
        OnSettingsChanged();
    }

    void Start()
    {
        // Keep the connection a little while in the background (phone call, notification),
        // but not so long that the room waits a minute for a player who has gone.
        PhotonNetwork.KeepAliveInBackground = 30f;
    }

    void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;

    void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    void OnDestroy()
    {
        GameSettings.Changed -= OnSettingsChanged;
        if (instance == this)
            instance = null;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single)
            return;

        switch (scene.name)
        {
            case GameScenes.Match:
                MatchState.Reset();
                if (FindAnyObjectByType<MatchHUD>() == null)
                    new GameObject("MatchHUD").AddComponent<MatchHUD>();
                break;
            case GameScenes.MainMenu:
                if (FindAnyObjectByType<MainMenuUI>() == null && MainMenuUI.Available)
                    new GameObject("MainMenuUI").AddComponent<MainMenuUI>();
                break;
            case GameScenes.OnBoarding:
                if (FindAnyObjectByType<OnBoardingUI>() == null && OnBoardingUI.Available)
                    new GameObject("OnBoardingUI").AddComponent<OnBoardingUI>();
                break;
            case GameScenes.Results:
                if (FindAnyObjectByType<ResultsScreen>() == null)
                    new GameObject("ResultsScreen").AddComponent<ResultsScreen>();
                break;
        }
    }

    /// <summary>One-line message that survives scene changes (e.g. "Connection lost").</summary>
    public static void Toast(string message, float seconds = 3.5f)
    {
        if (instance == null || instance.toast == null || string.IsNullOrEmpty(message))
            return;
        instance.toast.text = message;
        instance.toast.AddToClassList("toast--show"); // USS transition fades it in/out
        instance.toastHideAt = Time.unscaledTime + seconds;
    }

    void OnSettingsChanged()
    {
        fps.SetVisible(GameSettings.ShowFps);
    }

    void Update()
    {
        if (toastHideAt > 0f && Time.unscaledTime >= toastHideAt)
        {
            toastHideAt = -1f;
            toast.RemoveFromClassList("toast--show");
        }

        if (!GameSettings.ShowFps)
            return;
        fpsFrames++;
        fpsTimer += Time.unscaledDeltaTime;
        if (fpsTimer >= 0.5f)
        {
            fps.text = Mathf.RoundToInt(fpsFrames / fpsTimer) + " FPS";
            fpsFrames = 0;
            fpsTimer = 0f;
        }
    }
}
