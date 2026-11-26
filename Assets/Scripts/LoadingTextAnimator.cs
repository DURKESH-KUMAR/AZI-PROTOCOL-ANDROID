using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class LoadingTextAnimator : MonoBehaviour
{
    public Text loadingText;      // Assign in Inspector
    public float dotSpeed = 0.5f; // Speed of dot animation

    // Pre-built frames + one cached wait: the old loop allocated a new string and a new
    // WaitForSeconds every tick (GC churn for as long as the loading screen was up).
    static readonly string[] Frames = { "Loading", "Loading.", "Loading..", "Loading..." };
    int dotCount = 0;
    WaitForSeconds wait;

    void OnEnable()
    {
        wait = new WaitForSeconds(dotSpeed);
        StartCoroutine(AnimateLoading());
    }

    IEnumerator AnimateLoading()
    {
        while (true)
        {
            dotCount = (dotCount + 1) % Frames.Length;
            if (loadingText != null)
                loadingText.text = Frames[dotCount];
            yield return wait;
        }
    }
}
