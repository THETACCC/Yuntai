using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SaveManager : MonoBehaviour
{
    public static SaveManager instance;

    public int saveSlot;
    public string savePath;

    public bool hasGameSave = false;
    public bool[] saveList;

    //public bool isSaving = false;

    [Header("References")]
    public GameObject gameSavesUI;
    public List<TextMeshProUGUI> slotsNames;
    public List<TextMeshProUGUI> slotsTimes;
    public GameObject warningWindow;
    public TextMeshProUGUI warningText;


    public void Awake()
    {
        
        if (instance == null)
        {
            instance = this;
            saveList = new bool[3];
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
        
        
    }

    private void Start()
    {
        CheckExistingSaves();
        gameSavesUI.SetActive(false);
        warningWindow.SetActive(false);
    }

    public void Return()
    {
        if (!Settings.instance.isInGame)
        {
            if (Title_Scene.instance != null)
            {
                Title_Scene.instance.defaultOptions.SetActive(true);
                Title_Scene.instance.isCreatingGame = false;
                Title_Scene.instance.isLoadingGame = false;
            }
        }
        gameSavesUI.SetActive(false);
    }

    public void UpdateSlotInfo()
    {
        for (int i = 0; i < saveList.Length; i++)
        {
            if (saveList[i])
            {
                DateTime lastModified = File.GetLastWriteTime(Path.Combine(Application.persistentDataPath, $"GameSave{i + 1}.json"));
                slotsNames[i].text = $"{i + 1}. Save #{i + 1}";
                slotsTimes[i].text = "Last Modified: " + lastModified.ToString("yyyy-MM-dd HH:mm:ss");
            }
            else
            {
                slotsNames[i].text = $"{i + 1}. Empty";
                slotsTimes[i].text = "";
            }
        }
    }

    private void CheckExistingSaves()
    {
        for (int i = 0; i < saveList.Length; i++)
        {
            string path = Path.Combine(Application.persistentDataPath, $"GameSave{i + 1}.json");
            if (File.Exists(path))
            {
                saveList[i] = true;
                hasGameSave = true; // 至少有一个存档就标记为true
                Debug.Log($"检测到已有存档：{path}");
            }
            else
            {
                saveList[i] = false;
            }
        }
    }

    public void SaveGame(bool isAutoSave = false)
    {
        SaveManager.instance.UpdateSlotInfo();
        if (!hasGameSave)
        {
            hasGameSave = true;
        }
        if (saveSlot == 0)
        {
            saveSlot = FindEmptySlot();
            //没有空存档
            if (saveSlot == 0)
            {
                if (isAutoSave)
                {
                    Debug.Log("AutoSave skipped: no slot selected and all slots full.");
                    return;
                }
                else
                {
                    Settings.instance.OpenGameSaves();
                    return;
                }
            }
        }
        
        // 确保 savePath 被正确初始化
        savePath = Path.Combine(Application.persistentDataPath, $"GameSave{saveSlot}.json");
        
        saveList[saveSlot - 1] = true;

        SaveData data = new SaveData();

        // 存场景名字
        data.sceneName = SceneManager.GetActiveScene().name;

        // 存玩家位置

        //存Notebook
        data.objectiveUnlocked = NoteBookManager.instance.objectiveTab.GetUnlockedStatusArray();
        data.characterUnlocked = NoteBookManager.instance.characterTab.GetUnlockedStatusArray();
        data.eventUnlocked = NoteBookManager.instance.eventTab.GetUnlockedStatusArray();

        // 转换为 JSON
        string json = JsonUtility.ToJson(data, true);

        // 写入文件
        File.WriteAllText(savePath, json);

        Debug.Log("存档完成：" + savePath);
    }

    public void LoadGame()
    {
        if (!File.Exists(savePath))
        {
            Debug.LogWarning("没有存档文件！");
        }

        string json = File.ReadAllText(savePath);
        SaveData data = JsonUtility.FromJson<SaveData>(json);

        //load scene
        SceneController.instance.loadingGame = true;
        SceneController.instance.LoadSceneAndTeleport(data.sceneName, 0);

        //load notebook - 使用新的数组方法
        NoteBookManager.instance.objectiveTab.LoadUnlockedStatusArray(data.objectiveUnlocked);
        NoteBookManager.instance.characterTab.LoadUnlockedStatusArray(data.characterUnlocked);
        NoteBookManager.instance.eventTab.LoadUnlockedStatusArray(data.eventUnlocked);

        // 刷新显示
        NoteBookManager.instance.objectiveTab.RefreshUnlockedTabs();
        NoteBookManager.instance.eventTab.RefreshUnlockedTabs();
        NoteBookManager.instance.characterTab.RefreshUnlockedTabs();
    }

    public void SlotButtonHit(int slotNum)
    {
        //titlescene
        if (!Settings.instance.isInGame && Title_Scene.instance != null)
        {
            if (Title_Scene.instance.isCreatingGame)
            {
                saveSlot = slotNum;
                savePath = Path.Combine(Application.persistentDataPath, $"GameSave{saveSlot}.json");

                if (saveList[saveSlot - 1])
                {
                    //ask for replace as a new game
                    warningWindow.SetActive(true);
                    warningText.text = "You are creating a new Game by replacing File " + slotNum;
                } else
                {
                    //create a new game directly
                    SceneController.instance.LoadSceneAndTeleport("InitialCGScene", 0);
                    saveList[saveSlot - 1] = true;
                    Settings.instance.isInGame = true;
                    Title_Scene.instance.isCreatingGame = false;
                    gameSavesUI.SetActive(false);
                }
                
            }

            if (Title_Scene.instance.isLoadingGame)
            {
                saveSlot = slotNum;
                savePath = Path.Combine(Application.persistentDataPath, $"GameSave{saveSlot}.json");
                if (saveList[saveSlot - 1])
                {
                    //load game
                    Title_Scene.instance.isLoadingGame = false;
                    gameSavesUI.SetActive(false);
                    LoadGame();
                    Settings.instance.isInGame = true;
                } else
                {
                    //do nothing as this is loading game and slot is empty
                }
            }
        }
        

        //in game
        if (Settings.instance.isInGame)
        {
            saveSlot = slotNum;
            savePath = Path.Combine(Application.persistentDataPath, $"GameSave{saveSlot}.json");

            if (saveList[saveSlot - 1])
            {
                //ask for replace
                warningWindow.SetActive(true);
                warningText.text = "You are saving the progress by replacing File " + slotNum;
            }
            else
            {
                //save game directly
                SaveGame();
                gameSavesUI.SetActive(false);
            }
        }
    }

    public void CancelReplace()
    {
        warningWindow.SetActive(false);
    }

    public void ConfirmReplace()
    {
        warningWindow.SetActive(false);
        if (!Settings.instance.isInGame && Title_Scene.instance.isCreatingGame)
        {
            SceneController.instance.LoadSceneAndTeleport("InitialCGScene", 0);
            Settings.instance.isInGame = true;
            Title_Scene.instance.isCreatingGame = false;
        }
        if (Settings.instance.isInGame)
        {
            SaveGame();
        }
        gameSavesUI.SetActive(false);
    }

    public int FindEmptySlot()
    {
        for (int i = 0; i < saveList.Length; i++)
        {
            if (!saveList[i])
            {
                return i+1;
            }
        }
        
        return 0;
    }

    public void DeleteFileSave(int slotNum)
    {
        string path = Path.Combine(Application.persistentDataPath, $"GameSave{slotNum}.json");

        if (File.Exists(path))
        {
            File.Delete(path);
            saveList[slotNum - 1] = false;

            // 检查是否还有其他存档
            hasGameSave = false;
            foreach (bool slot in saveList)
            {
                if (slot)
                {
                    hasGameSave = true;
                    break;
                }
            }

        }
    }
}

[Serializable]
public class SaveData
{
    public string sceneName;
    //public int cameraSize;
    
    // 使用bool数组存储解锁状态，按照myTabs数组的索引顺序
    public bool[] objectiveUnlocked;
    public bool[] characterUnlocked;
    public bool[] eventUnlocked;
}
