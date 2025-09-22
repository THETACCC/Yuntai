using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DialogueCollisionTrigger : MonoBehaviour
{
    public TextAsset jsonFile;
    private bool isTrigger = false;
    // Start is called before the first frame update
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            Gamemanager.instance.StartDialogue();
            DialogueManager.instance.LoadDialogueFromFile(jsonFile);
            DialogueManager.instance.StartDialogue();
        }
    }
}
