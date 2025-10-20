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
                    Repaint();
                }
            }
        }

        // 显示状态栏
        if (!string.IsNullOrEmpty(currentFilePath))
        {
            string status = hasUnsavedChanges ? " *" : "";
            string fileName = Path.GetFileName(currentFilePath);
            string protectionStatus = hasSerializedData ? " [Protected]" : "";
            GUI.Label(new Rect(10, 5, 600, 20), $"Current File: {fileName}{status}{protectionStatus}");
        }
        else if (hasUnsavedChanges)
        {
            GUI.Label(new Rect(10, 5, 600, 20), "Unsaved Changes * (Press Ctrl+S to save)");
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
                    EditorApplication.delayCall += () => { if (graphView != null) graphView.CenterOnNode0(); };
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
        if (!string.IsNullOrEmpty(currentFilePath) && File.Exists(currentFilePath))
        {
            string projectPath = Application.dataPath;
            string projectDirectory = Directory.GetParent(projectPath).FullName;
            if (currentFilePath.StartsWith(projectDirectory) || Path.IsPathRooted(currentFilePath))
            {
                LoadFromFile(currentFilePath);
            }
            else
            {
                currentFilePath = "";
                EditorPrefs.DeleteKey(CURRENT_FILE_KEY);
            }
        }
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
        graphView = new DialogueGraphView();
        graphView.SetEditorWindow(this);
        graphView.style.flexGrow = 1;
        graphView.graphViewChanged += OnGraphViewChanged;

        // 添加键盘事件监听
        rootVisualElement.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);

        rootVisualElement.Add(graphView);
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
        Repaint();  // 强制重绘以更新状态栏
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
        Repaint();
    }
    #endregion

    #region Save/Load
    public void SaveDialogueTree()
    {
        if (string.IsNullOrEmpty(currentFilePath))
        {
            SaveAsDialogueTree();
        }
        else
        {
            SaveToFile(currentFilePath, false);
        }
    }

    public void SaveAsDialogueTree()
    {
        string path = EditorUtility.SaveFilePanel("Save Dialogue Tree",
            Path.Combine(Application.dataPath, "StreamingAssets"),
            string.IsNullOrEmpty(currentFilePath) ? "DialogueSequence" : Path.GetFileNameWithoutExtension(currentFilePath),
            "json");

        if (!string.IsNullOrEmpty(path))
        {
            SaveToFile(path, false);
            currentFilePath = path;
            EditorPrefs.SetString(CURRENT_FILE_KEY, currentFilePath);
        }
    }

    private void SaveToFile(string path, bool isAutoSave)
    {
        if (graphView == null) return;

        string directory = Path.GetDirectoryName(path);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        try
        {
            SaveRuntimeJsonFile(path);
            string dtreePath = Path.ChangeExtension(path, ".dtree");
            SaveEditorFormatFile(dtreePath);

            System.IO.File.SetLastWriteTime(path, System.DateTime.Now);
            System.IO.File.SetLastWriteTime(dtreePath, System.DateTime.Now);

            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            EditorApplication.delayCall += () =>
            {
                if (path.StartsWith(Application.dataPath))
                {
                    string relativePath = "Assets" + path.Substring(Application.dataPath.Length);
                    AssetDatabase.ImportAsset(relativePath);
                }
                if (dtreePath.StartsWith(Application.dataPath))
                {
                    string relativeTreePath = "Assets" + dtreePath.Substring(Application.dataPath.Length);
                    AssetDatabase.ImportAsset(relativeTreePath);
                }
            };

            hasUnsavedChanges = false;
            Repaint();  // 更新状态栏

            if (!isAutoSave)
            {
                Debug.Log($"Dialogue tree saved to: {path}");
                Debug.Log($"Editor file saved to: {dtreePath}");
                EditorUtility.DisplayDialog("Save Successful",
                    $"Runtime file saved to:\n{path}\n\nEditor file saved to:\n{dtreePath}", "OK");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to save dialogue tree: {e.Message}");
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

        File.WriteAllText(path, formattedJson);
    }

    private void SaveEditorFormatFile(string path)
    {
        DialogueTreeData treeData = graphView.SerializeDialogueTree();
        string json = JsonUtility.ToJson(treeData, true);
        File.WriteAllText(path, json);
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
            string jsonPath = Path.ChangeExtension(path, ".json");
            if (File.Exists(jsonPath))
            {
                currentFilePath = jsonPath;
            }
            else
            {
                currentFilePath = path;
            }
            EditorPrefs.SetString(CURRENT_FILE_KEY, currentFilePath);
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

                string jsonPath = Path.ChangeExtension(path, ".json");
                currentFilePath = File.Exists(jsonPath) ? jsonPath : path;
                EditorPrefs.SetString(CURRENT_FILE_KEY, currentFilePath);

                EditorApplication.delayCall += () => {
                    if (graphView != null)
                    {
                        graphView.CenterOnNode0();
                    }
                };

                Debug.Log($"Dialogue tree loaded from: {path}");
                Repaint();
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
}