using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Title_Scene : MonoBehaviour
{
    public GameObject savesSlots;
    public List<TextMeshProUGUI> slotsNames;
    public GameObject defaultOptions;
    public Button loadGameBt;

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
        //defaultOptions.SetActive(false);
        Settings.instance.OpenSettings();
    }

    public void NewGame()
    {
        //call start function -- TODO
        int slotNum = SaveManager.instance.FindEmptySlot();
        if (slotNum != 0)
        {
            SaveManager.instance.SlotButtonHit(slotNum);
        } else
        {
            LoadGame();
        }
        
    }

    public void LoadGame()
    {
        //open save slots UI
        defaultOptions.SetActive(false);
        savesSlots.SetActive(true);

        UpdateSlotName();
    }

    public void UpdateSlotName()
    {
        for (int i = 0; i < SaveManager.instance.saveList.Length; i++)
        {
            if (SaveManager.instance.saveList[i])
            {
                slotsNames[i].text = $"Save {i}";
            }
        }
    }

    public void Return()
    {
        defaultOptions.SetActive(true);
        savesSlots.SetActive(false);
        Settings.instance.CloseSettings();
    }

    public void ExitGame()
    {
        
    }
}
