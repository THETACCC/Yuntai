using UnityEngine;
using UnityEditor;
using DialogueSystem;

/// <summary>
/// 本地化设置UI绘制器 - 负责本地化设置的可视化
/// </summary>
public class LocalizationSettingsDrawer
{
    private LocalizationEditorManager manager;
    private bool localizationSettingsExpanded = false;
    private Vector2 csvUrlScrollPos;

    public LocalizationSettingsDrawer(LocalizationEditorManager manager)
    {
        this.manager = manager;
    }

    public void DrawLocalizationSettings()
    {
        EditorGUILayout.BeginVertical("box");

        string statusIcon = DialogueLocalization.IsLoaded ? "✓" : "⚠";
        string statusText = DialogueLocalization.IsLoaded
            ? $"{statusIcon} 本地化已加载 ({DialogueLocalization.GetAllIds().Count} 条)"
            : $"{statusIcon} 本地化未加载";

        GUIStyle foldoutStyle = new GUIStyle(EditorStyles.foldout);
        foldoutStyle.fontStyle = FontStyle.Bold;

        localizationSettingsExpanded = EditorGUILayout.Foldout(
            localizationSettingsExpanded,
            statusText,
            true,
            foldoutStyle);

        if (localizationSettingsExpanded)
        {
            EditorGUILayout.Space(5);

            EditorGUILayout.HelpBox(
                "Google Sheets 本地化配置\n" +
                "1. 在Google Sheets: 文件 > 共享 > 发布到网络\n" +
                "2. 选择工作表,格式选 CSV,点击发布\n" +
                "3. 复制链接粘贴到下方\n" +
                "格式: ID, 中文, English, 日语",
                MessageType.Info);

            EditorGUILayout.Space(5);

            EditorGUILayout.LabelField("CSV URL:", EditorStyles.boldLabel);
            csvUrlScrollPos = EditorGUILayout.BeginScrollView(csvUrlScrollPos, GUILayout.Height(50));
            manager.CsvUrlInput = EditorGUILayout.TextArea(manager.CsvUrlInput, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();

            GUI.enabled = !string.IsNullOrEmpty(manager.CsvUrlInput);
            if (GUILayout.Button("保存并加载", GUILayout.Height(25)))
            {
                SaveAndLoadLocalization();
            }
            GUI.enabled = true;

            if (GUILayout.Button("清空", GUILayout.Height(25), GUILayout.Width(60)))
            {
                manager.CsvUrlInput = "";
                DialogueLocalization.SetCsvUrl("");
                manager.SaveLocalizationSettings();
                DialogueLocalization.Clear();
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(5);
    }

    private void SaveAndLoadLocalization()
    {
        manager.CsvUrlInput = manager.CsvUrlInput.Trim();

        if (string.IsNullOrEmpty(manager.CsvUrlInput))
        {
            EditorUtility.DisplayDialog("错误", "请输入CSV URL", "确定");
            return;
        }

        if (!manager.CsvUrlInput.StartsWith("http://") && !manager.CsvUrlInput.StartsWith("https://"))
        {
            EditorUtility.DisplayDialog("错误", "URL必须以 http:// 或 https:// 开头", "确定");
            return;
        }

        DialogueLocalization.SetCsvUrl(manager.CsvUrlInput);
        manager.SaveLocalizationSettings();

        EditorCoroutineRunner.StartCoroutine(DialogueLocalization.LoadFromGoogleSheets((success, message) =>
        {
            if (success)
            {
                EditorUtility.DisplayDialog("成功", message, "确定");
            }
            else
            {
                EditorUtility.DisplayDialog("错误", message, "确定");
            }
        }));
    }
}