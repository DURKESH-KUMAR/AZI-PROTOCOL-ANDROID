using UnityEngine;

/// <summary>
/// Landscape only, but in BOTH directions: the game follows the phone when it is turned
/// around (left-handed / right-handed grip) instead of staying upside-down.
/// Portrait is never allowed. Runs once at startup (Loading scene); the setting persists.
/// </summary>
public class LandscapeManager : MonoBehaviour
{
    void Awake()
    {
        Screen.autorotateToPortrait = false;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.autorotateToLandscapeLeft = true;
        Screen.autorotateToLandscapeRight = true;

        // Start in landscape if the phone is held upright, then follow the sensor.
        if (Screen.orientation == ScreenOrientation.Portrait || Screen.orientation == ScreenOrientation.PortraitUpsideDown)
            Screen.orientation = ScreenOrientation.LandscapeLeft;
        Screen.orientation = ScreenOrientation.AutoRotation;
    }
}
