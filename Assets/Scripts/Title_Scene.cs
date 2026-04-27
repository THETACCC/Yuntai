using Fungus;
using MoreMountains.Feedbacks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Title_Scene : MonoBehaviour
{
    public static Title_Scene instance;
    public GameObject defaultOptions;
    public GameObject loadGameBt;
    public bool isCreatingGame = false;
    public bool isLoadingGame = false;

    private void Awake()
    {
        instance = this;
    }

    private void Start()
    {
        defaultOptions.SetActive(true);
        if (SaveManager.instance != null )
        {
            loadGameBt.SetActive(SaveManager.instance.hasGameSave);
        }
        Settings.instance.isInGame = false;
    }

    private void Update()
    {
        if (SaveManager.instance != null)
        {
            loadGameBt.SetActive(SaveManager.instance.hasGameSave);
        }
    }

    public void OpenSettings()
    {
        //defaultOptions.SetActive(false);
        Settings.instance.OpenSettings();
    }

    public void NewGame()
    {
        isCreatingGame = true;
        //open save slots UI
        defaultOptions.SetActive(false);
        SaveManager.instance.gameSavesUI.SetActive(true);
    }

    public void LoadGame()
    {
        isLoadingGame = true;
        //open save slots UI
        defaultOptions.SetActive(false);
        SaveManager.instance.gameSavesUI.SetActive(true);
    }





    public void ExitGame()
    {
#if UNITY_EDITOR
        // 如果在 Unity 编辑器里运行，停止播放
        UnityEditor.EditorApplication.isPlaying = false;
#else
        // 如果是 Build 后的游戏，真正退出应用
        Application.Quit();
#endif
    }

}
