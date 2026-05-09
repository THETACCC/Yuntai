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

    //[HideInInspector] public int deletingSlot;
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
        UpdateSlotInfo();
    }

    public void Return()
    {
        gameSavesUI.SetActive(false);
        if (!Settings.instance.isInGame)
        {
            if (Title_Scene.instance != null)
            {
                Title_Scene.instance.defaultOptions.SetActive(true);
                Title_Scene.instance.isCreatingGame = false;
                Title_Scene.instance.isLoadingGame = false;
            }
        } else
        {
            Settings.instance.OpenSettings();
        }
    }

    public void UpdateSlotInfo()
    {
        for (int i = 0; i < saveList.Length; i++)
        {
            UILocalization nameText = slotsNames[i].GetComponent<UILocalization>();
            UILocalization timeText = slotsTimes[i].GetComponent<UILocalization>();
            if (saveList[i])
            {
                DateTime lastModified = File.GetLastWriteTime(Path.Combine(Application.persistentDataPath, $"GameSave{i + 1}.json"));
                nameText.SetLanguageContent("en", $"{i + 1}. Save #{i + 1}");
                nameText.SetLanguageContent("zh", $"{i + 1}. 存档 #{i + 1}");
                nameText.SetLanguageContent("ja", $"{i + 1}. Save #{i + 1}");
                timeText.SetLanguageContent("en", "Last Modified: " + lastModified.ToString("yyyy-MM-dd HH:mm:ss"));
                timeText.SetLanguageContent("zh", "上次修改: " + lastModified.ToString("yyyy-MM-dd HH:mm:ss"));
                timeText.SetLanguageContent("ja", "Last Modified: " + lastModified.ToString("yyyy-MM-dd HH:mm:ss"));

                //slotsNames[i].text = $"{i + 1}. Save #{i + 1}";
                //slotsTimes[i].text = "Last Modified: " + lastModified.ToString("yyyy-MM-dd HH:mm:ss");
            }
            else
            {
                nameText.SetLanguageContent("en", $"{i + 1}. Empty");
                nameText.SetLanguageContent("zh", $"{i + 1}. 空位");
                nameText.SetLanguageContent("ja", $"{i + 1}. Empty");
                timeText.SetLanguageContent("en", "");
                timeText.SetLanguageContent("zh", "");
                timeText.SetLanguageContent("ja", "");
                //slotsNames[i].text = $"{i + 1}. Empty";
                //slotsTimes[i].text = "";
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
        if (saveSlot != 0)
        {
            UpdateSlotInfo();
            //Settings.instance.OpenGameSaves();


            if (!hasGameSave)
            {
                hasGameSave = true;
            }

            /*
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
            */

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
            UpdateSlotInfo();
        }
    }

    public void LoadGame()
    {
        //UpdateSlotInfo();
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
        UILocalization warningLocalization = warningText.GetComponent<UILocalization>();
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
                    warningLocalization.SetLanguageContent("en", "You are creating a new Game by replacing File " + slotNum);
                    warningLocalization.SetLanguageContent("zh", "你正在通过替换文件" + slotNum + "来创建一个新游戏文件。");
                    warningLocalization.SetLanguageContent("ja", "You are creating a new Game by replacing File " + slotNum);
                    //warningText.text = "You are creating a new Game by replacing File " + slotNum;
                } else
                {
                    //create a new game directly
                    gameSavesUI.SetActive(false);
                    saveList[saveSlot - 1] = true;
                    Settings.instance.isInGame = true;
                    Title_Scene.instance.isCreatingGame = false;
                    SceneController.instance.LoadSceneAndTeleport("InitialCGScene", 0);
                }
            }

            else if (Title_Scene.instance.isLoadingGame)
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

            return;
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
                warningLocalization.SetLanguageContent("en", "You are saving the progress by replacing File " + slotNum);
                warningLocalization.SetLanguageContent("zh", "你正在通过替换文件" + slotNum + "来保存当前的进度。");
                warningLocalization.SetLanguageContent("ja", "You are saving the progress by replacing File " + slotNum);
            }
            else
            {
                //save game directly
                SaveGame();
            }
        }
    }

    public void SetSlotNum(int slotNum)
    {
        saveSlot = slotNum;
    }

    public void CancelReplace()
    {
        warningWindow.SetActive(false);
    }

    public void ConfirmReplace()
    {
        warningWindow.SetActive(false);

        if (Settings.instance.isInGame)
        {
            SaveGame();
        } else
        {
            gameSavesUI.SetActive(false);
            if (Title_Scene.instance.isCreatingGame)
            {
                Settings.instance.isInGame = true;
                Title_Scene.instance.isCreatingGame = false;
                SceneController.instance.LoadSceneAndTeleport("InitialCGScene", 0);
            }          
        }
        
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

    public void DeleteFileSave()
    {
        if (NoteBookManager.instance != null)
        {
            NoteBookManager.instance.ResetAllProgress();
        }

        string path = Path.Combine(Application.persistentDataPath, $"GameSave{saveSlot}.json");

        if (File.Exists(path))
        {
            File.Delete(path);
            saveList[saveSlot - 1] = false;

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

        UpdateSlotInfo();
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
