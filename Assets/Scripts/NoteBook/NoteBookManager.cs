using MoreMountains.Feedbacks;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NoteBookManager : MonoBehaviour
{
    public static NoteBookManager instance;

    public CanvasGroup NoteBook_Canvas;

    public bool isOpen = false;

    [Header("Data Source")]
    [Tooltip("NoteBook数据表CSV文件（如果使用本地文件）")]
    public TextAsset noteBookData;

    [Tooltip("Google Sheets发布的CSV URL（如果使用在线表格）")]
    public string googleSheetsURL = "";

    private string noteBookDataText = "";

    // 数据是否已加载完成
    public bool IsDataReady { get; private set; } = false;

    //tab reference
    [Header("Tab Reference")]
    public TabManager objectiveTab;
    public TabManager characterTab;
    public TabManager eventTab;

    //Visual Feedbacks
    [Header("Feedback Reference")]
    public MMFeedbacks NoteBookUpdate;

    //Audio
    public AudioSource OpenNoteBook;

    //
    [Header("NoteBook Master Controll")]
    public bool allowOpen = false;

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
        NoteBook_Canvas.alpha = 0;
        isOpen = false;

        // 加载NoteBookData
        if (!string.IsNullOrEmpty(googleSheetsURL))
        {
            StartCoroutine(LoadNoteBookDataFromURL(googleSheetsURL));
        }
        else if (noteBookData != null)
        {
            noteBookDataText = noteBookData.text;
            IsDataReady = true;  // 本地数据立即就绪
        }
    }

    private System.Collections.IEnumerator LoadNoteBookDataFromURL(string url)
    {
        using (UnityEngine.Networking.UnityWebRequest www = UnityEngine.Networking.UnityWebRequest.Get(url))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[NoteBookManager] Failed to load from URL: {www.error}");
            }
            else
            {
                noteBookDataText = www.downloadHandler.text;
                IsDataReady = true;
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        if(allowOpen)
        {
            if ((Input.GetKeyDown(KeyCode.Tab)) && (Gamemanager.instance.phase != GamePhase.Talking))
            {
                if (!isOpen)
                {
                    if (OpenNoteBook) OpenNoteBook.Play();
                    DisablePlayerMovement();
                    NoteBook_Canvas.alpha = 1;
                    isOpen = true;
                }
                else
                {
                    EnablePlayerMovement();
                    NoteBook_Canvas.alpha = 0;
                    isOpen = false;
                }
            }
        }

    }

    public void enableNoteBook()
    {
        allowOpen = true;
    }

    public void disableNoteBook()
    {
        allowOpen = false;
    }

    public void closeNoteBook()
    {
        isOpen = false;
    }

    /// <summary>
    /// 获取NoteBookData的CSV文本
    /// </summary>
    public string GetNoteBookDataText()
    {
        if (!string.IsNullOrEmpty(noteBookDataText))
        {
            return noteBookDataText;
        }

        if (noteBookData != null)
        {
            return noteBookData.text;
        }

        Debug.LogWarning("[NoteBookManager] No noteBookData available!");
        return string.Empty;
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
        Debug.Log("UPDATING LOOGGGGG NTOEBOOK");
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