using UnityEngine;
using System.Collections;

public class SceneStartDialogue : MonoBehaviour
{
    [Header("Assign the JSON(s) for THIS scene")]
    public TextAsset mainDialogueJsonFile;
    public TextAsset postDialogueJsonFile;

    [Header("Start options")]
    public bool usePostDialogue = false;   // play post instead of main
    public float delay = 0f;               // optional delay before starting

    [Header("Optional: a trigger to mark as current")]
    public DialogueTrigger linkTrigger;    // keeps your finish flags consistent

    private IEnumerator Start()
    {
        // wait one frame so singletons/UI exist
        yield return null;

        if (delay > 0f) yield return new WaitForSeconds(delay);

        // choose which file to play
        TextAsset toPlay = (!usePostDialogue || postDialogueJsonFile == null)
            ? mainDialogueJsonFile
            : postDialogueJsonFile;

        if (toPlay == null)
        {
            Debug.LogError("[SceneStartDialogue] No dialogue JSON assigned for this scene.");
            yield break;
        }

        if (linkTrigger != null)
            DialogueManager.instance.currentTrigger = linkTrigger;

        Gamemanager.instance?.StartDialogue();
        DialogueManager.instance.LoadDialogueFromFile(toPlay);
        DialogueManager.instance.StartDialogue();
    }
}

