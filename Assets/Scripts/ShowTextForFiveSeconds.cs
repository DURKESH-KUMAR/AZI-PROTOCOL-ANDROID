using UnityEngine;

/// <summary>Shows <see cref="textObject"/> when the scene starts and hides it after <see cref="displayTime"/> seconds.</summary>
public class ShowTextForFiveSeconds : MonoBehaviour
{
    public GameObject textObject;  // Drag your Text GameObject here
    public float displayTime = 5f; // Duration (default 5 seconds)

    void Start()
    {
        if (textObject == null)
        {
            Debug.LogWarning("[ShowTextForFiveSeconds] No text object assigned on " + name);
            return;
        }
        textObject.SetActive(true);
        Invoke(nameof(Hide), displayTime); // no coroutine/WaitForSeconds allocation
    }

    void Hide()
    {
        if (textObject != null)
            textObject.SetActive(false);
    }
}
