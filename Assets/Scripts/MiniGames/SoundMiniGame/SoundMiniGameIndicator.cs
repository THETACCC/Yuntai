using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SoundMiniGameIndicator : MonoBehaviour
{

    public bool isReadyToTrigger = false;

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
    }

    public void OpenMiniGame()
    {
        //Set the GameState To Talking
        Gamemanager.instance?.StartDialogue();
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

