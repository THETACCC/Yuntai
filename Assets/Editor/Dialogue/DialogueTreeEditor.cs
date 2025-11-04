using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using DialogueSystem;

/// <summary>
/// 对话树编辑器窗口
/// </summary>
public class DialogueTreeEditor : EditorWindow
{
    private DialogueGraphView graphView;
    private string currentFilePath = "";
    private new bool hasUnsavedChanges = false;

    [SerializeField] private string serializedGraphJson = "";
    [SerializeField] private bool hasSerializedData = false;
    [SerializeField] private bool wasUnsaved = false;

    private Label fileNameLabel;
    private Language currentLanguage = Language.English;
    private Button englishButton;
    private Button chineseButton;
    private Button japaneseButton;

    private string CURRENT_FILE_KEY => $"DialogueTreeEditor_CurrentFile_{Application.dataPath.GetHashCode()}";

    #region Menu Items
    [MenuItem("Tools/Dialogue Tree Editor/Open Editor")]
    public static void OpenWindow()
    {
        DialogueTreeEditor window = GetWindow<DialogueTreeEditor>();
        window.titleContent = new GUIContent("Dialogue Tree Editor");
        window.minSize = new Vector2(1000, 600);
        window.Show();
        window.ForceInitialize();
    }

    [MenuItem("Tools/Dialogue Tree Editor/Create New")]
    public static void CreateNewFromMenu()
    {
        DialogueTreeEditor window = GetWindow<DialogueTreeEditor>();
        window.titleContent = new GUIContent("Dialogue Tree Editor");
        window.minSize = new Vector2(1000, 600);
        window.Show();
        window.ForceInitialize();

        if (window.hasUnsavedChanges)
        {
            if (!EditorUtility.DisplayDialog("New Document",
                "You have unsaved changes. Create new document without saving?",
                "Yes", "Cancel"))
            {
                return;
            }
        }
        window.NewDialogueTree();
    }

    [MenuItem("Tools/Dialogue Tree Editor/Load")]
    public static void LoadFromMenu()
    {
        DialogueTreeEditor window = GetWindow<DialogueTreeEditor>();
        window.titleContent = new GUIContent("Dialogue Tree Editor");
        window.minSize = new Vector2(1000, 600);
        window.Show();
        window.ForceInitialize();
        window.LoadDialogueTree();
    }

    [MenuItem("Tools/Dialogue Tree Editor/Save Current #s")]  // 添加快捷键提示
    public static void SaveCurrentFromMenu()
    {
        DialogueTreeEditor window = GetWindow<DialogueTreeEditor>();
        if (window != null && window.graphView != null)
        {
            window.SaveDialogueTree();
        }
    }

    [MenuItem("Tools/Dialogue Tree Editor/Save As...")]
    public static void SaveAsFromMenu()
    {
        DialogueTreeEditor window = GetWindow<DialogueTreeEditor>();
        if (window != null && window.graphView != null)
        {
            window.SaveAsDialogueTree();
        }
        else
        {
            EditorUtility.DisplayDialog("Error", "Please open the Dialogue Tree Editor first.", "OK");
        }
    }
    #endregion

    #region Unity Lifecycle
    private void OnEnable()
    {
        currentFilePath = EditorPrefs.GetString(CURRENT_FILE_KEY, "");
        rootVisualElement.Clear();
        AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
        EditorApplication.delayCall += DelayedInitialize;
    }

    private void OnDisable()
    {
        AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
        if (graphView != null) graphView = null;
    }

    private void OnDestroy()
    {
        CheckUnsavedChangesBeforeClose();
    }

    private void OnGUI()
    {
        // 处理快捷键 - 修复版本
        if (Event.current != null)
        {
            // Ctrl+S 或 Cmd+S (Mac)
            if (Event.current.type == EventType.KeyDown)
            {
                if ((Event.current.control || Event.current.command) && Event.current.keyCode == KeyCode.S)
                {
                    SaveDialogueTree();
                    Event.current.Use();
                }
            }
        }
    }

    // 更新文件名显示
    private void UpdateStatusBar()
    {
        if (fileNameLabel == null) return;

        if (!string.IsNullOrEmpty(currentFilePath))
        {
            // 显示 .dtree 文件名（虽然内部使用 .json 路径）
            string dtreeFileName = Path.ChangeExtension(Path.GetFileName(currentFilePath), ".dtree");

            if (hasUnsavedChanges)
            {
                fileNameLabel.text = "* " + dtreeFileName + " (unsaved)";
                fileNameLabel.style.color = new StyleColor(new Color(1f, 0.8f, 0.4f));  // 橙色
            }
            else
            {
                fileNameLabel.text = dtreeFileName;
                fileNameLabel.style.color = new StyleColor(new Color(0.8f, 0.8f, 0.8f));  // 灰白色
            }

            // Tooltip 显示完整的 .dtree 路径
            string dtreePath = Path.ChangeExtension(currentFilePath, ".dtree");
            fileNameLabel.tooltip = dtreePath;
        }
        else
        {
            fileNameLabel.text = hasUnsavedChanges ? "* Untitled (unsaved)" : "No File";
            fileNameLabel.style.color = hasUnsavedChanges ?
                new StyleColor(new Color(1f, 0.8f, 0.4f)) :
                new StyleColor(new Color(0.6f, 0.6f, 0.6f));
            fileNameLabel.tooltip = "";
        }
    }
    #endregion

    #region Initialization
    private void OnBeforeAssemblyReload()
    {
        if (graphView != null && graphView.GetNodeCount() > 0)
        {
            try
            {
                DialogueTreeData treeData = graphView.SerializeDialogueTree();
                serializedGraphJson = JsonUtility.ToJson(treeData, false);
                hasSerializedData = true;
                wasUnsaved = hasUnsavedChanges;
                Debug.Log($"[Dialogue Editor] Serialized {treeData.nodes.Count} nodes before compilation");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Dialogue Editor] Failed to serialize: {e.Message}");
                hasSerializedData = false;
            }
        }
    }

    private void DelayedInitialize()
    {
        if (rootVisualElement.childCount > 0) return;

        CreateMainLayout();

        if (hasSerializedData && !string.IsNullOrEmpty(serializedGraphJson))
        {
            try
            {
                DialogueTreeData treeData = JsonUtility.FromJson<DialogueTreeData>(serializedGraphJson);
                if (treeData != null && treeData.nodes != null && treeData.nodes.Count > 0)
                {
                    graphView.LoadDialogueTree(treeData);
                    hasUnsavedChanges = wasUnsaved;
                    Debug.Log($"[Dialogue Editor] Restored {treeData.nodes.Count} nodes after compilation");
                    hasSerializedData = false;
                    serializedGraphJson = "";
                    wasUnsaved = false;
                    EditorApplication.delayCall += () => {
                        if (graphView != null)
                        {
                            graphView.CenterOnNode0();
                            graphView.RefreshAllNodesLanguage();
                        }
                    };
                    UpdateStatusBar();  // 更新状态栏显示
                    return;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Dialogue Editor] Failed to restore: {e.Message}");
            }
            finally
            {
                hasSerializedData = false;
                serializedGraphJson = "";
                wasUnsaved = false;
            }
        }

        hasUnsavedChanges = false;
        if (!string.IsNullOrEmpty(currentFilePath))
        {
            // 确保使用 .dtree 文件加载（包含完整的编辑器信息）
            string dtreePath = Path.ChangeExtension(currentFilePath, ".dtree");
            string pathToLoad = File.Exists(dtreePath) ? dtreePath : currentFilePath;

            Debug.Log($"[DialogueTreeEditor] Attempting to restore file: {pathToLoad}");

            if (File.Exists(pathToLoad))
            {
                string projectPath = Application.dataPath;
                string projectDirectory = Directory.GetParent(projectPath).FullName;
                if (pathToLoad.StartsWith(projectDirectory) || Path.IsPathRooted(pathToLoad))
                {
                    LoadFromFile(pathToLoad);
                }
                else
                {
                    currentFilePath = "";
                    EditorPrefs.DeleteKey(CURRENT_FILE_KEY);
                }
            }
            else
            {
                Debug.LogWarning($"[DialogueTreeEditor] File not found: {pathToLoad}");
                currentFilePath = "";
                EditorPrefs.DeleteKey(CURRENT_FILE_KEY);
            }
        }
        UpdateStatusBar();  // 确保状态栏正确显示
    }

    public void ForceInitialize()
    {
        if (rootVisualElement.childCount == 0)
        {
            rootVisualElement.Clear();
            CreateMainLayout();
        }
        titleContent = new GUIContent("Dialogue Tree Editor");
    }

    private void CreateMainLayout()
    {
        // 创建顶部文件名显示栏
        var toolbar = new VisualElement();
        toolbar.style.flexDirection = FlexDirection.Row;
        toolbar.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f, 1f));
        toolbar.style.paddingTop = 5;
        toolbar.style.paddingBottom = 5;
        toolbar.style.paddingLeft = 10;
        toolbar.style.paddingRight = 10;
        toolbar.style.borderBottomWidth = 1;
        toolbar.style.borderBottomColor = new StyleColor(new Color(0.1f, 0.1f, 0.1f, 1f));
        toolbar.style.minHeight = 25;
        toolbar.style.alignItems = Align.Center;

        // 文件名标签
        fileNameLabel = new Label("No File");
        fileNameLabel.style.fontSize = 12;
        fileNameLabel.style.color = new StyleColor(new Color(0.8f, 0.8f, 0.8f));

        toolbar.Add(fileNameLabel);

        // 语言切换按钮
        var spacer = new VisualElement();
        spacer.style.width = 20;
        toolbar.Add(spacer);

        chineseButton = CreateLanguageButton("中文", Language.ChineseSimplified);
        toolbar.Add(chineseButton);

        englishButton = CreateLanguageButton("English", Language.English);
        toolbar.Add(englishButton);

        japaneseButton = CreateLanguageButton("日本語", Language.Japanese);
        toolbar.Add(japaneseButton);

        UpdateLanguageButtonStyles();
        rootVisualElement.Add(toolbar);

        // 创建图形视图
        graphView = new DialogueGraphView();
        graphView.SetEditorWindow(this);
        graphView.style.flexGrow = 1;
        graphView.graphViewChanged += OnGraphViewChanged;

        // 添加键盘事件监听
        rootVisualElement.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);

        rootVisualElement.Add(graphView);

        // 初始化状态显示
        UpdateStatusBar();
    }

    // 新增：处理键盘事件
    private void OnKeyDown(KeyDownEvent evt)
    {
        // Ctrl+S 或 Cmd+S
        if ((evt.ctrlKey || evt.commandKey) && evt.keyCode == KeyCode.S)
        {
            SaveDialogueTree();
            evt.StopPropagation();
            Debug.Log("Ctrl+S detected - Saving...");
        }
    }

    private UnityEditor.Experimental.GraphView.GraphViewChange OnGraphViewChanged(UnityEditor.Experimental.GraphView.GraphViewChange graphViewChange)
    {
        if (graphView != null && graphView.GetNodeCount() > 0)
        {
            MarkAsChanged();
        }
        return graphViewChange;
    }
    #endregion

    #region Public Methods
    public void MarkAsChanged()
    {
        hasUnsavedChanges = true;
        UpdateStatusBar();
    }

    public bool HasUnsavedChanges => hasUnsavedChanges;

    public void NewDialogueTree()
    {
        currentFilePath = "";
        hasUnsavedChanges = false;
        EditorPrefs.DeleteKey(CURRENT_FILE_KEY);

        if (graphView != null)
        {
            graphView.ClearGraph();
        }
        UpdateStatusBar();
    }
    #endregion

    #region Save/Load
    public void SaveDialogueTree()
    {
        Debug.Log("[DialogueTreeEditor] SaveDialogueTree called");

        if (graphView == null)
        {
            Debug.LogError("[DialogueTreeEditor] GraphView is null, cannot save");
            EditorUtility.DisplayDialog("Save Failed", "Editor is not properly initialized.", "OK");
            return;
        }

        if (string.IsNullOrEmpty(currentFilePath))
        {
            Debug.Log("[DialogueTreeEditor] No current file path, opening Save As dialog");
            SaveAsDialogueTree();
        }
        else
        {
            Debug.Log($"[DialogueTreeEditor] Saving to current file: {currentFilePath}");
            SaveToFile(currentFilePath, false);
        }
    }

    public void SaveAsDialogueTree()
    {
        Debug.Log("[DialogueTreeEditor] SaveAsDialogueTree called");

        if (graphView == null)
        {
            Debug.LogError("[DialogueTreeEditor] GraphView is null, cannot save");
            EditorUtility.DisplayDialog("Save Failed", "Editor is not properly initialized.", "OK");
            return;
        }

        string defaultPath = Path.Combine(Application.dataPath, "StreamingAssets");
        Debug.Log($"[DialogueTreeEditor] Default save path: {defaultPath}");

        string path = EditorUtility.SaveFilePanel("Save Dialogue Tree",
            defaultPath,
            string.IsNullOrEmpty(currentFilePath) ? "DialogueSequence" : Path.GetFileNameWithoutExtension(currentFilePath),
            "json");

        if (!string.IsNullOrEmpty(path))
        {
            Debug.Log($"[DialogueTreeEditor] User selected path: {path}");
            SaveToFile(path, false);
            currentFilePath = path;
            EditorPrefs.SetString(CURRENT_FILE_KEY, currentFilePath);
            Debug.Log($"[DialogueTreeEditor] Current file path updated to: {currentFilePath}");
        }
        else
        {
            Debug.Log("[DialogueTreeEditor] User cancelled save dialog");
        }
    }

    private void SaveToFile(string path, bool isAutoSave)
    {
        if (graphView == null)
        {
            Debug.LogError("[DialogueTreeEditor] GraphView is null in SaveToFile");
            return;
        }

        // 确保 path 是 .json 扩展名
        if (!path.EndsWith(".json"))
        {
            Debug.LogWarning($"[DialogueTreeEditor] Path doesn't end with .json: {path}. Converting...");
            path = Path.ChangeExtension(path, ".json");
        }

        string directory = Path.GetDirectoryName(path);
        Debug.Log($"[DialogueTreeEditor] Saving to directory: {directory}");

        if (!Directory.Exists(directory))
        {
            Debug.Log($"[DialogueTreeEditor] Directory doesn't exist, creating: {directory}");
            Directory.CreateDirectory(directory);
        }

        try
        {
            Debug.Log($"[DialogueTreeEditor] Starting save process...");
            Debug.Log($"[DialogueTreeEditor] JSON path: {path}");

            // 保存运行时 JSON 文件
            SaveRuntimeJsonFile(path);
            Debug.Log($"[DialogueTreeEditor] Runtime JSON file saved successfully");

            // 保存编辑器格式文件
            string dtreePath = Path.ChangeExtension(path, ".dtree");
            Debug.Log($"[DialogueTreeEditor] DTREE path: {dtreePath}");
            SaveEditorFormatFile(dtreePath);
            Debug.Log($"[DialogueTreeEditor] Editor DTREE file saved successfully");

            // 验证文件是否存在
            if (File.Exists(path))
            {
                Debug.Log($"[DialogueTreeEditor] Verified: JSON file exists at {path}");
            }
            else
            {
                Debug.LogError($"[DialogueTreeEditor] ERROR: JSON file not found at {path}");
            }

            if (File.Exists(dtreePath))
            {
                Debug.Log($"[DialogueTreeEditor] Verified: DTREE file exists at {dtreePath}");
            }
            else
            {
                Debug.LogError($"[DialogueTreeEditor] ERROR: DTREE file not found at {dtreePath}");
            }

            System.IO.File.SetLastWriteTime(path, System.DateTime.Now);
            System.IO.File.SetLastWriteTime(dtreePath, System.DateTime.Now);

            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            EditorApplication.delayCall += () =>
            {
                if (path.StartsWith(Application.dataPath))
                {
                    string relativePath = "Assets" + path.Substring(Application.dataPath.Length);
                    AssetDatabase.ImportAsset(relativePath);
                    Debug.Log($"[DialogueTreeEditor] Imported asset: {relativePath}");
                }
                if (dtreePath.StartsWith(Application.dataPath))
                {
                    string relativeTreePath = "Assets" + dtreePath.Substring(Application.dataPath.Length);
                    AssetDatabase.ImportAsset(relativeTreePath);
                    Debug.Log($"[DialogueTreeEditor] Imported asset: {relativeTreePath}");
                }
            };

            hasUnsavedChanges = false;
            UpdateStatusBar();  // 更新状态栏

            if (!isAutoSave)
            {
                Debug.Log($"[DialogueTreeEditor] Save completed successfully!");
                EditorUtility.DisplayDialog("Save Successful",
                    $"Runtime JSON file saved to:\n{path}\n\nEditor DTREE file saved to:\n{dtreePath}", "OK");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[DialogueTreeEditor] Failed to save dialogue tree: {e.Message}");
            Debug.LogError($"[DialogueTreeEditor] Stack trace: {e.StackTrace}");
            if (!isAutoSave)
            {
                EditorUtility.DisplayDialog("Save Failed", $"Failed to save dialogue tree:\n{e.Message}", "OK");
            }
        }
    }

    private void SaveRuntimeJsonFile(string path)
    {
        List<RuntimeDialogueData> exportData = graphView.GetDialogueSequence();
        var nodeIdToIndex = new Dictionary<string, int>();
        var allNodes = graphView.nodes.Cast<DialogueNode>().OrderBy(n => n.NodeIndex).ToList();

        foreach (var node in allNodes)
        {
            nodeIdToIndex[node.GetId()] = node.NodeIndex;
        }

        string formattedJson = "{\n  \"conversations\": [\n";
        for (int i = 0; i < exportData.Count; i++)
        {
            var item = exportData[i];
            formattedJson += "    {\n";
            formattedJson += $"      \"index\": {item.index},\n";
            formattedJson += $"      \"name\": \"{EscapeJsonString(item.name)}\",\n";
            formattedJson += $"      \"avatarAddr\": \"{EscapeJsonString(item.avatarAddr)}\",\n";
            formattedJson += $"      \"content\": \"{EscapeJsonString(item.content)}\"";

            // Conditional Branches
            if (item.conditionalBranches != null && item.conditionalBranches.Count > 0)
            {
                formattedJson += ",\n      \"conditionalBranches\": [\n";
                for (int j = 0; j < item.conditionalBranches.Count; j++)
                {
                    var branch = item.conditionalBranches[j];
                    formattedJson += "        {\n";
                    formattedJson += $"          \"targetIndex\": {branch.targetIndex},\n";
                    formattedJson += $"          \"priority\": {branch.priority}";

                    if (branch.priority > 0 && branch.conditions != null && branch.conditions.Count > 0)
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
                int nextIndex = -1;
                if (!string.IsNullOrEmpty(item.nextNodeId) && nodeIdToIndex.ContainsKey(item.nextNodeId))
                {
                    nextIndex = nodeIdToIndex[item.nextNodeId];
                }
                formattedJson += $",\n      \"nextIndex\": {nextIndex}";
            }

            // Choices
            if (item.choices.Count > 0)
            {
                formattedJson += ",\n      \"choices\": [\n";
                for (int j = 0; j < item.choices.Count; j++)
                {
                    var choice = item.choices[j];
                    int targetIndex = !string.IsNullOrEmpty(choice.nextNodeId) && nodeIdToIndex.ContainsKey(choice.nextNodeId)
                        ? nodeIdToIndex[choice.nextNodeId] : -1;

                    formattedJson += "        {\n";
                    formattedJson += $"          \"text\": \"{EscapeJsonString(choice.text)}\",\n";
                    formattedJson += $"          \"targetIndex\": {targetIndex}";

                    if (choice.conditions.Count > 0)
                    {
                        formattedJson += ",\n          \"conditions\": [\n";
                        for (int k = 0; k < choice.conditions.Count; k++)
                        {
                            var cond = choice.conditions[k];
                            formattedJson += "            {\n";
                            formattedJson += $"              \"targetObjectName\": \"{EscapeJsonString(cond.targetObjectName)}\",\n";
                            formattedJson += $"              \"componentTypeName\": \"{EscapeJsonString(cond.componentTypeName)}\",\n";
                            formattedJson += $"              \"variableName\": \"{EscapeJsonString(cond.variableName)}\",\n";
                            formattedJson += $"              \"comparison\": \"{cond.comparison}\",\n";
                            formattedJson += $"              \"compareValue\": \"{EscapeJsonString(cond.compareValue)}\"\n";
                            formattedJson += "            }";
                            if (k < choice.conditions.Count - 1) formattedJson += ",";
                            formattedJson += "\n";
                        }
                        formattedJson += "          ],\n";
                        formattedJson += $"          \"conditionLogic\": \"{choice.conditionLogic}\"\n";
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
            else
            {
                formattedJson += ",\n      \"choices\": []";
            }

            // Events (包含 triggerOnEnd 字段)
            if (item.eventCalls.Count > 0)
            {
                formattedJson += ",\n      \"eventCalls\": [\n";
                for (int j = 0; j < item.eventCalls.Count; j++)
                {
                    var evt = item.eventCalls[j];
                    formattedJson += "        {\n";
                    formattedJson += $"          \"targetObjectName\": \"{EscapeJsonString(evt.targetObjectName)}\",\n";
                    formattedJson += $"          \"componentTypeName\": \"{EscapeJsonString(evt.componentTypeName)}\",\n";
                    formattedJson += $"          \"methodName\": \"{EscapeJsonString(evt.methodName)}\",\n";
                    formattedJson += $"          \"parameterType\": \"{evt.parameterType}\",\n";
                    formattedJson += $"          \"stringParameter\": \"{EscapeJsonString(evt.stringParameter)}\",\n";
                    formattedJson += $"          \"intParameter\": {evt.intParameter},\n";
                    formattedJson += $"          \"floatParameter\": {evt.floatParameter},\n";
                    formattedJson += $"          \"boolParameter\": {evt.boolParameter.ToString().ToLower()},\n";
                    formattedJson += $"          \"triggerOnEnd\": {evt.triggerOnEnd.ToString().ToLower()}\n";
                    formattedJson += "        }";
                    if (j < item.eventCalls.Count - 1) formattedJson += ",";
                    formattedJson += "\n";
                }
                formattedJson += "      ]";
            }
            else
            {
                formattedJson += ",\n      \"eventCalls\": []";
            }

            formattedJson += "\n    }";
            if (i < exportData.Count - 1) formattedJson += ",";
            formattedJson += "\n";
        }
        formattedJson += "  ],\n  \"currentIndex\": 0\n}";

        Debug.Log($"[DialogueTreeEditor] Writing Runtime JSON to: {path}");
        Debug.Log($"[DialogueTreeEditor] JSON length: {formattedJson.Length} characters");
        File.WriteAllText(path, formattedJson);
        Debug.Log($"[DialogueTreeEditor] Runtime JSON write completed");
    }

    private void SaveEditorFormatFile(string path)
    {
        Debug.Log($"[DialogueTreeEditor] Serializing dialogue tree for editor format");
        DialogueTreeData treeData = graphView.SerializeDialogueTree();
        Debug.Log($"[DialogueTreeEditor] Serialized {treeData.nodes.Count} nodes, {treeData.connections.Count} connections");
        string json = JsonUtility.ToJson(treeData, true);
        Debug.Log($"[DialogueTreeEditor] Writing Editor DTREE to: {path}");
        Debug.Log($"[DialogueTreeEditor] DTREE JSON length: {json.Length} characters");
        File.WriteAllText(path, json);
        Debug.Log($"[DialogueTreeEditor] Editor DTREE write completed");
    }

    public void LoadDialogueTree()
    {
        if (hasUnsavedChanges)
        {
            if (!EditorUtility.DisplayDialog("Unsaved Changes",
                "You have unsaved changes. Load new file without saving?",
                "Yes", "Cancel"))
            {
                return;
            }
        }

        string path = EditorUtility.OpenFilePanel("Load Dialogue Tree",
            Path.Combine(Application.dataPath, "StreamingAssets"), "dtree");

        if (!string.IsNullOrEmpty(path))
        {
            LoadFromFile(path);
            // 关键修复：将 .dtree 路径转换为 .json 路径存储
            currentFilePath = Path.ChangeExtension(path, ".json");
            EditorPrefs.SetString(CURRENT_FILE_KEY, currentFilePath);
            Debug.Log($"[DialogueTreeEditor] Loaded from {path}, currentFilePath set to {currentFilePath}");
        }
    }

    public void LoadFromFile(string path)
    {
        if (graphView == null) return;

        try
        {
            string json = File.ReadAllText(path);
            DialogueTreeData treeData = JsonUtility.FromJson<DialogueTreeData>(json);

            if (treeData != null)
            {
                graphView.LoadDialogueTree(treeData);
                hasUnsavedChanges = false;

                // 关键修复：确保 currentFilePath 是 .json 路径
                currentFilePath = Path.ChangeExtension(path, ".json");
                EditorPrefs.SetString(CURRENT_FILE_KEY, currentFilePath);

                EditorApplication.delayCall += () => {
                    if (graphView != null)
                    {
                        graphView.CenterOnNode0();
                        // 加载后刷新语言显示
                        graphView.RefreshAllNodesLanguage();
                    }
                };

                Debug.Log($"Dialogue tree loaded from: {path}");
                Debug.Log($"currentFilePath set to: {currentFilePath}");
                UpdateStatusBar();  // 更新状态栏
            }
            else
            {
                Debug.LogError("Failed to load dialogue tree data or invalid file format");
                EditorUtility.DisplayDialog("Load Failed", "Failed to load dialogue tree data or invalid file format", "OK");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to load dialogue tree: {e.Message}");
            EditorUtility.DisplayDialog("Load Failed", $"Failed to load dialogue tree:\n{e.Message}", "OK");
        }
    }
    #endregion

    #region Helper Methods
    private void CheckUnsavedChangesBeforeClose()
    {
        if (hasUnsavedChanges && graphView != null && graphView.GetNodeCount() > 0)
        {
            int result = EditorUtility.DisplayDialogComplex("Unsaved Changes",
                "You have unsaved changes. What would you like to do?",
                "Save", "Don't Save", "Cancel");

            switch (result)
            {
                case 0:
                    SaveDialogueTree();
                    break;
                case 1:
                    break;
                case 2:
                    Debug.Log("Window closing cancelled by user");
                    break;
            }
        }
    }

    private string EscapeJsonString(string str)
    {
        if (string.IsNullOrEmpty(str)) return "";
        return str.Replace("\\", "\\\\").Replace("\"", "\\\"")
                  .Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
    }
    #endregion

    #region Character Update
    // 刷新所有节点的角色显示
    public void RefreshAllCharacterDisplays()
    {
        if (graphView == null) return;

        var nodes = graphView.nodes.Cast<DialogueNode>().ToList();
        foreach (var node in nodes)
        {
            node.RefreshCharacterDisplay();
        }

        Debug.Log($"[Dialogue Editor] Refreshed character displays for {nodes.Count} nodes");
    }

    // 静态方法：刷新所有打开的对话编辑器
    public static void RefreshAllOpenEditors()
    {
        var windows = Resources.FindObjectsOfTypeAll<DialogueTreeEditor>();
        foreach (var window in windows)
        {
            window.RefreshAllCharacterDisplays();
        }
        Debug.Log($"[Dialogue Editor] Refreshed {windows.Length} open editor window(s)");
    }

    #region Language Management
    private Button CreateLanguageButton(string text, Language language)
    {
        var button = new Button(() => SwitchLanguage(language)) { text = text };
        button.style.height = 20;
        button.style.minWidth = 60;
        button.style.fontSize = 11;
        button.style.marginLeft = 3;
        button.style.marginRight = 3;
        return button;
    }

    private void SwitchLanguage(Language language)
    {
        if (currentLanguage == language) return;
        currentLanguage = language;
        UpdateLanguageButtonStyles();
        if (graphView != null)
        {
            graphView.RefreshAllNodesLanguage();
        }
    }

    private void UpdateLanguageButtonStyles()
    {
        UpdateButtonStyle(englishButton, currentLanguage == Language.English);
        UpdateButtonStyle(chineseButton, currentLanguage == Language.ChineseSimplified);
        UpdateButtonStyle(japaneseButton, currentLanguage == Language.Japanese);
    }

    private void UpdateButtonStyle(Button button, bool isActive)
    {
        if (button == null) return;
        if (isActive)
        {
            button.style.backgroundColor = new StyleColor(new Color(0.3f, 0.5f, 0.8f));
            button.style.color = new StyleColor(Color.white);
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
        }
        else
        {
            button.style.backgroundColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f));
            button.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f));
            button.style.unityFontStyleAndWeight = FontStyle.Normal;
        }
    }

    public Language GetCurrentLanguage()
    {
        return currentLanguage;
    }
    #endregion

    #endregion
}