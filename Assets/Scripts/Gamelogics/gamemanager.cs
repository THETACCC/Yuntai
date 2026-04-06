using MoreMountains.Tools;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Gamemanager : MonoBehaviour
{
    public static Gamemanager instance;

    // objects that don't destroy
    public GameObject virtualplayer;
    public GameObject playermanager;
    public GameObject avatarplayer;
    public GameObject m_camera;

    public GamePhase phase;

    private GamePhase lastPhase;

    public bool isChapter1 = false;
    public GameObject myChapter1Music;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }

        DontDestroyOnLoad(virtualplayer);
        DontDestroyOnLoad(avatarplayer);
        DontDestroyOnLoad(m_camera);
        DontDestroyOnLoad(playermanager);
    }

    private void Start()
    {
        SetPhase(GamePhase.Moving);
    }

    private void Update()
    {
        if(Input.GetKeyDown(KeyCode.P))
        {
            SceneController.instance.LoadSceneAndTeleport("Level4-1City", 0);
        }

        // Detect unexpected direct changes
        if (phase != lastPhase)
        {
            Debug.LogWarning($"[GamePhase Changed Directly] {lastPhase} -> {phase}");
            lastPhase = phase;
        }
    }


    public void enableChapter1()
    {
        myChapter1Music.SetActive(true);
    }


    public void SetPhase(
        GamePhase newPhase,
        [CallerMemberName] string caller = "",
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        if (phase == newPhase) return;

        Debug.Log(
            $"[GamePhase Change] {phase} -> {newPhase}\n" +
            $"Called from: {caller}\n" +
            $"File: {file}\n" +
            $"Line: {line}"
        );

        phase = newPhase;
        lastPhase = newPhase;
    }

    public void StartDialogue()
    {
        SetPhase(GamePhase.Talking);
    }

    public void EndDialogue()
    {
        SetPhase(GamePhase.Moving);
    }

    public void StartMiniGame()
    {
        SetPhase(GamePhase.Eventing);
    }

    public void EndMiniGame()
    {
        SetPhase(GamePhase.Moving);
    }
}

public enum GamePhase
{
    Loading,
    Moving,
    Talking,
    Eventing
}