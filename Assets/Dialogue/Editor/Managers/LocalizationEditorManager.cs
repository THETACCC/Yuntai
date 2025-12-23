using UnityEngine;
using UnityEditor;
using System.IO;
using DialogueSystem;

/// <summary>
/// 本地化编辑器管理器 - 负责本地化设置和数据管理
/// </summary>
public class LocalizationEditorManager
{
    private string csvUrlInput = "";

    public string CsvUrlInput
    {
        get => csvUrlInput;
        set => csvUrlInput = value;
    }

    public void LoadLocalizationSettings()
    {
        string path = GetLocalizationSettingsPath();
        if (File.Exists(path))
        {
            try
            {
                string json = File.ReadAllText(path);
                DialogueLocalizationSettings settings = JsonUtility.FromJson<DialogueLocalizationSettings>(json);
                if (settings != null && !string.IsNullOrEmpty(settings.googleSheetsCsvUrl))
                {
                    csvUrlInput = settings.googleSheetsCsvUrl;
                    DialogueLocalization.SetCsvUrl(csvUrlInput);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to load localization settings: {e.Message}");
            }
        }
    }

    public void SaveLocalizationSettings()
    {
        try
        {
            DialogueLocalizationSettings settings = new DialogueLocalizationSettings
            {
                googleSheetsCsvUrl = csvUrlInput
            };

            string path = GetLocalizationSettingsPath();
            string json = JsonUtility.ToJson(settings, true).Trim();

            string folder = Path.GetDirectoryName(path);
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            json = json.Replace("\r\n", "\n");
            System.Text.UTF8Encoding utf8WithoutBom = new System.Text.UTF8Encoding(false);
            File.WriteAllText(path, json, utf8WithoutBom);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to save localization settings: {e.Message}");
        }
    }

    private string GetLocalizationSettingsPath()
    {
        return "Assets/Dialogue/Editor/Data/LocalizationSettings.json";
    }
}