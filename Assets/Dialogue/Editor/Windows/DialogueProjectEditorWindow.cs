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

        if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70)))
        {
            RefreshAll();
        }

        if (GUILayout.Button("Save All", EditorStyles.toolbarButton, GUILayout.Width(70)))
        {
            SaveAllDialogueTrees();
        }

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Scan Dialogue Trees", EditorStyles.toolbarButton, GUILayout.Width(150)))
        {
            folderManager.ScanAllDialogueTrees();
            Repaint();
        }

        EditorGUILayout.EndHorizontal();
    }

    private void SaveAllDialogueTrees()
    {
        if (folderManager.GuidToPath == null || folderManager.GuidToPath.Count == 0)
        {
            EditorUtility.DisplayDialog("Save All", "No files found.", "OK");
            return;
        }

        // 第一步：检测所有文件的变化
        Debug.Log("[Save All] 开始检测文件变化...");
        List<FileChangeInfo> changedFiles = DetectAllFileChanges();

        if (changedFiles.Count == 0)
        {
            EditorUtility.DisplayDialog("Save All", "所有文件都已是最新状态，无需保存。", "确定");
            return;
        }

        // 第二步：显示变化列表并让用户确认
        if (!ShowSaveAllConfirmationDialog(changedFiles))
        {
            return; // 用户取消
        }

        // 第三步：执行保存
        ExecuteSaveAll(changedFiles);
    }

    private List<FileChangeInfo> DetectAllFileChanges()
    {
        List<FileChangeInfo> changedFiles = new List<FileChangeInfo>();
        int idx = 0;

        foreach (var kvp in folderManager.GuidToPath)
        {
            idx++;
            string dtreePath = kvp.Value;
            string name = Path.GetFileName(dtreePath);
            EditorUtility.DisplayProgressBar("检测变化", $"{idx}/{folderManager.GuidToPath.Count}: {name}",
                (float)idx / folderManager.GuidToPath.Count);

            try
            {
                if (!File.Exists(dtreePath))
                {
                    changedFiles.Add(new FileChangeInfo
                    {
                        fileName = name,
                        changeType = "文件不存在",
                        hasError = true
                    });
                    continue;
                }

                string currentDtreeContent = File.ReadAllText(dtreePath);
                DialogueTreeData data = JsonUtility.FromJson<DialogueTreeData>(currentDtreeContent);

                if (data == null || data.nodes == null)
                {
                    changedFiles.Add(new FileChangeInfo
                    {
                        fileName = name,
                        changeType = "文件格式错误",
                        hasError = true
                    });
                    continue;
                }

                // 生成更新后的内容
                DialogueTreeData updatedData = ApplyLocalizationUpdates(data);
                string newDtreeContent = JsonUtility.ToJson(updatedData, true).Trim().Replace("\r\n", "\n");

                // 规范化当前文件内容的换行符，以便准确对比
                string normalizedCurrentDtreeContent = currentDtreeContent.Replace("\r\n", "\n").TrimEnd();
                string normalizedNewDtreeContent = newDtreeContent.TrimEnd();

                // 检查 .dtree 是否有变化
                bool dtreeChanged = normalizedCurrentDtreeContent != normalizedNewDtreeContent;

                // 检查 .json 是否有变化
                string runtimePath = Path.ChangeExtension(dtreePath, ".json");
                bool jsonChanged = false;
                if (File.Exists(runtimePath))
                {
                    string currentJsonContent = File.ReadAllText(runtimePath);
                    string newJsonContent = GenerateRuntimeJsonContent(updatedData);
                    
                    // 规范化换行符进行对比
                    string normalizedCurrentJson = currentJsonContent.Replace("\r\n", "\n").TrimEnd();
                    string normalizedNewJson = newJsonContent.TrimEnd();
                    
                    jsonChanged = normalizedCurrentJson != normalizedNewJson;
                }
                else
                {
                    jsonChanged = true; // .json文件不存在，需要生成
                }

                if (dtreeChanged || jsonChanged)
                {
                    List<string> changes = new List<string>();
                    if (dtreeChanged) changes.Add(".dtree");
                    if (jsonChanged) changes.Add(".json");

                    changedFiles.Add(new FileChangeInfo
                    {
                        fileName = name,
                        changeType = string.Join(" + ", changes) + " 需要更新",
                        hasError = false,
                        data = updatedData
                    });
                }
            }
            catch (Exception e)
            {
                changedFiles.Add(new FileChangeInfo
                {
                    fileName = name,
                    changeType = $"检测出错: {e.Message}",
                    hasError = true
                });
            }
        }

        EditorUtility.ClearProgressBar();
        return changedFiles;
    }

    private DialogueTreeData ApplyLocalizationUpdates(DialogueTreeData data)
    {
        // 创建副本以避免修改原始数据
        DialogueTreeData updatedData = JsonUtility.FromJson<DialogueTreeData>(JsonUtility.ToJson(data));

        if (DialogueLocalization.IsLoaded)
        {
            foreach (var node in updatedData.nodes)
            {
                if (node.useContentId && !string.IsNullOrEmpty(node.contentId) && DialogueLocalization.HasId(node.contentId))
                {
                    var locData = DialogueLocalization.GetAllLanguages(node.contentId);
                    if (locData != null)
                    {
                        if (node.content == null) node.content = new LocalizedText();
                        node.content.en = locData.ContainsKey(Language.English) ? locData[Language.English] : "";
                        node.content.zh = locData.ContainsKey(Language.ChineseSimplified) ? locData[Language.ChineseSimplified] : "";
                        node.content.ja = locData.ContainsKey(Language.Japanese) ? locData[Language.Japanese] : "";
                    }
                }

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
                            }
                        }
                    }
                }
            }
        }

        return updatedData;
    }

    private string GenerateRuntimeJsonContent(DialogueTreeData data)
    {
        var runtime = ConvertToRuntime(data);
        var idxMap = new Dictionary<string, int>();
        foreach (var n in data.nodes) idxMap[n.id] = n.index;

        string formattedJson = "{\n  \"conversations\": [\n";

        for (int i = 0; i < runtime.Count; i++)
        {
            var item = runtime[i];
            formattedJson += "    {\n";
            formattedJson += $"      \"index\": {item.index},\n";
            formattedJson += $"      \"name\": {SerializeLocalizedText(item.name, 3)},\n";
            formattedJson += $"      \"avatarAddr\": \"{EscapeJsonString(item.avatarAddr)}\",\n";
            formattedJson += $"      \"isPlayer\": {item.isPlayer.ToString().ToLower()},\n";
            formattedJson += $"      \"content\": {SerializeLocalizedText(item.content, 3)}";

            if (item.conditionalBranches?.Count > 0)
            {
                formattedJson += ",\n      \"conditionalBranches\": [\n";
                for (int j = 0; j < item.conditionalBranches.Count; j++)
                {
                    var branch = item.conditionalBranches[j];
                    formattedJson += "        {\n";
                    formattedJson += $"          \"targetIndex\": {branch.targetIndex},\n";
                    formattedJson += $"          \"priority\": {branch.priority}";
                    if (branch.priority > 0 && branch.conditions?.Count > 0)
                    {
                        formattedJson += ",\n          \"conditions\": [\n";
                        for (int k = 0; k < branch.conditions.Count; k++)
                        {
                            var cond = branch.conditions[k];
                            formattedJson += "            {\n";
                            formattedJson += $"              \"targetObjectName\": \"{EscapeJsonString(cond.targetObjectName)}\",\n";
                            formattedJson += $"              \"componentTypeName\": \"{EscapeJsonString(cond.componentTypeName)}\",\n";
                            formattedJson += $"              \"variableName\": \"{EscapeJsonString(cond.variableName)}\",\n";
                            formattedJson += $"              \"comparison\": \"{cond.comparison}\",\n";
                            formattedJson += $"              \"compareValue\": \"{EscapeJsonString(cond.compareValue)}\"\n";
                            formattedJson += "            }";
                            if (k < branch.conditions.Count - 1) formattedJson += ",";
                            formattedJson += "\n";
                        }
                        formattedJson += "          ],\n";
                        formattedJson += $"          \"conditionLogic\": \"{branch.conditionLogic}\"\n";
                    }
                    else
                    {
                        formattedJson += "\n";
                    }
                    formattedJson += "        }";
                    if (j < item.conditionalBranches.Count - 1) formattedJson += ",";
                    formattedJson += "\n";
                }
                formattedJson += "      ]";
            }
            else
            {
                int next = -1;
                if (!string.IsNullOrEmpty(item.nextNodeId) && idxMap.ContainsKey(item.nextNodeId))
                    next = idxMap[item.nextNodeId];
                formattedJson += $",\n      \"nextIndex\": {next}";
            }

            if (item.choices?.Count > 0)
            {
                formattedJson += ",\n      \"choices\": [\n";
                for (int j = 0; j < item.choices.Count; j++)
                {
                    var ch = item.choices[j];
                    int tgt = -1;
                    if (!string.IsNullOrEmpty(ch.nextNodeId) && idxMap.ContainsKey(ch.nextNodeId))
                        tgt = idxMap[ch.nextNodeId];
                    formattedJson += "        {\n";
                    formattedJson += $"          \"text\": {SerializeLocalizedText(ch.text, 5)},\n";
                    formattedJson += $"          \"targetIndex\": {tgt}";
                    if (ch.conditions?.Count > 0)
                    {
                        formattedJson += ",\n          \"conditions\": [\n";
                        for (int k = 0; k < ch.conditions.Count; k++)
                        {
                            var cond = ch.conditions[k];
                            formattedJson += "            {\n";
                            formattedJson += $"              \"targetObjectName\": \"{EscapeJsonString(cond.targetObjectName)}\",\n";
                            formattedJson += $"              \"componentTypeName\": \"{EscapeJsonString(cond.componentTypeName)}\",\n";
                            formattedJson += $"              \"variableName\": \"{EscapeJsonString(cond.variableName)}\",\n";
                            formattedJson += $"              \"comparison\": \"{cond.comparison}\",\n";
                            formattedJson += $"              \"compareValue\": \"{EscapeJsonString(cond.compareValue)}\"\n";
                            formattedJson += "            }";
                            if (k < ch.conditions.Count - 1) formattedJson += ",";
                            formattedJson += "\n";
                        }
                        formattedJson += "          ],\n";
                        formattedJson += $"          \"conditionLogic\": \"{ch.conditionLogic}\"\n";
                    }
                    else
                    {
                        formattedJson += "\n";
                    }
                    formattedJson += "        }";
                    if (j < item.choices.Count - 1) formattedJson += ",";
                    formattedJson += "\n";
                }
                formattedJson += "      ]";
            }

            if (item.eventCalls?.Count > 0)
            {
                formattedJson += ",\n      \"eventCalls\": [\n";
                for (int j = 0; j < item.eventCalls.Count; j++)
                {
                    var ev = item.eventCalls[j];
                    formattedJson += "        {\n";
                    formattedJson += $"          \"targetObjectID\": \"{EscapeJsonString(ev.targetObjectID)}\",\n";
                    formattedJson += $"          \"targetObjectName\": \"{EscapeJsonString(ev.targetObjectName)}\",\n";
                    formattedJson += $"          \"componentTypeName\": \"{EscapeJsonString(ev.componentTypeName)}\",\n";
                    formattedJson += $"          \"methodName\": \"{EscapeJsonString(ev.methodName)}\",\n";
                    formattedJson += $"          \"parameterType\": \"{ev.parameterType}\",\n";
                    formattedJson += $"          \"stringParameter\": \"{EscapeJsonString(ev.stringParameter)}\",\n";
                    formattedJson += $"          \"intParameter\": {ev.intParameter},\n";
                    formattedJson += $"          \"floatParameter\": {ev.floatParameter},\n";
                    formattedJson += $"          \"boolParameter\": {ev.boolParameter.ToString().ToLower()},\n";
                    formattedJson += $"          \"triggerTiming\": {(int)ev.triggerTiming}\n";
                    formattedJson += "        }";
                    if (j < item.eventCalls.Count - 1) formattedJson += ",";
                    formattedJson += "\n";
                }
                formattedJson += "      ]";
            }

            formattedJson += "\n    }";
            if (i < runtime.Count - 1) formattedJson += ",";
            formattedJson += "\n";
        }
        formattedJson += "  ],\n  \"currentIndex\": 0\n}";

        return formattedJson.Replace("\r\n", "\n");
    }

    private bool ShowSaveAllConfirmationDialog(List<FileChangeInfo> changedFiles)
    {
        string message = $"检测到 {changedFiles.Count} 个文件需要更新:\n\n";

        int displayCount = Mathf.Min(changedFiles.Count, 15);
        for (int i = 0; i < displayCount; i++)
        {
            var info = changedFiles[i];
            string status = info.hasError ? "❌" : "✓";
            message += $"{status} {info.fileName}\n    {info.changeType}\n";
        }

        if (changedFiles.Count > 15)
        {
            message += $"\n... 还有 {changedFiles.Count - 15} 个文件\n";
        }

        int errorCount = changedFiles.Count(f => f.hasError);
        int validCount = changedFiles.Count - errorCount;

        message += $"\n总计: {validCount} 个可更新, {errorCount} 个有错误";
        message += "\n\n是否继续保存？";

        return EditorUtility.DisplayDialog("Save All - 确认更新", message, "保存", "取消");
    }

    private void ExecuteSaveAll(List<FileChangeInfo> changedFiles)
    {
        int saved = 0, failed = 0;
        List<string> errors = new List<string>();

        try
        {
            int idx = 0;
            foreach (var fileInfo in changedFiles)
            {
                idx++;
                EditorUtility.DisplayProgressBar("保存文件", $"{idx}/{changedFiles.Count}: {fileInfo.fileName}",
                    (float)idx / changedFiles.Count);

                if (fileInfo.hasError)
                {
                    failed++;
                    errors.Add(fileInfo.fileName + $" ({fileInfo.changeType})");
                    continue;
                }

                try
                {
                    var kvp = folderManager.GuidToPath.FirstOrDefault(x => Path.GetFileName(x.Value) == fileInfo.fileName);
                    if (kvp.Value == null)
                    {
                        failed++;
                        errors.Add(fileInfo.fileName + " (路径未找到)");
                        continue;
                    }

                    string dtreePath = kvp.Value;

                    // 保存 .dtree
                    string updatedDtree = JsonUtility.ToJson(fileInfo.data, true).Trim().Replace("\r\n", "\n");
                    System.Text.UTF8Encoding utf8WithoutBom = new System.Text.UTF8Encoding(false);
                    File.WriteAllText(dtreePath, updatedDtree, utf8WithoutBom);

                    // 保存运行时 .json
                    string runtimePath = Path.ChangeExtension(dtreePath, ".json");
                    SaveRuntimeJson(runtimePath, fileInfo.data);

                    saved++;
                }
                catch (Exception e)
                {
                    failed++;
                    errors.Add(fileInfo.fileName + $" ({e.Message})");
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.Refresh();

        string msg = $"成功保存: {saved} 个文件 (.dtree + .json)";
        if (failed > 0)
        {
            msg += $"\n失败: {failed} 个\n\n详细信息:\n";
            foreach (var e in errors)
            {
                msg += $"• {e}\n";
            }
        }
        EditorUtility.DisplayDialog("保存完成", msg, "确定");

        RefreshAllOpenEditorWindows();
    }

    // 文件变化信息类
    private class FileChangeInfo
    {
        public string fileName;
        public string changeType;
        public bool hasError;
        public DialogueTreeData data;
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
        var pureIds = new HashSet<string>(changedIds.Select(id =>
        {
            if (id.EndsWith(" (新增)")) return id.Substring(0, id.Length - 5);
            if (id.EndsWith(" (已删除)")) return id.Substring(0, id.Length - 6);
            return id;
        }));

        // 找出 nameId 在 changedIds 里的角色，收集其 characterId（GUID）
        var affectedCharacterIds = new HashSet<string>();
        var charLib = characterManager.CharacterLibrary;
        if (charLib?.characters != null)
        {
            foreach (var ch in charLib.characters)
            {
                if (ch.useNameId && !string.IsNullOrEmpty(ch.nameId) && pureIds.Contains(ch.nameId))
                    affectedCharacterIds.Add(ch.id);
            }
        }

        foreach (var kvp in folderManager.GuidToPath)
        {
            string path = kvp.Value;
            if (!path.EndsWith(".dtree")) continue;

            try
            {
                string jsonContent = File.ReadAllText(path);

                // 检查 content/choice ID
                bool affected = pureIds.Any(id => jsonContent.Contains($"\"{id}\""));

                // 检查角色 nameId 变化（.dtree 里存的是 characterId GUID）
                if (!affected && affectedCharacterIds.Count > 0)
                    affected = affectedCharacterIds.Any(cid => jsonContent.Contains($"\"{cid}\""));

                if (affected)
                    affectedFiles.Add(Path.GetFileName(path));
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
                    string updatedJson = JsonUtility.ToJson(data, true).Trim().Replace("\r\n", "\n");
                    System.Text.UTF8Encoding utf8WithoutBom = new System.Text.UTF8Encoding(false);
                    if (!File.Exists(dtreePath) || File.ReadAllText(dtreePath) != updatedJson)
                        File.WriteAllText(dtreePath, updatedJson, utf8WithoutBom);

                    // 同时重新生成运行时 .json 文件
                    string runtimeJsonPath = Path.ChangeExtension(dtreePath, ".json");
                    SaveRuntimeJson(runtimeJsonPath, data);

                    Debug.Log($"[Localization] 已同时更新 .dtree 和 .json 文件: {fileName}");
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

    // ===== Character 修改后重新生成受影响的运行时 JSON =====

    /// <summary>
    /// 静态入口：Character 保存后调用，找到当前窗口实例并触发重新生成
    /// </summary>
    public static void OnCharacterSaved(string modifiedCharacterId)
    {
        var window = Resources.FindObjectsOfTypeAll<DialogueProjectEditorWindow>().FirstOrDefault();
        if (window != null)
        {
            window.RegenerateAffectedRuntimeJSON(modifiedCharacterId);
        }
        else
        {
            Debug.LogWarning("[Character] DialogueProjectEditorWindow not open, skipping runtime JSON regeneration.");
        }
    }

    private void RegenerateAffectedRuntimeJSON(string modifiedCharacterId)
    {
        int regeneratedCount = 0;
        List<string> affectedFiles = new List<string>();

        foreach (var kvp in folderManager.GuidToPath)
        {
            string dtreePath = kvp.Value;

            try
            {
                string dtreeJson = File.ReadAllText(dtreePath);
                DialogueTreeData treeData = JsonUtility.FromJson<DialogueTreeData>(dtreeJson);

                if (treeData != null && treeData.nodes != null)
                {
                    bool usesCharacter = treeData.nodes.Any(node =>
                        !string.IsNullOrEmpty(node.characterId) && node.characterId == modifiedCharacterId);

                    if (usesCharacter)
                    {
                        string jsonPath = Path.ChangeExtension(dtreePath, ".json");

                        if (File.Exists(jsonPath))
                        {
                            SaveRuntimeJson(jsonPath, treeData);
                            affectedFiles.Add(Path.GetFileName(dtreePath));
                            regeneratedCount++;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Character] Failed to check/regenerate {Path.GetFileName(dtreePath)}: {e.Message}");
            }
        }

        if (regeneratedCount > 0)
        {
            AssetDatabase.Refresh();
            Debug.Log($"[Character] Auto-regenerated {regeneratedCount} affected runtime JSON file(s)");

            string fileList = string.Join("\n• ", affectedFiles);
            EditorUtility.DisplayDialog("Character Updated",
                $"Character saved!\n\nAuto-regenerated {regeneratedCount} file(s):\n\n• {fileList}",
                "OK");
        }
    }

    // ===== 运行时 JSON 序列化工具 =====

    private void SaveRuntimeJson(string path, DialogueTreeData data)
    {
        var runtime = ConvertToRuntime(data);
        var idxMap = new Dictionary<string, int>();
        foreach (var n in data.nodes) idxMap[n.id] = n.index;

        string formattedJson = "{\n  \"conversations\": [\n";

        for (int i = 0; i < runtime.Count; i++)
        {
            var item = runtime[i];
            formattedJson += "    {\n";
            formattedJson += $"      \"index\": {item.index},\n";
            formattedJson += $"      \"name\": {SerializeLocalizedText(item.name, 3)},\n";
            formattedJson += $"      \"avatarAddr\": \"{EscapeJsonString(item.avatarAddr)}\",\n";
            formattedJson += $"      \"isPlayer\": {item.isPlayer.ToString().ToLower()},\n";
            formattedJson += $"      \"content\": {SerializeLocalizedText(item.content, 3)}";

            // Conditional Branches
            if (item.conditionalBranches?.Count > 0)
            {
                formattedJson += ",\n      \"conditionalBranches\": [\n";
                for (int j = 0; j < item.conditionalBranches.Count; j++)
                {
                    var branch = item.conditionalBranches[j];
                    formattedJson += "        {\n";
                    formattedJson += $"          \"targetIndex\": {branch.targetIndex},\n";
                    formattedJson += $"          \"priority\": {branch.priority}";
                    if (branch.priority > 0 && branch.conditions?.Count > 0)
                    {
                        formattedJson += ",\n          \"conditions\": [\n";
                        for (int k = 0; k < branch.conditions.Count; k++)
                        {
                            var cond = branch.conditions[k];
                            formattedJson += "            {\n";
                            formattedJson += $"              \"targetObjectName\": \"{EscapeJsonString(cond.targetObjectName)}\",\n";
                            formattedJson += $"              \"componentTypeName\": \"{EscapeJsonString(cond.componentTypeName)}\",\n";
                            formattedJson += $"              \"variableName\": \"{EscapeJsonString(cond.variableName)}\",\n";
                            formattedJson += $"              \"comparison\": \"{cond.comparison}\",\n";
                            formattedJson += $"              \"compareValue\": \"{EscapeJsonString(cond.compareValue)}\"\n";
                            formattedJson += "            }";
                            if (k < branch.conditions.Count - 1) formattedJson += ",";
                            formattedJson += "\n";
                        }
                        formattedJson += "          ],\n";
                        formattedJson += $"          \"conditionLogic\": \"{branch.conditionLogic}\"\n";
                    }
                    else
                    {
                        formattedJson += "\n";
                    }
                    formattedJson += "        }";
                    if (j < item.conditionalBranches.Count - 1) formattedJson += ",";
                    formattedJson += "\n";
                }
                formattedJson += "      ]";
            }
            else
            {
                int next = -1;
                if (!string.IsNullOrEmpty(item.nextNodeId) && idxMap.ContainsKey(item.nextNodeId))
                    next = idxMap[item.nextNodeId];
                formattedJson += $",\n      \"nextIndex\": {next}";
            }

            // Choices
            if (item.choices?.Count > 0)
            {
                formattedJson += ",\n      \"choices\": [\n";
                for (int j = 0; j < item.choices.Count; j++)
                {
                    var ch = item.choices[j];
                    int tgt = -1;
                    if (!string.IsNullOrEmpty(ch.nextNodeId) && idxMap.ContainsKey(ch.nextNodeId))
                        tgt = idxMap[ch.nextNodeId];
                    formattedJson += "        {\n";
                    formattedJson += $"          \"text\": {SerializeLocalizedText(ch.text, 5)},\n";
                    formattedJson += $"          \"targetIndex\": {tgt}";
                    if (ch.conditions?.Count > 0)
                    {
                        formattedJson += ",\n          \"conditions\": [\n";
                        for (int k = 0; k < ch.conditions.Count; k++)
                        {
                            var cond = ch.conditions[k];
                            formattedJson += "            {\n";
                            formattedJson += $"              \"targetObjectName\": \"{EscapeJsonString(cond.targetObjectName)}\",\n";
                            formattedJson += $"              \"componentTypeName\": \"{EscapeJsonString(cond.componentTypeName)}\",\n";
                            formattedJson += $"              \"variableName\": \"{EscapeJsonString(cond.variableName)}\",\n";
                            formattedJson += $"              \"comparison\": \"{cond.comparison}\",\n";
                            formattedJson += $"              \"compareValue\": \"{EscapeJsonString(cond.compareValue)}\"\n";
                            formattedJson += "            }";
                            if (k < ch.conditions.Count - 1) formattedJson += ",";
                            formattedJson += "\n";
                        }
                        formattedJson += "          ],\n";
                        formattedJson += $"          \"conditionLogic\": \"{ch.conditionLogic}\"\n";
                    }
                    else
                    {
                        formattedJson += "\n";
                    }
                    formattedJson += "        }";
                    if (j < item.choices.Count - 1) formattedJson += ",";
                    formattedJson += "\n";
                }
                formattedJson += "      ]";
            }

            // Event Calls
            if (item.eventCalls?.Count > 0)
            {
                formattedJson += ",\n      \"eventCalls\": [\n";
                for (int j = 0; j < item.eventCalls.Count; j++)
                {
                    var ev = item.eventCalls[j];
                    formattedJson += "        {\n";
                    formattedJson += $"          \"targetObjectID\": \"{EscapeJsonString(ev.targetObjectID)}\",\n";
                    formattedJson += $"          \"targetObjectName\": \"{EscapeJsonString(ev.targetObjectName)}\",\n";
                    formattedJson += $"          \"componentTypeName\": \"{EscapeJsonString(ev.componentTypeName)}\",\n";
                    formattedJson += $"          \"methodName\": \"{EscapeJsonString(ev.methodName)}\",\n";
                    formattedJson += $"          \"parameterType\": \"{ev.parameterType}\",\n";
                    formattedJson += $"          \"stringParameter\": \"{EscapeJsonString(ev.stringParameter)}\",\n";
                    formattedJson += $"          \"intParameter\": {ev.intParameter},\n";
                    formattedJson += $"          \"floatParameter\": {ev.floatParameter},\n";
                    formattedJson += $"          \"boolParameter\": {ev.boolParameter.ToString().ToLower()},\n";
                    formattedJson += $"          \"triggerTiming\": {(int)ev.triggerTiming}\n";
                    formattedJson += "        }";
                    if (j < item.eventCalls.Count - 1) formattedJson += ",";
                    formattedJson += "\n";
                }
                formattedJson += "      ]";
            }

            formattedJson += "\n    }";
            if (i < runtime.Count - 1) formattedJson += ",";
            formattedJson += "\n";
        }
        formattedJson += "  ],\n  \"currentIndex\": 0\n}";

        formattedJson = formattedJson.Replace("\r\n", "\n");
        if (File.Exists(path) && File.ReadAllText(path) == formattedJson)
            return;
        System.Text.UTF8Encoding utf8WithoutBom = new System.Text.UTF8Encoding(false);
        File.WriteAllText(path, formattedJson, utf8WithoutBom);
    }

    private List<RuntimeDialogueData> ConvertToRuntime(DialogueTreeData data)
    {
        var result = new List<RuntimeDialogueData>();
        var idxMap = new Dictionary<string, int>();
        var nodes = data.nodes.OrderBy(n => n.index).ToList();
        foreach (var n in nodes) idxMap[n.id] = n.index;

        var charLib = characterManager.CharacterLibrary;

        foreach (var node in nodes)
        {
            var rt = new RuntimeDialogueData
            {
                index = node.index,
                content = node.content ?? new LocalizedText(),
                eventCalls = new List<DialogueEventCall>(node.eventCalls ?? new List<DialogueEventCall>())
            };

            if (!string.IsNullOrEmpty(node.characterId) && charLib?.characters != null)
            {
                var ch = Array.Find(charLib.characters, c => c.id == node.characterId);
                if (ch != null)
                {
                    // 支持 useNameId 模式：从本地化表读取角色名
                    if (ch.useNameId && !string.IsNullOrEmpty(ch.nameId) && DialogueLocalization.IsLoaded && DialogueLocalization.HasId(ch.nameId))
                    {
                        var locData = DialogueLocalization.GetAllLanguages(ch.nameId);
                        rt.name = new LocalizedText
                        {
                            en = locData.ContainsKey(Language.English) ? locData[Language.English] : "",
                            zh = locData.ContainsKey(Language.ChineseSimplified) ? locData[Language.ChineseSimplified] : "",
                            ja = locData.ContainsKey(Language.Japanese) ? locData[Language.Japanese] : ""
                        };
                    }
                    else
                    {
                        rt.name = ch.characterName ?? new LocalizedText();
                    }
                    rt.avatarAddr = ConvertResourcePath(ch.avatarAssetPath ?? "");
                    rt.isPlayer = ch.isPlayer;
                }
                else { rt.name = new LocalizedText(); rt.avatarAddr = ""; rt.isPlayer = false; }
            }
            else { rt.name = new LocalizedText(); rt.avatarAddr = ""; rt.isPlayer = false; }

            rt.choices = new List<RuntimeChoice>();
            if (node.choices?.Count > 0)
            {
                foreach (var choice in node.choices)
                {
                    var conn = data.connections.FirstOrDefault(c => c.outputNodeId == node.id && c.choiceIndex == node.choices.IndexOf(choice));
                    rt.choices.Add(new RuntimeChoice
                    {
                        text = choice.text ?? new LocalizedText(),
                        nextNodeId = conn?.inputNodeId ?? "",
                        conditions = new List<ChoiceCondition>(choice.conditions ?? new List<ChoiceCondition>()),
                        conditionLogic = choice.conditionLogic
                    });
                }
            }
            else
            {
                var conn = data.connections.FirstOrDefault(c => c.outputNodeId == node.id && c.choiceIndex == -1);
                rt.nextNodeId = conn?.inputNodeId ?? "";
            }

            rt.conditionalBranches = new List<RuntimeConditionalBranch>();
            if (node.conditionalBranches?.Count > 0)
            {
                foreach (var br in node.conditionalBranches)
                {
                    var conn = data.connections.FirstOrDefault(c => c.outputNodeId == node.id && c.branchPriority == br.priority);
                    if (conn != null && idxMap.ContainsKey(conn.inputNodeId))
                    {
                        rt.conditionalBranches.Add(new RuntimeConditionalBranch
                        {
                            targetIndex = idxMap[conn.inputNodeId],
                            priority = br.priority,
                            conditions = new List<ChoiceCondition>(br.conditions ?? new List<ChoiceCondition>()),
                            conditionLogic = br.conditionLogic
                        });
                    }
                }
            }

            result.Add(rt);
        }
        return result;
    }

    private string ConvertResourcePath(string path)
    {
        if (string.IsNullOrEmpty(path)) return "";
        int idx = path.IndexOf("Resources/");
        if (idx >= 0)
        {
            string sub = path.Substring(idx + 10);
            return Path.ChangeExtension(sub, null);
        }
        return Path.GetFileNameWithoutExtension(path);
    }

    private string SerializeLocalizedText(LocalizedText text, int indentLevel = 2)
    {
        if (text == null) text = new LocalizedText();
        string indent = new string(' ', indentLevel * 2);
        return "{\n" +
               $"{indent}  \"en\": \"{EscapeJsonString(text.en)}\",\n" +
               $"{indent}  \"zh\": \"{EscapeJsonString(text.zh)}\",\n" +
               $"{indent}  \"ja\": \"{EscapeJsonString(text.ja)}\"\n" +
               $"{indent}}}";
    }

    private string EscapeJsonString(string str)
    {
        if (string.IsNullOrEmpty(str)) return "";
        return str.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
    }
}