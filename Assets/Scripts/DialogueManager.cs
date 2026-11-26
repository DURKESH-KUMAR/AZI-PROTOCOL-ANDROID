using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class DialogueLine
{
    public string dialogueText;
    public AudioClip audioClip;
}

public class DialogueManager : MonoBehaviour
{
    public Text dialogueTextUI;
    public AudioSource audioSource;
    public List<DialogueLine> dialogueList;

    public float totalDialogueInterval = 15f; // Total time per dialogue
    public float textVisibleTime = 3f;        // Text visible duration

    void Start()
    {
        StartCoroutine(PlayDialogueLoop());
    }

    IEnumerator PlayDialogueLoop()
    {
        // An empty list used to spin this loop forever without yielding (game freeze).
        if (dialogueList == null || dialogueList.Count == 0 || dialogueTextUI == null)
            yield break;

        // Cached waits: no allocation per line.
        WaitForSeconds visible = new WaitForSeconds(textVisibleTime);
        float remainingTime = totalDialogueInterval - textVisibleTime;
        WaitForSeconds remaining = remainingTime > 0f ? new WaitForSeconds(remainingTime) : null;

        while (true) // Loop forever
        {
            for (int i = 0; i < dialogueList.Count; i++)
            {
                dialogueTextUI.text = dialogueList[i].dialogueText;

                if (audioSource != null && dialogueList[i].audioClip != null)
                {
                    audioSource.clip = dialogueList[i].audioClip;
                    audioSource.Play();
                }

                yield return visible;
                dialogueTextUI.text = "";

                if (remaining != null)
                    yield return remaining;
            }
        }
    }
}
