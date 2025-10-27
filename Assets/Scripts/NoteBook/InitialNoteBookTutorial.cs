using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InitialNoteBookTutorial : DialogueTrigger
{
    public bool isTutorialized;
    // Start is called before the first frame update
    void Start()
    {
        if (!isTutorialized)
        {
            TriggerDialogue();
            isTutorialized = true;
        }
    }
}
