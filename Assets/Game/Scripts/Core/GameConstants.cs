using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Scene names used by code. Loading by NAME (not build index) means reordering
/// Build Settings no longer silently breaks scene flow.
/// </summary>
public static class GameScenes
{
    public const string Loading = "Loading";
    public const string MainMenu = "MainMenu";
    public const string OnBoarding = "OnBoarding";
    public const string Match = "The_Viking_Village";
    /// <summary>Shown whenever a match ends (time, score limit, or the player leaving).</summary>
    public const string Results = "Results";

    public static bool IsMatch(Scene scene) => scene.name == Match;

    /// <summary>Results if it is in the build; otherwise straight back to the main menu.</summary>
    public static string AfterMatch => Application.CanStreamedLevelBeLoaded(Results) ? Results : MainMenu;
}

/// <summary>Photon custom-property keys shared by several scripts.</summary>
public static class GameProps
{
    // Player properties
    public const string Kills = "K";
    public const string Deaths = "D";
    public const string Ready = "Ready";

    // Room properties
    public const string BlueScore = "BlueScore";
    public const string RedScore = "RedScore";
    public const string MatchStart = "MatchStart";
    public const string MatchOver = "Over";
    public const string Winner = "Win";
    public const string EndReason = "EndWhy";

    public const int EndReasonTime = 0;
    public const int EndReasonScore = 1;

    public static int GetInt(ExitGames.Client.Photon.Hashtable props, string key, int fallback = 0)
    {
        return props != null && props.TryGetValue(key, out object v) && v is int i ? i : fallback;
    }

    public static bool GetBool(ExitGames.Client.Photon.Hashtable props, string key)
    {
        return props != null && props.TryGetValue(key, out object v) && v is bool b && b;
    }
}

/// <summary>Team 1 = BLUE, team 2 = RED (matches the ScoreBoard: team 1 deaths score for Red).</summary>
public static class Teams
{
    public const int Blue = 1;
    public const int Red = 2;

    public static readonly Color BlueColor = new Color(0.25f, 0.62f, 1f);
    public static readonly Color RedColor = new Color(1f, 0.32f, 0.3f);
    public static readonly Color NeutralColor = new Color(0.8f, 0.8f, 0.8f);

    public static string Name(int team) => team == Blue ? "BLUE" : team == Red ? "RED" : "-";

    public static Color Color(int team) => team == Blue ? BlueColor : team == Red ? RedColor : NeutralColor;

    public static string ColorHex(int team) => ColorUtility.ToHtmlStringRGB(Color(team));

    public static int Other(int team) => team == Blue ? Red : Blue;
}

/// <summary>
/// Physics layers of the level geometry. The village terrain is on "Ground"/"MainGround",
/// props and docks on "Props"/"ReflectedInWater". A ground check that only looked at
/// Default never saw the terrain, so the player was "falling" forever.
/// </summary>
public static class WorldLayers
{
    static int mask = -1;

    public static int Mask
    {
        get
        {
            if (mask == -1)
                mask = LayerMask.GetMask("Default", "Props", "ReflectedInWater", "Ground", "MainGround");
            return mask;
        }
    }
}

/// <summary>Live match flags, written by ScoreBoard / TimerScript in the match scene.</summary>
public static class MatchState
{
    public static bool IsOver;
    public static int Winner;

    /// <summary>True during the pre-match countdown (and while waiting for the master's start time).</summary>
    public static bool IsWarmup => TimerScript.Instance != null && TimerScript.Instance.IsWarmup;

    public static bool CombatAllowed => !IsOver && !IsWarmup;

    public static void Reset()
    {
        IsOver = false;
        Winner = 0;
    }
}
