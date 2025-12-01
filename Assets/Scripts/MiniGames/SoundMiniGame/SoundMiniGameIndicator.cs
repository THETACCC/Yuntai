using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SoundMiniGameIndicator : MonoBehaviour
{
    private bool isSolved = false;
    public bool isReadyToTrigger = false;
    public GameObject myMiniGame;

    public GameObject myUI_E;

    // Start is called before the first frame update
    void Start()
    {
        myUI_E.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
        if (isReadyToTrigger)
        {
            if (Input.GetKeyDown(KeyCode.E))
            {
                OpenMiniGame();
            }

        }

        if(Input.GetKeyDown(KeyCode.Escape))
        {
          //  CloseMiniGame();
        }


    }

    public void CloseMiniGame()
    {
        Gamemanager.instance?.EndDialogue();
        myMiniGame.SetActive(false);
    }

    public void SolvedMiniGame()
    {
        Gamemanager.instance?.EndDialogue();
        myMiniGame.SetActive(false);
        isReadyToTrigger = false;
        isSolved = true;

    }

    public void OpenMiniGame()
    {
        //Set the GameState To Talking
        Gamemanager.instance?.StartDialogue();
        myMiniGame.SetActive(true);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") && !isSolved)
        {
            myUI_E.SetActive(true);
            isReadyToTrigger = true;
        }
    }
    private void OnTriggerStay2D(Collider2D other)
    {
        if (other.CompareTag("Player") && !isSolved)
        {
            if (Input.GetKey(KeyCode.E))
            {
                myUI_E.SetActive(false);
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player") && !isSolved)
        {
            myUI_E.SetActive(false);
            isReadyToTrigger = false;
        }
    }
}

