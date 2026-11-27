using UnityEngine;

/// <summary>
/// Background music, kept alive across scenes (created in the Loading scene).
/// The music is "ducked" (faded down) during a match so gunfire, footsteps and call-outs
/// stay clear, and faded back up in the menus. GameRuntime calls <see cref="SetDucked"/>
/// on every scene change.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Header("Audio Settings")]
    public AudioSource audioSource;
    public AudioClip backgroundMusic;

    [Range(0f, 1f)]
    public float volume = 1f;

    [Header("Playback Settings")]
    public bool playOnStart = true;
    public bool loopMusic = true;

    [Header("Ducking")]
    [Tooltip("Music volume multiplier during a match.")]
    [Range(0f, 1f)] public float duckedVolume = 0.25f;
    [Tooltip("Seconds to fade between normal and ducked volume.")]
    public float fadeTime = 1.2f;

    float duckFactor = 1f;
    float duckTarget = 1f;

    void Awake()
    {
        // Prevent duplicate AudioManagers
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject); // keep the music playing across scenes

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.loop = loopMusic;
        audioSource.spatialBlend = 0f;
        ApplyVolume();

        if (backgroundMusic != null)
            audioSource.clip = backgroundMusic;

        if (playOnStart && audioSource.clip != null)
            audioSource.Play();
    }

    void Update()
    {
        if (Mathf.Approximately(duckFactor, duckTarget))
            return;
        duckFactor = Mathf.MoveTowards(duckFactor, duckTarget, Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeTime));
        ApplyVolume();
    }

    void ApplyVolume() => audioSource.volume = volume * duckFactor;

    /// <summary>Fade the music down (match) or back up (menus).</summary>
    public void SetDucked(bool ducked)
    {
        duckTarget = ducked ? duckedVolume : 1f;
    }

    public void PlayMusic(AudioClip newMusic)
    {
        if (newMusic == null)
            return;
        if (audioSource.clip == newMusic && audioSource.isPlaying)
            return;

        audioSource.Stop();
        audioSource.clip = newMusic;
        audioSource.Play();
    }

    public void StopMusic() => audioSource.Stop();

    public void PauseMusic() => audioSource.Pause();

    public void ResumeMusic() => audioSource.UnPause();

    public void SetVolume(float newVolume)
    {
        volume = Mathf.Clamp01(newVolume);
        ApplyVolume();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
