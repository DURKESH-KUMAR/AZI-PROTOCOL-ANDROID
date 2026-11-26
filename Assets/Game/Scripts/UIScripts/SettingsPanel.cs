using System;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Settings screen (Resources/UI/Settings.uxml) used by the pause menu and the main menu:
/// look / scope sensitivity, volume, graphics quality, 30/60 fps, vibration, FPS counter
/// and (main menu only) the Photon region.
/// </summary>
public static class SettingsPanel
{
    /// <summary>Shows the panel inside <paramref name="host"/>. Returns the element; remove it to close.</summary>
    public static VisualElement Open(VisualElement host, bool showRegion, Action onClose, Action onRegionChanged = null)
    {
        VisualElement panel = GameUI.Instantiate("Settings", host);

        BindSlider(panel, "look", GameSettings.LookSensitivity, v => GameSettings.LookSensitivity = v, v => v.ToString("0.00") + "x");
        BindSlider(panel, "scope", GameSettings.ScopeSensitivity, v => GameSettings.ScopeSensitivity = v, v => Mathf.RoundToInt(v * 100f) + "%");
        BindSlider(panel, "volume", GameSettings.MasterVolume, v => GameSettings.MasterVolume = v, v => Mathf.RoundToInt(v * 100f) + "%");

        BindToggle(panel, "fps60", GameSettings.HighFrameRate, v => GameSettings.HighFrameRate = v);
        BindToggle(panel, "vibration", GameSettings.Vibration, v => GameSettings.Vibration = v);
        BindToggle(panel, "showfps", GameSettings.ShowFps, v => GameSettings.ShowFps = v);

        string[] qualities = QualitySettings.names;
        Button quality = null;
        quality = panel.Bind("quality", () =>
        {
            if (qualities.Length > 0)
                GameSettings.QualityLevel = (QualitySettings.GetQualityLevel() + 1) % qualities.Length;
            quality.text = QualityName(qualities);
        });
        if (quality != null)
            quality.text = QualityName(qualities);

        panel.Q("region-row").SetVisible(showRegion);
        Button region = null;
        region = panel.Bind("region", () =>
        {
            int index = Array.IndexOf(GameSettings.Regions, GameSettings.Region);
            GameSettings.Region = GameSettings.Regions[(index + 1) % GameSettings.Regions.Length];
            region.text = GameSettings.RegionLabel(GameSettings.Region);
            onRegionChanged?.Invoke();
        });
        if (region != null)
            region.text = GameSettings.RegionLabel(GameSettings.Region);

        panel.Bind("done", () =>
        {
            GameSettings.Save(); // one disk write when closing, not per slider tick
            panel.RemoveFromHierarchy();
            onClose?.Invoke();
        });
        return panel;
    }

    static string QualityName(string[] names)
    {
        return names.Length == 0 ? "DEFAULT" : names[Mathf.Clamp(QualitySettings.GetQualityLevel(), 0, names.Length - 1)].ToUpperInvariant();
    }

    static void BindSlider(VisualElement panel, string name, float value, Action<float> set, Func<float, string> format)
    {
        Slider slider = panel.Q<Slider>(name);
        Label label = panel.Q<Label>(name + "-value");
        if (slider == null)
            return;
        slider.SetValueWithoutNotify(value);
        if (label != null)
            label.text = format(value);
        slider.RegisterValueChangedCallback(e =>
        {
            set(e.newValue);
            if (label != null)
                label.text = format(e.newValue);
        });
    }

    static void BindToggle(VisualElement panel, string name, bool value, Action<bool> set)
    {
        Toggle toggle = panel.Q<Toggle>(name);
        if (toggle == null)
            return;
        toggle.SetValueWithoutNotify(value);
        toggle.RegisterValueChangedCallback(e =>
        {
            GameAudio.PlayUI(GameAudio.Click, 0.5f);
            set(e.newValue);
        });
    }
}
