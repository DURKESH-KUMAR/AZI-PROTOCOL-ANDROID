using System;
using UnityEngine;

/// <summary>
/// Player options, persisted in PlayerPrefs. Values are cached in memory, so reading them
/// every frame (e.g. look sensitivity) costs nothing. Call <see cref="Save"/> when a
/// settings screen closes, not on every slider tick.
/// </summary>
public static class GameSettings
{
    const string Prefix = "settings.";

    public static event Action Changed;

    static bool loaded;
    static float lookSensitivity, scopeSensitivity, masterVolume;
    static int qualityLevel;
    static bool highFrameRate, vibration, showFps;
    static string region;

    /// <summary>Multiplier on the camera joystick (1 = default feel).</summary>
    public static float LookSensitivity { get { Load(); return lookSensitivity; } set { Load(); lookSensitivity = Mathf.Clamp(value, 0.1f, 4f); PlayerPrefs.SetFloat(Prefix + "look", lookSensitivity); Notify(); } }

    /// <summary>Extra multiplier while scoped (lower = steadier aim at high zoom).</summary>
    public static float ScopeSensitivity { get { Load(); return scopeSensitivity; } set { Load(); scopeSensitivity = Mathf.Clamp(value, 0.05f, 1f); PlayerPrefs.SetFloat(Prefix + "scope", scopeSensitivity); Notify(); } }

    public static float MasterVolume { get { Load(); return masterVolume; } set { Load(); masterVolume = Mathf.Clamp01(value); PlayerPrefs.SetFloat(Prefix + "volume", masterVolume); Notify(); } }

    /// <summary>Index into QualitySettings.names, or -1 to keep the project default.</summary>
    public static int QualityLevel { get { Load(); return qualityLevel; } set { Load(); qualityLevel = value; PlayerPrefs.SetInt(Prefix + "quality", qualityLevel); Notify(); } }

    /// <summary>60 fps when true, 30 fps (battery saver) when false.</summary>
    public static bool HighFrameRate { get { Load(); return highFrameRate; } set { Load(); highFrameRate = value; PlayerPrefs.SetInt(Prefix + "fps60", value ? 1 : 0); Notify(); } }

    public static bool Vibration { get { Load(); return vibration; } set { Load(); vibration = value; PlayerPrefs.SetInt(Prefix + "vibrate", value ? 1 : 0); Notify(); } }

    public static bool ShowFps { get { Load(); return showFps; } set { Load(); showFps = value; PlayerPrefs.SetInt(Prefix + "showfps", value ? 1 : 0); Notify(); } }

    /// <summary>Photon region code ("" = best ping).</summary>
    public static string Region { get { Load(); return region; } set { Load(); region = value ?? ""; PlayerPrefs.SetString(Prefix + "region", region); Notify(); } }

    public static readonly string[] Regions = { "", "asia", "in", "eu", "us", "usw", "jp", "au", "sa" };

    public static string RegionLabel(string code) => string.IsNullOrEmpty(code) ? "BEST PING" : code.ToUpperInvariant();

    static void Load()
    {
        if (loaded)
            return;
        loaded = true;
        lookSensitivity = PlayerPrefs.GetFloat(Prefix + "look", 1f);
        scopeSensitivity = PlayerPrefs.GetFloat(Prefix + "scope", 0.45f);
        masterVolume = PlayerPrefs.GetFloat(Prefix + "volume", 1f);
        qualityLevel = PlayerPrefs.GetInt(Prefix + "quality", -1);
        highFrameRate = PlayerPrefs.GetInt(Prefix + "fps60", 1) == 1;
        vibration = PlayerPrefs.GetInt(Prefix + "vibrate", 1) == 1;
        showFps = PlayerPrefs.GetInt(Prefix + "showfps", 0) == 1;
        region = PlayerPrefs.GetString(Prefix + "region", "");
    }

    static void Notify()
    {
        Apply();
        Changed?.Invoke();
    }

    /// <summary>Pushes the settings into the engine.</summary>
    public static void Apply()
    {
        Load();
        AudioListener.volume = masterVolume;
        // Android defaults to 30 fps unless a target is set.
        Application.targetFrameRate = highFrameRate ? 60 : 30;
        QualitySettings.vSyncCount = 0;
        if (qualityLevel >= 0 && qualityLevel < QualitySettings.names.Length && QualitySettings.GetQualityLevel() != qualityLevel)
            QualitySettings.SetQualityLevel(qualityLevel, true);
    }

    public static void Save() => PlayerPrefs.Save();

    /// <summary>Short haptic pulse on Android, if enabled.</summary>
    public static void Vibrate()
    {
#if UNITY_ANDROID || UNITY_IOS
        if (Vibration && !Application.isEditor)
            Handheld.Vibrate();
#endif
    }
}
