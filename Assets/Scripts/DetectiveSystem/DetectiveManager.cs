using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DetectiveManager : MonoBehaviour
{
    public GameObject myDetectiveCanvas;

    public TextAsset CorrectDialogueJsonFile;
    public TextAsset FirstWrongDialogueJsonFile;
    public TextAsset SecondWrongDialogueJsonFile;
    public TextAsset FailDialogueJsonFile;

    private int WrongTimes = 0;

    public GameObject[] myCluesContent;



    public void Start()
    {
        myDetectiveCanvas.SetActive(false);
    }

    public void StartDetectiveGame()
    {
        if (myDetectiveCanvas != null)
        {
            myDetectiveCanvas.SetActive(true);
            Gamemanager.instance?.StartDialogue();
        }
    }

    public void EndDetectiveGame()
    {
        if (myDetectiveCanvas != null)
        {
            myDetectiveCanvas.SetActive(false);
            Gamemanager.instance?.EndDialogue();
        }
    }

    public void DisableAllContent()
    {
        if (myCluesContent == null || myCluesContent.Length == 0)
            return;

        foreach (var content in myCluesContent)
        {
            if (content != null)
                content.SetActive(false);
        }
    }


    public void SelectedCorrect()
    {
        EndDetectiveGame();
        DisableAllContent();
        Gamemanager.instance?.StartDialogue();
        DialogueManager.instance.LoadDialogueFromFile(CorrectDialogueJsonFile);
        DialogueManager.instance.StartDialogue();
    }

    public void SelectedWrong()
    {
        if(WrongTimes == 0)
        {
            EndDetectiveGame();
            DisableAllContent();
            Gamemanager.instance?.StartDialogue();
            DialogueManager.instance.LoadDialogueFromFile(FirstWrongDialogueJsonFile);
            DialogueManager.instance.StartDialogue();
            WrongTimes += 1;
        }
        else if (WrongTimes == 1)
        {
            EndDetectiveGame();
            DisableAllContent();
            Gamemanager.instance?.StartDialogue();
            DialogueManager.instance.LoadDialogueFromFile(SecondWrongDialogueJsonFile);
            DialogueManager.instance.StartDialogue();
            WrongTimes += 1;
        }
        else if(WrongTimes == 2)
        {
            EndDetectiveGame();
            DisableAllContent();
            Gamemanager.instance?.StartDialogue();
            DialogueManager.instance.LoadDialogueFromFile(FailDialogueJsonFile);
            DialogueManager.instance.StartDialogue();
        }


    }

}
