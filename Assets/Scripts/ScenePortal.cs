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

    [Header("Audio")]
    [Tooltip("留空则用默认 sndSceneTransition / sndNextScene；填 Resources 下的相对路径则覆盖本 portal 的过场音效")]
    [SerializeField] private string transitionSfxOverride = "";

    private const string DefaultTransitionSfx = "Sound Effects/Henk/sndSceneTransition";
    private const string DefaultInstantSfx = "Sound Effects/Henk/sndNextScene";

    private string ResolveSfx(string fallback)
        => string.IsNullOrEmpty(transitionSfxOverride) ? fallback : transitionSfxOverride;


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
                AudioManager.PlayOneShot(ResolveSfx(DefaultInstantSfx), AudioGroup.SFX);
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
        if(isPlayerInTrigger && (myGamemanager.phase == GamePhase.Moving))
        {
            if (Input.GetKeyDown(KeyCode.W))
            {
                AudioManager.PlayOneShot(ResolveSfx(DefaultTransitionSfx), AudioGroup.SFX);

                SceneController.instance.LoadSceneAndTeleport(scenename, SpawnPointLocation);
            }
        }
    }


}
