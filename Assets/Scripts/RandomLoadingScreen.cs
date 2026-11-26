
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

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

    private int currentSceneIndex;
    private int nextSceneIndex;

    private void Start()
    {
        // Get current scene index
        currentSceneIndex =
            SceneManager.GetActiveScene().buildIndex;

        // Calculate next scene index
        nextSceneIndex = currentSceneIndex + 1;

        // Display a random loading image
        ShowRandomImage();

        // Start loading timer
        StartCoroutine(LoadingSequence());
    }

    private void ShowRandomImage()
    {
        if (loadingImage == null)
        {
            Debug.LogError(
                "Loading Image UI is not assigned!"
            );
            return;
        }

        if (loadingImages == null ||
            loadingImages.Count == 0)
        {
            Debug.LogWarning(
                "No loading images assigned!"
            );
            return;
        }

        // Select random image index
        int randomIndex =
            Random.Range(0, loadingImages.Count);

        // Apply selected image
        loadingImage.sprite =
            loadingImages[randomIndex];

        loadingImage.enabled = true;

        Debug.Log(
            "Loading Image Index: " + randomIndex
        );
    }

    private IEnumerator LoadingSequence()
    {
        // Wait for 8 seconds
        if (useUnscaledTime)
        {
            yield return new WaitForSecondsRealtime(
                loadingDuration
            );
        }
        else
        {
            yield return new WaitForSeconds(
                loadingDuration
            );
        }

        // Load next scene
        if (loadNextScene)
        {
            LoadNextScene();
        }
    }

    private void LoadNextScene()
    {
        int totalScenes =
            SceneManager.sceneCountInBuildSettings;

        if (nextSceneIndex >= 0 &&
            nextSceneIndex < totalScenes)
        {
            Debug.Log(
                "Loading Scene Index: " +
                nextSceneIndex
            );

            SceneManager.LoadScene(nextSceneIndex);
        }
        else
        {
            Debug.LogWarning(
                "Next scene not found in Build Settings!"
            );
        }
    }
}
