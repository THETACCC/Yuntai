using System;
using UnityEngine;
using UnityEngine.UI;

public class DialogueContinueButton : MonoBehaviour
{
    public static DialogueContinueButton instance;

    Button button;

    public bool isActice = false;

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
        button.enabled = isActice;
    }

    public void GoToNextDialogue()
    {
        NextDialogueIndex();
        UpdateDialogue();
    }

    void NextDialogueIndex()
    {
        if (DialogueController.instance != null)
        {
            DialogueController.instance.NextDialogueIndex();
        }
        else
        {
            Debug.LogError("Please Assign DialogueController");
        }
    }

    void UpdateDialogue()
    {
        if (DialogueController.instance != null)
        {
            DialogueController.instance.UpdateDialogue(DialogueDisplaySettings.instance?.currentLanguage);
        }
        else
        {
            Debug.LogError("Please Assign DialogueController");
        }
    }
}