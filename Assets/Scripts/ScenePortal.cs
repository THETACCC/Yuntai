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
    protected bool isPlayerInTrigger = false;

    //
    public bool isInstant = false;



    private void Start()
    {
        InteractIndicator.SetActive(false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            if(!isInstant)
            {
                isPlayerInTrigger = true;
                InteractIndicator.SetActive(true);
            }
            else
            {
                SceneController.instance.LoadSceneAndTeleport(scenename, SpawnPointLocation);
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

    protected virtual void Update()
    {
        if(isPlayerInTrigger)
        {
            if (Input.GetKeyDown(KeyCode.W))
            {
                SceneController.instance.LoadSceneAndTeleport(scenename, SpawnPointLocation);
            }
        }
    }


}
