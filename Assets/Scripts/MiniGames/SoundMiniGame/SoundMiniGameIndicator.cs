using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SoundMiniGameIndicator : MonoBehaviour
{

    public bool isReadyToTrigger = false;
    public GameObject myMiniGame;
    // Start is called before the first frame update
    void Start()
    {
        
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

    public void OpenMiniGame()
    {
        //Set the GameState To Talking
        Gamemanager.instance?.StartDialogue();
        myMiniGame.SetActive(true);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isReadyToTrigger = true;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isReadyToTrigger = false;
        }
    }
}

