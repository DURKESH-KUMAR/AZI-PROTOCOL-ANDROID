using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// Keeps Build Settings in sync with the project and offers one-click scene optimisation.
///
/// Automatic (after every script reload):
///   - removes Build Settings entries whose scene file no longer exists (e.g. the deleted End scene);
///   - makes sure Assets/Scenes/Results.unity is in the build (it is shown when a match ends).
///
/// Menu "Phantom Warrior":
///   - Optimise Build Scenes: per scene, turns off raycasts on purely decorative UI graphics,
///     disables pixel-perfect canvases, lets videos drop frames instead of stalling, and warns
///     about duplicate EventSystems / AudioListeners. Saves each scene.
///   - Bake Occlusion Culling (Match): bakes occlusion data for the Viking Village match scene
///     so buildings hidden behind other buildings are not drawn (big win on Android GPUs).
/// </summary>
[InitializeOnLoad]
static class ProjectSetup
{
    const string ResultsScene = "Assets/Scenes/Results.unity";
    const string MatchScene = "Assets/Viking Village/Scenes/The_Viking_Village.unity";

    static ProjectSetup()
    {
        EditorApplication.delayCall += () => SyncBuildSettings(false);
    }

    [MenuItem("Phantom Warrior/Sync Build Settings")]
    static void SyncFromMenu() => SyncBuildSettings(true);

    static void SyncBuildSettings(bool verbose)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        bool changed = false;

        int removed = scenes.RemoveAll(s => string.IsNullOrEmpty(s.path) || !File.Exists(s.path));
        if (removed > 0)
        {
            changed = true;
            Debug.Log("[PW] Removed " + removed + " missing scene(s) from Build Settings.");
        }

        if (File.Exists(ResultsScene))
        {
            EditorBuildSettingsScene results = scenes.Find(s => s.path == ResultsScene);
            if (results == null)
            {
                scenes.Add(new EditorBuildSettingsScene(ResultsScene, true));
                changed = true;
                Debug.Log("[PW] Added Results scene to Build Settings.");
            }
            else if (!results.enabled)
            {
                results.enabled = true;
                changed = true;
            }
        }

        if (changed)
            EditorBuildSettings.scenes = scenes.ToArray();
        else if (verbose)
            Debug.Log("[PW] Build Settings already up to date.");
    }

    // ------------------------------------------------------------------ scene optimisation

    [MenuItem("Phantom Warrior/Optimise Build Scenes")]
    static void OptimiseBuildScenes()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        SceneSetup[] previous = EditorSceneManager.GetSceneManagerSetup();

        int raycasts = 0, canvases = 0, videos = 0;
        foreach (EditorBuildSettingsScene entry in EditorBuildSettings.scenes)
        {
            if (!entry.enabled || !File.Exists(entry.path))
                continue;

            Scene scene = EditorSceneManager.OpenScene(entry.path, OpenSceneMode.Single);
            int r = 0, c = 0, v = 0;

            foreach (Graphic g in FindInScene<Graphic>(scene))
            {
                if (g.raycastTarget && !NeedsRaycast(g))
                {
                    Undo.RecordObject(g, "Disable raycast");
                    g.raycastTarget = false;
                    r++;
                }
            }

            foreach (Canvas canvas in FindInScene<Canvas>(scene))
            {
                if (canvas.pixelPerfect)
                {
                    Undo.RecordObject(canvas, "Disable pixel perfect");
                    canvas.pixelPerfect = false; // re-layouts every moving element each frame
                    c++;
                }
            }

            foreach (VideoPlayer player in FindInScene<VideoPlayer>(scene))
            {
                if (!player.skipOnDrop)
                {
                    Undo.RecordObject(player, "Skip on drop");
                    player.skipOnDrop = true; // slow phones drop frames instead of slowing the whole video
                    v++;
                }
            }

            int eventSystems = FindInScene<EventSystem>(scene).Count;
            int listeners = FindInScene<AudioListener>(scene).Count;
            if (eventSystems > 1)
                Debug.LogWarning("[PW] " + scene.name + ": " + eventSystems + " EventSystems (keep one).");
            if (listeners > 1)
                Debug.LogWarning("[PW] " + scene.name + ": " + listeners + " AudioListeners (keep one).");

            if (r + c + v > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            Debug.Log("[PW] " + scene.name + ": " + r + " decorative raycasts off, " + c + " pixel-perfect canvases off, " + v + " videos set to skip on drop.");
            raycasts += r;
            canvases += c;
            videos += v;
        }

        if (previous.Length > 0)
            EditorSceneManager.RestoreSceneManagerSetup(previous);
        EditorUtility.DisplayDialog("Phantom Warrior", "Optimised build scenes:\n\n"
            + raycasts + " decorative UI raycasts disabled\n"
            + canvases + " pixel-perfect canvases disabled\n"
            + videos + " videos set to skip frames on slow devices\n\nDetails are in the Console.", "OK");
    }

    /// <summary>
    /// A graphic only needs raycasts if something on it or above it handles pointer events
    /// (buttons, input fields, scroll views, joysticks, event triggers...). Everything else is
    /// decoration and only costs raycast time on every touch.
    /// </summary>
    static bool NeedsRaycast(Graphic graphic)
    {
        for (Transform t = graphic.transform; t != null; t = t.parent)
        {
            foreach (MonoBehaviour b in t.GetComponents<MonoBehaviour>())
                if (b is IEventSystemHandler)
                    return true;
            if (t.GetComponent<Canvas>() != null && t.GetComponent<Canvas>().isRootCanvas)
                break;
        }
        return false;
    }

    static List<T> FindInScene<T>(Scene scene) where T : Component
    {
        List<T> result = new List<T>();
        foreach (GameObject root in scene.GetRootGameObjects())
            result.AddRange(root.GetComponentsInChildren<T>(true));
        return result;
    }

    [MenuItem("Phantom Warrior/Bake Occlusion Culling (Match)")]
    static void BakeOcclusion()
    {
        if (!File.Exists(MatchScene) || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        if (!EditorUtility.DisplayDialog("Phantom Warrior", "Bake occlusion culling for the match scene?\nThis can take a few minutes.", "Bake", "Cancel"))
            return;

        SceneSetup[] previous = EditorSceneManager.GetSceneManagerSetup();
        Scene scene = EditorSceneManager.OpenScene(MatchScene, OpenSceneMode.Single);
        if (StaticOcclusionCulling.Compute())
        {
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[PW] Occlusion culling baked for " + scene.name + ".");
        }
        else
        {
            Debug.LogWarning("[PW] Occlusion bake failed. Mark large buildings/walls as 'Occluder Static' and try again.");
        }
        if (previous.Length > 0)
            EditorSceneManager.RestoreSceneManagerSetup(previous);
    }
}
