using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SaveManager : MonoBehaviour
{
    public static SaveManager instance;

    private int saveSlot;
    public string savePath;

    public bool hasGameSave = false;
    public bool[] saveList;

    public void Awake()
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
        saveList = new bool[3];
        CheckExistingSaves();
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

    public void SaveGame()
    {
        if (!hasGameSave)
        {
            hasGameSave = true;
        }
        saveList[saveSlot - 1] = true;

        SaveData data = new SaveData();

        // 存场景名字
        data.sceneName = SceneManager.GetActiveScene().name;

        // 存玩家位置


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

        SceneController.instance.LoadSceneAndTeleport(data.sceneName, 0);
    }

    public void SlotButtonHit(int slotNum)
    {
        saveSlot = slotNum;
        /*
        if (!hasGameSave)
        {
            hasGameSave = true;
        }
        */
        savePath = Path.Combine(Application.persistentDataPath, $"GameSave{saveSlot}.json");

        if (saveList[saveSlot - 1])
        {
            LoadGame();
        } else
        {
            SceneController.instance.LoadSceneAndTeleport("Level1-1", 0);
            saveList[saveSlot - 1] = true;
        }

        Settings.instance.isInGame = true;
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
}

[Serializable]
public class SaveData
{
    public string sceneName;
    
}
