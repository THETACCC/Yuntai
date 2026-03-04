using UnityEngine;
using System.Collections;

public class SceneDialogueStarter : DialogueTrigger
{
    [Header("Start options")]
    public bool usePostDialogue = false;   // play post instead of main
    public float delay = 0f;               // optional delay before starting

    private IEnumerator Start()
    {
        // wait one frame so singletons/UI exist
        yield return null;

        if (delay > 0f) yield return new WaitForSeconds(delay);
        Debug.Log("Changing pHASE");

        Gamemanager.instance.StartDialogue();
        TriggerDialogue();
    }


}