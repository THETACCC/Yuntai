using UnityEngine;

public class DialogueChoice : MonoBehaviour
{
    public int index;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void GoToChoiceIndex()
    {
        SetDialogueIndex();
        UpdateDialogue();
    }

    void SetDialogueIndex()
    {
        if (DialogueManager.instance != null)
        {
            DialogueManager.instance.SetDialogueIndex(index);
        } else
        {
            Debug.LogError("Please Assign DialogueManager");
        }
    }

    void UpdateDialogue()
    {
        if (DialogueManager.instance != null)
        {
            DialogueManager.instance.UpdateDialogue();
        }
        else
        {
            Debug.LogError("Please Assign DialogueManager");
        }
    }
}
