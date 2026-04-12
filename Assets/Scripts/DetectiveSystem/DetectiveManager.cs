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

    public int WrongTimes = 0;

    public GameObject[] myCluesContent;

    public DialogueTrigger myAffectedTrigger;


    private bool isPlayingDetective = false;

    public void Start()
    {
        myDetectiveCanvas.SetActive(false);
    }

    public void Update()
    {
        if(isPlayingDetective)
        {
        //    Gamemanager.instance?.StartDialogue();
        }
    }

    public void StopPlayerMovement()
    {
        Gamemanager.instance?.StartDialogue();
    }


    public void StartDetectiveGame()
    {
        //tartCoroutine(StartDetectiveGameDelayed());

        if (myDetectiveCanvas != null)
        {
            myDetectiveCanvas.SetActive(true);
            Gamemanager.instance?.StartDialogue();
            isPlayingDetective = true;
        }
    }

    private IEnumerator StartDetectiveGameDelayed()
    {
        yield return new WaitForSeconds(1f);

    }

    public void EndDetectiveGame()
    {
        if (myDetectiveCanvas != null)
        {
            myDetectiveCanvas.SetActive(false);
            Gamemanager.instance?.EndDialogue();
            isPlayingDetective = false;
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

        DialogueController.instance.LoadDialogueFromFile(CorrectDialogueJsonFile);
        DialogueController.instance.StartDialogue();
        Gamemanager.instance?.StartDialogue();
    }

    public void SelectedWrong()
    {
        if(WrongTimes == 0)
        {
            EndDetectiveGame();
            DisableAllContent();

            DialogueController.instance.LoadDialogueFromFile(FirstWrongDialogueJsonFile);
            DialogueController.instance.StartDialogue();
            Gamemanager.instance?.StartDialogue();
            WrongTimes += 1;
        }
        else if (WrongTimes == 1)
        {
            EndDetectiveGame();
            DisableAllContent();

            DialogueController.instance.LoadDialogueFromFile(SecondWrongDialogueJsonFile);
            DialogueController.instance.StartDialogue();
            Gamemanager.instance?.StartDialogue();
            WrongTimes += 1;
        }
        else if(WrongTimes == 2)
        {
            EndDetectiveGame();
            DisableAllContent();

            DialogueController.instance.LoadDialogueFromFile(FailDialogueJsonFile);
            DialogueController.instance.StartDialogue();
            Gamemanager.instance?.StartDialogue();
            //This disables the conversation, can add other effects such as death
            myAffectedTrigger.enabled = false;
        }


    }

}
