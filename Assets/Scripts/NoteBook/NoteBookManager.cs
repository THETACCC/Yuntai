using MoreMountains.Feedbacks;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NoteBookManager : MonoBehaviour
{
    public static NoteBookManager instance;

    public GameObject NoteBook_Canvas;

    private bool isOpen = false;

    [Header("Data Source")]
    [Tooltip("NoteBook数据表CSV文件")]
    public TextAsset noteBookData;

    //Visual Feedbacks
    [Header("Feedback Reference")]
    public MMFeedbacks NoteBookUpdate;

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
    }


    void Start()
    {

        NoteBook_Canvas.SetActive(false);
        isOpen = false;
    }

    // Update is called once per frame
    void Update()
    {
        if ((Input.GetKeyDown(KeyCode.Tab)) && (Gamemanager.instance.phase != GamePhase.Talking))
        {
            if (!isOpen)
            {
                DisablePlayerMovement();
                NoteBook_Canvas.SetActive(true);
                isOpen = true;
            }
            else
            {
                EnablePlayerMovement();
                NoteBook_Canvas.SetActive(false);
                isOpen = false;
            }
        }
    }

    /// <summary>
    /// 获取NoteBookData的CSV文本
    /// </summary>
    public string GetNoteBookDataText()
    {
        if (noteBookData == null)
        {
            Debug.LogWarning("[NoteBookManager] No noteBookData assigned!");
            return string.Empty;
        }
        return noteBookData.text;
    }

    #region Event Controll
    public void UnlockEvent(int eventNumber)
    {

        NoteBookUpdate?.PlayFeedbacks();
        /*
        if (EventBlocks == null || eventNumber < 0 || eventNumber >= EventBlocks.Length)
        {
            Debug.LogError($"UnlockEvent: index {eventNumber} is out of range.");
            return;
        }

        if (EventBlocks[eventNumber] == null)
        {
            Debug.LogError($"UnlockEvent: EventBlocks[{eventNumber}] is null.");
            return;
        }

        EventBlocks[eventNumber].SetActive(false); // or false if "unlock" = hide
        */
    }


    //Temp solution

    public void UnlockEventFeedBack()
    {
        NoteBookUpdate?.PlayFeedbacks();
    }

    public void UnlockEvent0()
    {
        UnlockEvent(0);
    }

    public void UnlockEvent1()
    {
        UnlockEvent(1);

    }

    public void UnlockEvent2()
    {
        UnlockEvent(2);
    }
    public void UnlockEvent3()
    {
        UnlockEvent(3);
    }
    public void UnlockEvent4()
    {
        UnlockEvent(4);
    }


    #endregion


    #region Player Controll Related

    public void DisablePlayerMovement()
    {
        Gamemanager.instance.phase = GamePhase.Eventing;
    }

    public void EnablePlayerMovement()
    {
        Gamemanager.instance.phase = GamePhase.Moving;
    }


    #endregion




}