using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ScenePortal : MonoBehaviour
{
    //scenes
    public string scenename;
    public int SpawnPointLocation;
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

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInTrigger = false;
            InteractIndicator.SetActive(false);

        }
    }

    private void Update()
    {
        if(isPlayerInTrigger)
        {
            if (Input.GetKeyDown(KeyCode.E))
            {
                SceneController.instance.LoadSceneAndTeleport(scenename, SpawnPointLocation);
            }
        }
    }


}
