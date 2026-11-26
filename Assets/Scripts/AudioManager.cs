using UnityEngine;

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

    private void Awake()
    {
        // Prevent duplicate AudioManagers
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Keep AudioManager alive across scenes
        DontDestroyOnLoad(gameObject);

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.loop = loopMusic;
        audioSource.volume = volume;
        audioSource.spatialBlend = 0f;

        if (backgroundMusic != null)
        {
            audioSource.clip = backgroundMusic;
        }

        if (playOnStart && audioSource.clip != null)
        {
            audioSource.Play();
        }
    }

    public void PlayMusic(AudioClip newMusic)
    {
        if (newMusic == null) return;

        if (audioSource.clip == newMusic &&
            audioSource.isPlaying)
        {
            return;
        }

        audioSource.Stop();
        audioSource.clip = newMusic;
        audioSource.Play();
    }

    public void StopMusic()
    {
        audioSource.Stop();
    }

    public void PauseMusic()
    {
        audioSource.Pause();
    }

    public void ResumeMusic()
    {
        audioSource.UnPause();
    }

    public void SetVolume(float newVolume)
    {
        volume = Mathf.Clamp01(newVolume);
        audioSource.volume = volume;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
