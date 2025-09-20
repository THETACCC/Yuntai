using System;
using UnityEngine;
using UnityEngine.UI;

public class DialogueDefaultSequence : MonoBehaviour
{
    public static DialogueDefaultSequence instance;

    Button button;

    public bool isButtonActice = false;

    public void Awake()
    {
        instance = this;
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        button = GetComponent<Button>();
    }

    // Update is called once per frame
    void Update()
    {
        button.enabled = isButtonActice;
        if (isButtonActice)
        {
            if (Input.GetKeyUp(KeyCode.Space))
            {
                GoToNextDialogue();
            }
        }
    }

    public void GoToNextDialogue()
    {
        NextDialogueIndex();
        UpdateDialogue();
    }

    void NextDialogueIndex()
    {
        if (DialogueManager.instance != null)
        {
            DialogueManager.instance.NextDialogueIndex();
        }
        else
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
