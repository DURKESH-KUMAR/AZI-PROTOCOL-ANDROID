using System;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// UI Toolkit plumbing for every runtime screen (HUD, results, settings, overlay).
/// Layouts live in Resources/UI/*.uxml, styles in *.uss.
/// - ONE shared PanelSettings for all documents: one panel = fewer batches/updates
///   than a panel per screen; order is controlled with UIDocument.sortingOrder.
/// - Non-interactive elements are made touch-transparent, so the HUD never steals
///   touches from the uGUI joysticks underneath.
/// </summary>
public static class GameUI
{
    const string Folder = "UI/";

    public static readonly Color Accent = new Color(1f, 0.72f, 0.19f);
    public static readonly Color TextColor = new Color(0.93f, 0.95f, 1f);
    public static readonly Color MutedText = new Color(0.6f, 0.64f, 0.7f);

    static PanelSettings panel;

    public static PanelSettings Panel
    {
        get
        {
            if (panel != null)
                return panel;
            panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.name = "Phantom Warrior UI Panel";
            panel.hideFlags = HideFlags.DontUnloadUnusedAsset; // survives scene loads
            panel.themeStyleSheet = Resources.Load<ThemeStyleSheet>(Folder + "Theme");
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1920, 1080);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            // Match HEIGHT: the panel is always 1080 units tall and only the width changes with
            // the phone (1920 at 16:9, ~2340 at 19.5:9). Vertical layouts never overflow on tall
            // phones; edge-anchored elements simply get more room.
            panel.match = 1f;
            panel.sortingOrder = 100; // above the scene's uGUI canvases (joysticks), so pause/settings get touches first
            panel.clearColor = false;
            return panel;
        }
    }

    public static VisualTreeAsset Load(string uxml) => Resources.Load<VisualTreeAsset>(Folder + uxml);

    /// <summary>Creates a child GameObject with a UIDocument showing the given UXML.</summary>
    public static UIDocument CreateDocument(Transform parent, string uxml, int sortingOrder)
    {
        GameObject go = new GameObject(uxml + " UI");
        go.SetActive(false); // configure before OnEnable builds the tree
        go.transform.SetParent(parent, false);
        UIDocument doc = go.AddComponent<UIDocument>();
        doc.panelSettings = Panel;
        doc.sortingOrder = sortingOrder;
        doc.visualTreeAsset = Load(uxml);
        go.SetActive(true);
        if (doc.visualTreeAsset == null)
            Debug.LogError("[GameUI] Missing Resources/" + Folder + uxml + ".uxml");
        MakeTouchTransparent(doc.rootVisualElement);
        if (doc.rootVisualElement.childCount > 0)
        {
            VisualElement content = doc.rootVisualElement[0];
            if (content is TemplateContainer && content.childCount > 0)
                content = content[0];
            KeepInSafeArea(doc.rootVisualElement, content);
        }
        return doc;
    }

    /// <summary>
    /// Insets <paramref name="content"/> (an absolutely positioned full-screen container) by the
    /// device safe area, so buttons and text never sit under a notch, punch-hole camera or the
    /// rounded corners of Android phones. Re-applied whenever the panel size changes (rotation).
    /// </summary>
    public static void KeepInSafeArea(VisualElement root, VisualElement content)
    {
        void Apply()
        {
            if (root.panel == null || Screen.width <= 0 || Screen.height <= 0)
                return;
            float scale = root.panel.visualTree.layout.width / Screen.width; // screen px -> panel units
            if (float.IsNaN(scale) || scale <= 0f)
                return;
            Rect safe = Screen.safeArea;
            content.style.position = Position.Absolute;
            content.style.left = Mathf.Round(safe.xMin * scale);
            content.style.right = Mathf.Round((Screen.width - safe.xMax) * scale);
            content.style.top = Mathf.Round((Screen.height - safe.yMax) * scale);
            content.style.bottom = Mathf.Round(safe.yMin * scale);
        }
        root.RegisterCallback<GeometryChangedEvent>(_ => Apply());
        Apply();
    }

    /// <summary>Instantiates a UXML template under a parent (settings panel, result rows).</summary>
    public static VisualElement Instantiate(string uxml, VisualElement parent)
    {
        VisualTreeAsset asset = Load(uxml);
        if (asset == null)
        {
            Debug.LogError("[GameUI] Missing Resources/" + Folder + uxml + ".uxml");
            return new VisualElement();
        }
        TemplateContainer tree = asset.Instantiate();
        tree.AddToClassList("fill");
        parent.Add(tree);
        MakeTouchTransparent(tree);
        return tree;
    }

    /// <summary>
    /// Everything ignores touches except controls, full-screen overlays (class "overlay", which
    /// must block the game while open) and custom touch surfaces (class "touch": sticks, action buttons).
    /// </summary>
    public static void MakeTouchTransparent(VisualElement element)
    {
        if (element == null)
            return;
        if (element is Button || element is Slider || element is Toggle || element is ScrollView || element is TextField)
            return;
        element.pickingMode = element.ClassListContains("overlay") || element.ClassListContains("touch") ? PickingMode.Position : PickingMode.Ignore;
        for (int i = 0; i < element.childCount; i++)
            MakeTouchTransparent(element[i]);
    }

    /// <summary>Binds a click with the UI click sound.</summary>
    public static Button Bind(this VisualElement root, string name, Action onClick)
    {
        Button button = root.Q<Button>(name);
        if (button == null)
        {
            Debug.LogWarning("[GameUI] Button not found: " + name);
            return null;
        }
        button.clicked += () =>
        {
            GameAudio.PlayUI(GameAudio.Click, 0.5f);
            onClick();
        };
        return button;
    }

    public static void SetVisible(this VisualElement element, bool visible)
    {
        if (element != null && element.ClassListContains("hidden") == visible)
            element.EnableInClassList("hidden", !visible);
    }

    public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

    /// <summary>Sets a uGUI button label whether it uses legacy Text or TextMeshPro (scene menus).</summary>
    public static void SetButtonLabel(GameObject button, string label)
    {
        UnityEngine.UI.Text legacy = button.GetComponentInChildren<UnityEngine.UI.Text>(true);
        if (legacy != null)
        {
            legacy.text = label;
            return;
        }
        TMPro.TMP_Text tmp = button.GetComponentInChildren<TMPro.TMP_Text>(true);
        if (tmp != null)
            tmp.text = label;
    }

    // ------------------------------------------------------------ generated textures

    static Texture2D vignette;

    /// <summary>Radial gradient (clear centre, solid edges) for damage / low-health tints.</summary>
    public static Texture2D Vignette
    {
        get
        {
            if (vignette != null)
                return vignette;
            const int size = 128;
            vignette = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontUnloadUnusedAsset };
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f;
                    float dy = (y + 0.5f) / size * 2f - 1f;
                    float d = Mathf.Clamp01((Mathf.Sqrt(dx * dx + dy * dy) - 0.45f) / 0.75f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.SmoothStep(0f, 1f, d) * 255f));
                }
            }
            vignette.SetPixels32(pixels);
            vignette.Apply(false, true);
            return vignette;
        }
    }

    public static Texture2D VerticalGradient(Color bottom, Color top)
    {
        Texture2D tex = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 64; y++)
            tex.SetPixel(0, y, Color.Lerp(bottom, top, y / 63f));
        tex.Apply(false, true);
        return tex;
    }
}
