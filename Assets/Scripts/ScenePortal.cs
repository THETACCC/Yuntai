using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using static AudioManager;

public class ScenePortal : MonoBehaviour
{
    //scenes
    public string scenename;
    public int SpawnPointLocation;
    public GameObject InteractIndicator;
    protected bool isPlayerInTrigger = false;

    //
    public bool isInstant = false;
    public Gamemanager myGamemanager;


    private void Start()
    {
        myGamemanager = Gamemanager.instance;
        InteractIndicator.SetActive(false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") && (myGamemanager.phase == GamePhase.Moving))
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
                //AudioManager.PlayOneShot("Sound Effects/sndToiletFlush", AudioGroup.SFX);

                SceneController.instance.LoadSceneAndTeleport(scenename, SpawnPointLocation);
            }
        }
    }


}
