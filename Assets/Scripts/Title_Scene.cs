using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Title_Scene : MonoBehaviour
{
    public GameObject savesSlots;
    public GameObject defaultOptions;
    public Button loadGameBt;
    public GameObject Settings;

    private void Start()
    {
        defaultOptions.SetActive(true);
        savesSlots.SetActive(false);
        if (SaveManager.instance != null )
        {
            loadGameBt.gameObject.SetActive(SaveManager.instance.hasGameSave);
        }
        
    }

    private void Update()
    {
        if (SaveManager.instance != null)
        {
            loadGameBt.gameObject.SetActive(SaveManager.instance.hasGameSave);
        }
    }

    public void OpenSettings()
    {
        defaultOptions.SetActive(false);
        Settings.SetActive(true);
    }

    public void NewGame()
    {
        //call start function -- TODO

        //open save slots UI
        defaultOptions.SetActive(false);
        savesSlots.SetActive(true);
    }

    public void Return()
    {
        defaultOptions.SetActive(true);
        savesSlots.SetActive(false);
        Settings.SetActive(false);
    }

    public void ExitGame()
    {
        
    }
}
