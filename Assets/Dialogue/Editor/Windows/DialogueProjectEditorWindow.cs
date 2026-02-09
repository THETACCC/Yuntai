using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System;
using DialogueSystem;

/// <summary>
/// 对话项目编辑器主窗口 - 管理对话树、角色库和本地化
/// </summary>
public class DialogueProjectEditorWindow : EditorWindow
{
    // Managers - 业务逻辑层
    private CharacterLibraryManager characterManager;
    private VirtualFolderManager folderManager;
    private LocalizationEditorManager localizationManager;

    // Drawers - UI绘制层
    private CharacterListDrawer characterDrawer;
    private FolderTreeDrawer folderDrawer;
    private LocalizationSettingsDrawer localizationDrawer;

    // UI State
    private Vector2 scrollPos;

    // 本地化变化检测
    private Dictionary<string, Dictionary<Language, string>> previousLocalizationData =
        new Dictionary<string, Dictionary<Language, string>>();

    [MenuItem("Tools/Dialogue System/Manager Window")]
    public static void ShowWindow()
    {
        var window = GetWindow<DialogueProjectEditorWindow>();
        window.titleContent = new GUIContent("Dialogue Manager");
        window.minSize = new Vector2(400, 500);
        window.Show();
    }

    private void OnEnable()
    {
        // 1. 初始化Managers
        characterManager = new CharacterLibraryManager();
        folderManager = new VirtualFolderManager();
        localizationManager = new LocalizationEditorManager();

        // 2. 加载数据 (必须在初始化Drawers之前！)
        folderManager.LoadVirtualFolderStructure();
        characterManager.LoadCharacterLibrary();
        characterManager.LoadCharacterFolderStructure();
        localizationManager.LoadLocalizationSettings();
        folderManager.ScanAllDialogueTrees();

        // 3. 初始化Drawers (现在数据已经加载好了)
        characterDrawer = new CharacterListDrawer(characterManager);
        folderDrawer = new FolderTreeDrawer(folderManager);
        localizationDrawer = new LocalizationSettingsDrawer(localizationManager);

        // 4. 初始化本地化
        if (!DialogueLocalization.IsLoaded && !string.IsNullOrEmpty(localizationManager.CsvUrlInput))
        {
            DialogueLocalization.LoadInEditorSync();
        }
    }

    private void OnGUI()
    {
        DrawToolbar();

        localizationDrawer.DrawLocalizationSettings();

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        characterDrawer.DrawCharactersSection();

        EditorGUILayout.Space(5);

        folderDrawer.DrawFolderTree();

        EditorGUILayout.EndScrollView();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("Scan Dialogue Trees", EditorStyles.toolbarButton, GUILayout.Width(150)))
        {
            folderManager.ScanAllDialogueTrees();
            Repaint();
        }

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
        {
            RefreshAll();
        }

        EditorGUILayout.EndHorizontal();
    }

    private void RefreshAll()
    {
        // 保存当前的本地化数据用于对比
        if (DialogueLocalization.IsLoaded)
        {
            previousLocalizationData.Clear();
            foreach (var id in DialogueLocalization.GetAllIds())
            {
                var data = DialogueLocalization.GetAllLanguages(id);
                if (data != null)
                {
                    previousLocalizationData[id] = new Dictionary<Language, string>(data);
                }
            }
        }

        Debug.Log("========== Refreshing All Data ==========");

        // 重新加载本地文件数据
        folderManager.LoadVirtualFolderStructure();
        folderManager.ScanAllDialogueTrees();
        characterManager.LoadCharacterLibrary();
        characterManager.LoadCharacterFolderStructure();

        Debug.Log($"✓ Loaded {folderManager.GuidToPath.Count} dialogue files");
        Debug.Log($"✓ Loaded {characterManager.GetCharacterCount()} characters");

        // 刷新本地化数据（从网上重新加载）
        if (!string.IsNullOrEmpty(DialogueLocalization.GetCsvUrl()))
        {
            EditorCoroutineRunner.StartCoroutine(DialogueLocalization.LoadFromGoogleSheets((success, message) =>
            {
                if (success)
                {
                    Debug.Log($"[Localization] {message}");
                    DetectLocalizationChanges();
                }
                else
                {
                    Debug.LogError($"[Localization] {message}");
                    EditorUtility.DisplayDialog("刷新失败", message, "确定");
                }
            }));
        }
        else
        {
            Debug.Log("Refresh complete! (No localization URL configured)");
        }

        Repaint();
    }

    private void DetectLocalizationChanges()
    {
        if (previousLocalizationData.Count == 0)
        {
            Debug.Log("[Localization] 首次加载，无法检测变化");
            RefreshAllOpenEditorWindows();
            return;
        }

        List<string> changedIds = new List<string>();

        // 检测新增和修改的ID
        foreach (var id in DialogueLocalization.GetAllIds())
        {
            var newData = DialogueLocalization.GetAllLanguages(id);

            if (!previousLocalizationData.ContainsKey(id))
            {
                changedIds.Add(id + " (新增)");
                continue;
            }

            var oldData = previousLocalizationData[id];

            bool hasChange = false;
            foreach (Language lang in System.Enum.GetValues(typeof(Language)))
            {
                string oldText = oldData.ContainsKey(lang) ? oldData[lang] : "";
                string newText = newData.ContainsKey(lang) ? newData[lang] : "";

                if (oldText != newText)
                {
                    hasChange = true;
                    break;
                }
            }

            if (hasChange)
            {
                changedIds.Add(id);
            }
        }

        // 检测删除的ID
        foreach (var oldId in previousLocalizationData.Keys)
        {
            if (!DialogueLocalization.HasId(oldId))
            {
                changedIds.Add(oldId + " (已删除)");
            }
        }

        if (changedIds.Count == 0)
        {
            EditorUtility.DisplayDialog("刷新完成", "本地化数据已刷新，没有检测到变化", "确定");
            RefreshAllOpenEditorWindows();
            return;
        }

        List<string> affectedFiles = FindAffectedDialogueFiles(changedIds);
        ShowChangeDetectionDialog(changedIds, affectedFiles);
    }

    private List<string> FindAffectedDialogueFiles(List<string> changedIds)
    {
        List<string> affectedFiles = new List<string>();

        // 移除 "(新增)" 和 "(已删除)" 后缀
        var pureIds = changedIds.Select(id =>
        {
            if (id.EndsWith(" (新增)")) return id.Substring(0, id.Length - 5);
            if (id.EndsWith(" (已删除)")) return id.Substring(0, id.Length - 6);
            return id;
        }).ToList();

        foreach (var kvp in folderManager.GuidToPath)
        {
            string path = kvp.Value;
            if (!path.EndsWith(".dtree")) continue;

            try
            {
                string jsonContent = File.ReadAllText(path);

                bool hasAffectedId = false;
                foreach (var id in pureIds)
                {
                    if (jsonContent.Contains($"\"{id}\""))
                    {
                        hasAffectedId = true;
                        break;
                    }
                }

                if (hasAffectedId)
                {
                    affectedFiles.Add(Path.GetFileName(path));
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Localization] 无法读取文件 {path}: {e.Message}");
            }
        }

        return affectedFiles;
    }

    private void ShowChangeDetectionDialog(List<string> changedIds, List<string> affectedFiles)
    {
        string message = "检测到以下ID的内容发生变化:\n\n";

        int displayCount = Mathf.Min(changedIds.Count, 10);
        for (int i = 0; i < displayCount; i++)
        {
            message += $"• {changedIds[i]}\n";
        }

        if (changedIds.Count > 10)
        {
            message += $"\n... 还有 {changedIds.Count - 10} 个ID\n";
        }

        message += $"\n共 {changedIds.Count} 个ID发生变化\n";

        if (affectedFiles.Count > 0)
        {
            message += $"\n受影响的对话树文件 ({affectedFiles.Count}个):\n";

            int fileDisplayCount = Mathf.Min(affectedFiles.Count, 5);
            for (int i = 0; i < fileDisplayCount; i++)
            {
                message += $"• {affectedFiles[i]}\n";
            }

            if (affectedFiles.Count > 5)
            {
                message += $"... 还有 {affectedFiles.Count - 5} 个文件\n";
            }

            message += "\n是否要重新保存这些对话树文件？\n(这将同时更新 .dtree 和 .json 文件)";

            if (EditorUtility.DisplayDialog("检测到本地化变化", message, "重新保存", "取消"))
            {
                ResaveAffectedFiles(affectedFiles);
            }
            else
            {
                RefreshAllOpenEditorWindows();
            }
        }
        else
        {
            message += "\n没有找到使用这些ID的对话树文件。";
            EditorUtility.DisplayDialog("检测到本地化变化", message, "确定");
            RefreshAllOpenEditorWindows();
        }
    }

    private void ResaveAffectedFiles(List<string> fileNames)
    {
        int savedCount = 0;

        foreach (var fileName in fileNames)
        {
            var kvp = folderManager.GuidToPath.FirstOrDefault(x => Path.GetFileName(x.Value) == fileName);
            if (kvp.Value == null) continue;

            string dtreePath = kvp.Value;

            try
            {
                if (!File.Exists(dtreePath))
                {
                    Debug.LogWarning($"[Localization] 文件不存在: {dtreePath}");
                    continue;
                }

                string jsonContent = File.ReadAllText(dtreePath);
                DialogueTreeData data = JsonUtility.FromJson<DialogueTreeData>(jsonContent);

                if (data == null)
                {
                    Debug.LogWarning($"[Localization] 无法解析文件: {dtreePath}");
                    continue;
                }

                // 用DialogueLocalization中的最新数据更新所有LocalizedText（只更新Use ID模式的内容）
                bool hasUpdates = false;
                foreach (var node in data.nodes)
                {
                    // 只在Use ID模式时更新节点内容
                    if (node.useContentId && !string.IsNullOrEmpty(node.contentId) && DialogueLocalization.HasId(node.contentId))
                    {
                        var locData = DialogueLocalization.GetAllLanguages(node.contentId);
                        if (locData != null)
                        {
                            if (node.content == null) node.content = new LocalizedText();
                            node.content.en = locData.ContainsKey(Language.English) ? locData[Language.English] : "";
                            node.content.zh = locData.ContainsKey(Language.ChineseSimplified) ? locData[Language.ChineseSimplified] : "";
                            node.content.ja = locData.ContainsKey(Language.Japanese) ? locData[Language.Japanese] : "";
                            hasUpdates = true;
                        }
                    }

                    // 只在Use ID模式时更新选项文本
                    if (node.choices != null)
                    {
                        foreach (var choice in node.choices)
                        {
                            if (choice.useTextId && !string.IsNullOrEmpty(choice.textId) && DialogueLocalization.HasId(choice.textId))
                            {
                                var locData = DialogueLocalization.GetAllLanguages(choice.textId);
                                if (locData != null)
                                {
                                    if (choice.text == null) choice.text = new LocalizedText();
                                    choice.text.en = locData.ContainsKey(Language.English) ? locData[Language.English] : "";
                                    choice.text.zh = locData.ContainsKey(Language.ChineseSimplified) ? locData[Language.ChineseSimplified] : "";
                                    choice.text.ja = locData.ContainsKey(Language.Japanese) ? locData[Language.Japanese] : "";
                                    hasUpdates = true;
                                }
                            }
                        }
                    }
                }

                if (hasUpdates)
                {
                    // 保存更新后的.dtree文件
                    string updatedJson = JsonUtility.ToJson(data, true).Trim();
                    updatedJson = updatedJson.Replace("\r\n", "\n");
                    System.Text.UTF8Encoding utf8WithoutBom = new System.Text.UTF8Encoding(false);
                    File.WriteAllText(dtreePath, updatedJson, utf8WithoutBom);

                    Debug.Log($"[Localization] 已更新文件: {fileName}");
                    savedCount++;
                }
                else
                {
                    Debug.LogWarning($"[Localization] {fileName} 没有需要更新的本地化数据");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[Localization] 保存文件时出错 {fileName}: {e.Message}");
            }
        }

        EditorUtility.DisplayDialog("保存完成", $"成功更新 {savedCount} 个对话树文件", "确定");
        AssetDatabase.Refresh();

        RefreshAllOpenEditorWindows();
    }

    private void RefreshAllOpenEditorWindows()
    {
        var editorWindows = Resources.FindObjectsOfTypeAll<DialogueTreeEditor>();

        if (editorWindows.Length > 0)
        {
            Debug.Log($"[Localization] 正在刷新 {editorWindows.Length} 个打开的编辑器窗口...");

            foreach (var editor in editorWindows)
            {
                if (editor != null)
                {
                    editor.RefreshLanguageDisplay();
                }
            }
        }
    }
}