using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class UI_E : MonoBehaviour
{
    //scenes
    public GameObject InteractIndicator;
    private bool isPlayerInTrigger = false;

    private void Start()
    {
        InteractIndicator.SetActive(false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInTrigger = true;
            InteractIndicator.SetActive(true);

        }
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        //This disables the E when player presses
        if (collision.CompareTag("Player"))
        {
            if(Input.GetKey(KeyCode.E))
            {
                InteractIndicator.SetActive(false);
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInTrigger = false;
            InteractIndicator.SetActive(false);

        }
    }
}
