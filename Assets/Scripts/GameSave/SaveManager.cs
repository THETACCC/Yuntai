using Fungus;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SaveManager : MonoBehaviour
{
    public static SaveManager instance;
    private string savePath;

    public bool hasGameSave = false;
    public bool[] saveList;

    public void Awake()
    {
        instance = this;
        saveList = new bool[3];
    }

    public void SaveGame()
    {
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

    public SaveData LoadGame()
    {
        if (!File.Exists(savePath))
        {
            Debug.LogWarning("没有存档文件！");
            return null;
        }

        string json = File.ReadAllText(savePath);
        SaveData data = JsonUtility.FromJson<SaveData>(json);
        return data;
    }

    public void SlotButtonHit(int slotNum)
    {
        Debug.Log(saveList.Length);
        saveList[slotNum - 1] = true;
        if (!hasGameSave)
        {
            hasGameSave = true;
        }

        savePath = Path.Combine(Application.persistentDataPath, $"GameSave{slotNum}.json");
    }
}

[Serializable]
public class SaveData
{
    public string sceneName;
    
}
