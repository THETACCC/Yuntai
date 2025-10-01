using Fungus;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SaveManager : MonoBehaviour
{
    private string savePath;

    public void Awake()
    {
        savePath = Path.Combine(Application.persistentDataPath, "GameSave.json");
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
}

[Serializable]
public class SaveData
{
    public string sceneName;
    
}
