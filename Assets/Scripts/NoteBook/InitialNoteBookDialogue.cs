using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InitialNoteBookDialogue : MonoBehaviour
{
    public bool isOpenOnce = false;
    public bool isCloseOnce = false;
    public bool isTriggered = false;
    [SerializeField] private DialogueTrigger myDialogue;
    void Update()
    {
        if(NoteBookManager.instance.isOpen == true)
        {
            isOpenOnce = true;
        }

        if((NoteBookManager.instance.isOpen == false) && isOpenOnce)
        {
            isCloseOnce = true;
        }

        if(isCloseOnce && !isTriggered)
        {
            isTriggered = true;
            TriggerDialogue();
        }


    }
    

    public void TriggerDialogue()
    {
        if (myDialogue) myDialogue.TriggerDialogue();
    }

}
