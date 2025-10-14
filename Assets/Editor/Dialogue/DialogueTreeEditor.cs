using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.Experimental.GraphView;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using DialogueSystem;
using UnityEditor.UIElements;

[System.Serializable]
public class DialogueTreeData
{
    public List<DialogueNodeData> nodes = new List<DialogueNodeData>();
    public List<DialogueConnectionData> connections = new List<DialogueConnectionData>();
}

[System.Serializable]
public class DialogueNodeData
{
    public string id;
    public int index;
    public string name;
    public string avatarAssetPath;
    public string content;
    public float positionX;
    public float positionY;
    public List<ChoiceData> choices = new List<ChoiceData>();
    public List<DialogueEventCall> eventCalls = new List<DialogueEventCall>();
}

[System.Serializable]
public class ChoiceData
{
    public string text;
    public List<ChoiceCondition> conditions = new List<ChoiceCondition>();
    public ConditionLogic conditionLogic = ConditionLogic.AND;
}

[System.Serializable]
public class DialogueConnectionData
{
    public string outputNodeId;
    public string inputNodeId;
    public int choiceIndex;
    public string choiceText;
}

[System.Serializable]
public class RuntimeDialogueData
{
    public int index;
    public string name;
    public string avatarAddr;
    public string content;
    public List<RuntimeChoice> choices = new List<RuntimeChoice>();
    public string nextNodeId;
    public List<DialogueEventCall> eventCalls = new List<DialogueEventCall>();
}

[System.Serializable]
public class RuntimeChoice
{
    public string text;
    public string nextNodeId;
    public List<ChoiceCondition> conditions = new List<ChoiceCondition>();
    public ConditionLogic conditionLogic = ConditionLogic.AND;
}

public class DialogueTreeEditor : EditorWindow
{
    private DialogueGraphView graphView;
    private string currentFilePath = "";
    private new bool hasUnsavedChanges = false;

    // ===== 域重载保护：使用 JSON 字符串保存 =====
    [SerializeField] private string serializedGraphJson = "";
    [SerializeField] private bool hasSerializedData = false;
    [SerializeField] private bool wasUnsaved = false;
    // ==========================================

    private string CURRENT_FILE_KEY => $"DialogueTreeEditor_CurrentFile_{Application.dataPath.GetHashCode()}";

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

    [MenuItem("Tools/Dialogue Tree Editor/Save Current")]
    public static void SaveCurrentFromMenu()
    {
        DialogueTreeEditor window = GetWindow<DialogueTreeEditor>();
        window.titleContent = new GUIContent("Dialogue Tree Editor");
        if (window != null && window.graphView != null)
        {
            window.SaveDialogueTree();
        }
    }

    [MenuItem("Tools/Dialogue Tree Editor/Save As...")]
    public static void SaveAsFromMenu()
    {
        DialogueTreeEditor window = GetWindow<DialogueTreeEditor>();
        window.titleContent = new GUIContent("Dialogue Tree Editor");
        if (window != null && window.graphView != null)
        {
            window.SaveAsDialogueTree();
        }
        else
        {
            EditorUtility.DisplayDialog("Error", "Please open the Dialogue Tree Editor first and create some dialogue nodes.", "OK");
        }
    }

    private void OnEnable()
    {
        currentFilePath = EditorPrefs.GetString(CURRENT_FILE_KEY, "");
        rootVisualElement.Clear();

        // 监听域重载事件
        AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;

        EditorApplication.delayCall += DelayedInitialize;
    }

    // ===== 域重载前：保存数据到 JSON 字符串 =====
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

                // 调试：打印第一个节点的位置
                if (treeData.nodes.Count > 0)
                {
                    var firstNode = treeData.nodes[0];
                    Debug.Log($"[Dialogue Editor] Saved Node 0 at position: ({firstNode.positionX}, {firstNode.positionY})");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Dialogue Editor] Failed to serialize graph data: {e.Message}");
                hasSerializedData = false;
            }
        }
    }
    // ===========================================

    private void DelayedInitialize()
    {
        if (rootVisualElement.childCount > 0)
            return;

        CreateMainLayout();

        // ===== 域重载后：从 JSON 字符串恢复数据 =====
        if (hasSerializedData && !string.IsNullOrEmpty(serializedGraphJson))
        {
            try
            {
                DialogueTreeData treeData = JsonUtility.FromJson<DialogueTreeData>(serializedGraphJson);

                if (treeData != null && treeData.nodes != null && treeData.nodes.Count > 0)
                {
                    // 调试：打印恢复的节点位置
                    var firstNode = treeData.nodes[0];
                    Debug.Log($"[Dialogue Editor] Restoring Node 0 at position: ({firstNode.positionX}, {firstNode.positionY})");

                    graphView.LoadDialogueTree(treeData);
                    hasUnsavedChanges = wasUnsaved;

                    Debug.Log($"[Dialogue Editor] Restored {treeData.nodes.Count} nodes after compilation");

                    // 清理序列化数据
                    hasSerializedData = false;
                    serializedGraphJson = "";
                    wasUnsaved = false;

                    // 延迟居中到节点0
                    EditorApplication.delayCall += () => {
                        if (graphView != null)
                        {
                            graphView.CenterOnNode0();
                        }
                    };

                    return;
                }
                else
                {
                    Debug.LogError("[Dialogue Editor] Deserialized data is null or empty");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Dialogue Editor] Failed to restore graph data: {e.Message}\n{e.StackTrace}");
            }
            finally
            {
                hasSerializedData = false;
                serializedGraphJson = "";
                wasUnsaved = false;
            }
        }
        // ===========================================

        // 如果没有序列化数据，按原来的方式加载
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

    private void OnDisable()
    {
        // 取消监听域重载事件
        AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;

        if (graphView != null)
        {
            graphView = null;
        }
    }

    private void OnDestroy()
    {
        CheckUnsavedChangesBeforeClose();
    }

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

    private void OnGUI()
    {
        if (focusedWindow == this)
        {
            Event e = Event.current;
            if (e.type == EventType.KeyDown && e.control && e.keyCode == KeyCode.S)
            {
                SaveDialogueTree();
                e.Use();
            }
        }

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

    private void CreateMainLayout()
    {
        graphView = new DialogueGraphView();
        graphView.SetEditorWindow(this);
        graphView.style.flexGrow = 1;
        graphView.graphViewChanged += OnGraphViewChanged;

        rootVisualElement.Add(graphView);
    }

    private GraphViewChange OnGraphViewChanged(GraphViewChange graphViewChange)
    {
        if (graphView != null && graphView.GetNodeCount() > 0)
        {
            MarkAsChanged();
        }
        return graphViewChange;
    }

    public void MarkAsChanged()
    {
        hasUnsavedChanges = true;
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
    }

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
        string path = EditorUtility.SaveFilePanel(
            "Save Dialogue Tree",
            Path.Combine(Application.dataPath, "StreamingAssets"),
            string.IsNullOrEmpty(currentFilePath) ? "DialogueSequence" : Path.GetFileNameWithoutExtension(currentFilePath),
            "json"
        );

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

            int nextIndex = -1;
            if (!string.IsNullOrEmpty(item.nextNodeId) && nodeIdToIndex.ContainsKey(item.nextNodeId))
            {
                nextIndex = nodeIdToIndex[item.nextNodeId];
            }
            formattedJson += $",\n      \"nextIndex\": {nextIndex}";

            if (item.choices.Count > 0)
            {
                formattedJson += ",\n      \"choices\": [\n";
                for (int j = 0; j < item.choices.Count; j++)
                {
                    var choice = item.choices[j];
                    int targetIndex = -1;

                    if (!string.IsNullOrEmpty(choice.nextNodeId) && nodeIdToIndex.ContainsKey(choice.nextNodeId))
                    {
                        targetIndex = nodeIdToIndex[choice.nextNodeId];
                    }

                    formattedJson += "        {\n";
                    formattedJson += $"          \"text\": \"{EscapeJsonString(choice.text)}\",\n";
                    formattedJson += $"          \"targetIndex\": {targetIndex}";

                    if (choice.conditions.Count > 0)
                    {
                        formattedJson += ",\n          \"conditions\": [\n";
                        for (int k = 0; k < choice.conditions.Count; k++)
                        {
                            var condition = choice.conditions[k];
                            formattedJson += "            {\n";
                            formattedJson += $"              \"targetObjectName\": \"{EscapeJsonString(condition.targetObjectName)}\",\n";
                            formattedJson += $"              \"componentTypeName\": \"{EscapeJsonString(condition.componentTypeName)}\",\n";
                            formattedJson += $"              \"variableName\": \"{EscapeJsonString(condition.variableName)}\",\n";
                            formattedJson += $"              \"comparison\": \"{condition.comparison}\",\n";
                            formattedJson += $"              \"compareValue\": \"{EscapeJsonString(condition.compareValue)}\"\n";
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

            if (item.eventCalls.Count > 0)
            {
                formattedJson += ",\n      \"eventCalls\": [\n";
                for (int j = 0; j < item.eventCalls.Count; j++)
                {
                    var eventCall = item.eventCalls[j];
                    formattedJson += "        {\n";
                    formattedJson += $"          \"targetObjectName\": \"{EscapeJsonString(eventCall.targetObjectName)}\",\n";
                    formattedJson += $"          \"componentTypeName\": \"{EscapeJsonString(eventCall.componentTypeName)}\",\n";
                    formattedJson += $"          \"methodName\": \"{EscapeJsonString(eventCall.methodName)}\",\n";
                    formattedJson += $"          \"parameterType\": \"{eventCall.parameterType}\",\n";
                    formattedJson += $"          \"stringParameter\": \"{EscapeJsonString(eventCall.stringParameter)}\",\n";
                    formattedJson += $"          \"intParameter\": {eventCall.intParameter},\n";
                    formattedJson += $"          \"floatParameter\": {eventCall.floatParameter},\n";
                    formattedJson += $"          \"boolParameter\": {eventCall.boolParameter.ToString().ToLower()}\n";
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
        formattedJson += "  ],\n";
        formattedJson += "  \"currentIndex\": 0\n";
        formattedJson += "}";

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

        string path = EditorUtility.OpenFilePanel(
            "Load Dialogue Tree",
            Path.Combine(Application.dataPath, "StreamingAssets"),
            "dtree"
        );

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

    private string EscapeJsonString(string str)
    {
        if (string.IsNullOrEmpty(str)) return "";

        return str.Replace("\\", "\\\\")
                  .Replace("\"", "\\\"")
                  .Replace("\n", "\\n")
                  .Replace("\r", "\\r")
                  .Replace("\t", "\\t");
    }
}

public class DialogueGraphView : GraphView
{
    private DialogueTreeEditor editorWindow;
    private int nextNodeIndex = 0;

    public DialogueGraphView()
    {
        SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);

        this.AddManipulator(new ContentDragger());
        this.AddManipulator(new SelectionDragger());
        this.AddManipulator(new RectangleSelector());

        var grid = new GridBackground();
        Insert(0, grid);
        grid.StretchToParentSize();

        this.AddManipulator(new ContextualMenuManipulator(BuildContextualMenu));

        serializeGraphElements = SerializeGraphElementsImplementation;
        canPasteSerializedData = CanPasteSerializedDataImplementation;
        unserializeAndPaste = UnserializeAndPasteImplementation;

        focusable = true;
        RegisterCallback<KeyDownEvent>(OnKeyDown);
        RegisterCallback<MouseDownEvent>(OnMouseDown);
    }

    public void SetEditorWindow(DialogueTreeEditor window)
    {
        editorWindow = window;
    }

    public int GetNodeCount()
    {
        return nodes.ToList().Count;
    }

    public void ClearGraph()
    {
        DeleteElements(graphElements.ToList());
        nextNodeIndex = 0;
    }

    public void CenterOnNode0()
    {
        var node0 = nodes.Cast<DialogueNode>().FirstOrDefault(n => n.NodeIndex == 0);
        if (node0 == null)
        {
            Debug.LogWarning("Node 0 not found for centering");
            return;
        }

        var nodePosition = node0.GetPosition();

        if (float.IsNaN(nodePosition.x) || float.IsNaN(nodePosition.y) ||
            float.IsNaN(nodePosition.width) || float.IsNaN(nodePosition.height))
        {
            Debug.Log("Node 0 position contains NaN values, retrying centering...");
            EditorApplication.delayCall += () => CenterOnNode0();
            return;
        }

        var layoutBounds = node0.layout;
        if (nodePosition.size == Vector2.zero && layoutBounds.size != Vector2.zero)
        {
            nodePosition = layoutBounds;
        }

        var nodeBounds = new Rect(nodePosition.position, nodePosition.size);

        if (nodeBounds.size == Vector2.zero)
        {
            Debug.Log("Node 0 not fully initialized, retrying centering...");
            EditorApplication.delayCall += () => CenterOnNode0();
            return;
        }

        var graphViewBounds = worldBound;

        if (graphViewBounds.width <= 0 || graphViewBounds.height <= 0)
        {
            Debug.Log("GraphView bounds not ready, retrying centering...");
            EditorApplication.delayCall += () => CenterOnNode0();
            return;
        }

        var nodeCenter = nodeBounds.center;

        if (float.IsNaN(nodeCenter.x) || float.IsNaN(nodeCenter.y))
        {
            Debug.LogError("Node center calculation resulted in NaN, using default position");
            nodeCenter = Vector2.zero;
        }

        var viewportCenter = new Vector2(graphViewBounds.width * 0.5f, graphViewBounds.height * 0.5f);

        var currentZoom = contentViewContainer.transform.scale.x;
        if (float.IsNaN(currentZoom) || currentZoom <= 0)
        {
            currentZoom = 1f;
        }

        var targetPosition = -nodeCenter * currentZoom + viewportCenter;

        if (float.IsNaN(targetPosition.x) || float.IsNaN(targetPosition.y))
        {
            Debug.LogError("Target position calculation resulted in NaN");
            return;
        }

        UpdateViewTransform(targetPosition, new Vector3(currentZoom, currentZoom, 1f));

        Debug.Log($"Successfully centered on Node 0 at position: {nodeCenter}, viewport: {viewportCenter}, zoom: {currentZoom}, target: {targetPosition}");
    }

    private void OnMouseDown(MouseDownEvent evt)
    {
        Focus();
    }

    private void OnKeyDown(KeyDownEvent evt)
    {
        if (evt.ctrlKey && evt.keyCode == KeyCode.S)
        {
            if (editorWindow != null)
            {
                editorWindow.SaveDialogueTree();
                evt.StopPropagation();
            }
        }
        else if (evt.keyCode == KeyCode.Delete)
        {
            DeleteSelectedElements();
            if (editorWindow != null)
            {
                editorWindow.MarkAsChanged();
            }
            evt.StopPropagation();
        }
        else if (evt.ctrlKey && evt.keyCode == KeyCode.D)
        {
            DuplicateSelectedNodes();
            if (editorWindow != null)
            {
                editorWindow.MarkAsChanged();
            }
            evt.StopPropagation();
        }
    }

    public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
    {
        evt.menu.AppendAction("Create Dialogue Node",
            action => {
                CreateDialogueNode("Character", null, "New Dialogue",
                    GetLocalMousePosition(action.eventInfo.localMousePosition));
                if (editorWindow != null)
                {
                    editorWindow.MarkAsChanged();
                }
            },
            DropdownMenuAction.AlwaysEnabled);

        evt.menu.AppendSeparator();

        evt.menu.AppendAction("Save Current",
            action => {
                if (editorWindow != null)
                {
                    editorWindow.SaveDialogueTree();
                }
            },
            DropdownMenuAction.AlwaysEnabled);

        evt.menu.AppendAction("Save As...",
            action => {
                if (editorWindow != null)
                {
                    editorWindow.SaveAsDialogueTree();
                }
            },
            DropdownMenuAction.AlwaysEnabled);

        evt.menu.AppendAction("Load",
            action => {
                if (editorWindow != null)
                {
                    editorWindow.LoadDialogueTree();
                }
            },
            DropdownMenuAction.AlwaysEnabled);

        evt.menu.AppendSeparator();

        evt.menu.AppendAction("Create New",
            action => {
                if (editorWindow != null)
                {
                    if (editorWindow.HasUnsavedChanges)
                    {
                        if (!EditorUtility.DisplayDialog("New Document",
                            "You have unsaved changes. Create new document without saving?",
                            "Yes", "Cancel"))
                        {
                            return;
                        }
                    }
                    editorWindow.NewDialogueTree();
                }
            },
            DropdownMenuAction.AlwaysEnabled);

        var selectedNodes = selection.OfType<DialogueNode>().ToList();
        if (selectedNodes.Count > 0)
        {
            evt.menu.AppendSeparator();
            evt.menu.AppendAction("Duplicate Selected",
                action => {
                    DuplicateSelectedNodes();
                    if (editorWindow != null)
                    {
                        editorWindow.MarkAsChanged();
                    }
                },
                DropdownMenuAction.AlwaysEnabled);

            evt.menu.AppendAction("Delete Selected",
                action => {
                    DeleteSelectedElements();
                    if (editorWindow != null)
                    {
                        editorWindow.MarkAsChanged();
                    }
                },
                DropdownMenuAction.AlwaysEnabled);
        }
    }

    public DialogueNode CreateDialogueNode(string characterName, Sprite avatarSprite, string content, Vector2 position = default)
    {
        var dialogueNode = new DialogueNode(characterName, avatarSprite, content, nextNodeIndex++, editorWindow);
        dialogueNode.SetPosition(new Rect(position, Vector2.zero));
        dialogueNode.OnNodeChanged += () => {
            if (editorWindow != null)
            {
                editorWindow.MarkAsChanged();
            }
        };
        AddElement(dialogueNode);
        return dialogueNode;
    }

    private Vector2 GetLocalMousePosition(Vector2 mousePosition)
    {
        return contentViewContainer.WorldToLocal(mousePosition);
    }

    public void DeleteSelectedElements()
    {
        var elementsToDelete = selection.OfType<GraphElement>().ToList();
        var nodesToDelete = elementsToDelete.OfType<DialogueNode>().ToList();

        if (nodesToDelete.Count > 0)
        {
            DeleteElements(elementsToDelete);
            ReorganizeNodeIndices();
        }
        else
        {
            DeleteElements(elementsToDelete);
        }
    }

    private void ReorganizeNodeIndices()
    {
        var allNodes = nodes.Cast<DialogueNode>().OrderBy(n => n.NodeIndex).ToList();
        int currentIndex = 0;

        foreach (var node in allNodes)
        {
            node.SetNodeIndex(currentIndex++);
        }

        nextNodeIndex = allNodes.Count;
    }

    public List<RuntimeDialogueData> GetDialogueSequence()
    {
        var exportDict = new Dictionary<string, RuntimeDialogueData>();
        var nodes = this.nodes.Cast<DialogueNode>().ToList();
        var edges = this.edges.ToList();

        foreach (var node in nodes)
        {
            string runtimeAvatarPath = ConvertSpriteToRuntimePath(node.AvatarSprite);

            var exportData = new RuntimeDialogueData
            {
                index = node.NodeIndex,
                name = node.CharacterName,
                avatarAddr = runtimeAvatarPath,
                content = node.DialogueText,
                choices = new List<RuntimeChoice>(),
                nextNodeId = null,
                eventCalls = new List<DialogueEventCall>(node.EventCalls)
            };
            exportDict[node.GetId()] = exportData;
        }

        foreach (var node in nodes)
        {
            var outputConnections = edges.Where(edge => edge.output.node == node).ToList();
            var exportData = exportDict[node.GetId()];

            foreach (var connection in outputConnections)
            {
                var targetNode = connection.input.node as DialogueNode;
                if (targetNode == null) continue;

                int choiceIndex = node.GetChoiceIndexForPort(connection.output);

                if (choiceIndex == -1)
                {
                    exportData.nextNodeId = targetNode.GetId();
                }
                else if (choiceIndex >= 0 && choiceIndex < node.ChoicesData.Count)
                {
                    var choiceData = node.ChoicesData[choiceIndex];
                    var choice = new RuntimeChoice
                    {
                        text = choiceData.text,
                        nextNodeId = targetNode.GetId(),
                        conditions = new List<ChoiceCondition>(choiceData.conditions),
                        conditionLogic = choiceData.conditionLogic
                    };
                    exportData.choices.Add(choice);
                }
            }

            exportData.choices = exportData.choices.OrderBy(c => node.ChoicesData.FindIndex(cd => cd.text == c.text)).ToList();
        }

        return exportDict.Values.OrderBy(d => d.index).ToList();
    }

    private string ConvertSpriteToRuntimePath(Sprite sprite)
    {
        if (sprite == null) return "";

        string assetPath = AssetDatabase.GetAssetPath(sprite);

        int resourcesIndex = assetPath.IndexOf("Resources/");
        if (resourcesIndex >= 0)
        {
            string resourcePath = assetPath.Substring(resourcesIndex + 10);
            resourcePath = Path.ChangeExtension(resourcePath, null);
            return resourcePath;
        }
        else
        {
            Debug.LogWarning($"Avatar sprite '{sprite.name}' at path '{assetPath}' is not in a Resources folder. " +
                           $"It won't be loadable at runtime! Please move it to a Resources folder.");
            return sprite.name;
        }
    }

    public DialogueTreeData SerializeDialogueTree()
    {
        var treeData = new DialogueTreeData();

        try
        {
            var nodes = this.nodes.Cast<DialogueNode>().ToList();
            foreach (var node in nodes)
            {
                if (node == null) continue;

                var nodeData = new DialogueNodeData
                {
                    id = node.GetId(),
                    index = node.NodeIndex,
                    name = node.CharacterName ?? "",
                    avatarAssetPath = node.AvatarSprite != null ? AssetDatabase.GetAssetPath(node.AvatarSprite) : "",
                    content = node.DialogueText ?? "",
                    positionX = node.GetPosition().x,
                    positionY = node.GetPosition().y,
                    choices = new List<ChoiceData>(node.ChoicesData ?? new List<ChoiceData>()),
                    eventCalls = new List<DialogueEventCall>(node.EventCalls ?? new List<DialogueEventCall>())
                };
                treeData.nodes.Add(nodeData);
            }

            var edges = this.edges.ToList();
            foreach (var edge in edges)
            {
                if (edge?.output?.node == null || edge?.input?.node == null) continue;

                var outputNode = edge.output.node as DialogueNode;
                var inputNode = edge.input.node as DialogueNode;

                if (outputNode != null && inputNode != null)
                {
                    int choiceIndex = outputNode.GetChoiceIndexForPort(edge.output);
                    string choiceText = "";

                    if (choiceIndex >= 0 && choiceIndex < outputNode.ChoicesData.Count)
                    {
                        choiceText = outputNode.ChoicesData[choiceIndex].text;
                    }

                    var connectionData = new DialogueConnectionData
                    {
                        outputNodeId = outputNode.GetId(),
                        inputNodeId = inputNode.GetId(),
                        choiceIndex = choiceIndex,
                        choiceText = choiceText
                    };
                    treeData.connections.Add(connectionData);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error during serialization: {e.Message}");
            return new DialogueTreeData();
        }

        return treeData;
    }

    public void LoadDialogueTree(DialogueTreeData treeData)
    {
        DeleteElements(graphElements.ToList());

        var nodeDict = new Dictionary<string, DialogueNode>();
        var sortedNodes = treeData.nodes.OrderBy(n => n.index).ToList();

        Debug.Log($"[LoadDialogueTree] Loading {sortedNodes.Count} nodes");

        foreach (var nodeData in sortedNodes)
        {
            Sprite avatarSprite = null;
            if (!string.IsNullOrEmpty(nodeData.avatarAssetPath))
            {
                avatarSprite = AssetDatabase.LoadAssetAtPath<Sprite>(nodeData.avatarAssetPath);
                if (avatarSprite == null)
                {
                    Debug.LogWarning($"Failed to load sprite at path: {nodeData.avatarAssetPath}");
                }
            }

            Vector2 position = new Vector2(nodeData.positionX, nodeData.positionY);
            Debug.Log($"[LoadDialogueTree] Node {nodeData.index} at position: ({position.x}, {position.y})");

            var node = CreateDialogueNodeWithIndex(nodeData.name, avatarSprite, nodeData.content, position, nodeData.index);
            node.SetId(nodeData.id);
            node.SetChoicesData(nodeData.choices);
            node.SetEventCalls(nodeData.eventCalls);
            nodeDict[nodeData.id] = node;

            // 验证节点位置是否正确设置
            var verifyPos = node.GetPosition();
            Debug.Log($"[LoadDialogueTree] Node {nodeData.index} verified position: ({verifyPos.x}, {verifyPos.y})");
        }

        if (sortedNodes.Count > 0)
        {
            nextNodeIndex = sortedNodes.Max(n => n.index) + 1;
        }
        else
        {
            nextNodeIndex = 0;
        }

        foreach (var connectionData in treeData.connections)
        {
            if (nodeDict.TryGetValue(connectionData.outputNodeId, out var outputNode) &&
                nodeDict.TryGetValue(connectionData.inputNodeId, out var inputNode))
            {
                Port outputPort = null;

                if (connectionData.choiceIndex == -1)
                {
                    outputPort = outputNode.GetDefaultOutputPort();
                }
                else
                {
                    outputPort = outputNode.GetOutputPortByIndex(connectionData.choiceIndex);
                }

                var inputPort = inputNode.GetInputPort();

                if (outputPort != null && inputPort != null)
                {
                    var edge = outputPort.ConnectTo(inputPort);
                    AddElement(edge);
                }
            }
        }

        Debug.Log($"[LoadDialogueTree] Loaded {nodeDict.Count} nodes and {treeData.connections.Count} connections");
    }

    private DialogueNode CreateDialogueNodeWithIndex(string characterName, Sprite avatarSprite, string content, Vector2 position, int index)
    {
        var dialogueNode = new DialogueNode(characterName, avatarSprite, content, index, editorWindow);
        dialogueNode.SetPosition(new Rect(position, Vector2.zero));
        dialogueNode.OnNodeChanged += () => {
            if (editorWindow != null)
            {
                editorWindow.MarkAsChanged();
            }
        };
        AddElement(dialogueNode);
        return dialogueNode;
    }

    private string SerializeGraphElementsImplementation(IEnumerable<GraphElement> elements)
    {
        var selectedNodes = elements.OfType<DialogueNode>().ToList();
        if (selectedNodes.Count == 0) return string.Empty;

        var nodeData = new List<string>();
        foreach (var node in selectedNodes)
        {
            var position = node.GetPosition();
            var choicesStr = string.Join("~", node.ChoicesData.Select(c => c.text));
            var choicesDataStr = JsonUtility.ToJson(new SerializableChoiceDataList { choicesData = node.ChoicesData });
            var eventCallsStr = JsonUtility.ToJson(new SerializableEventCallList { eventCalls = node.EventCalls });
            var avatarPath = node.AvatarSprite != null ? AssetDatabase.GetAssetPath(node.AvatarSprite) : "";
            nodeData.Add($"{node.CharacterName}|{avatarPath}|{node.DialogueText}|{position.x}|{position.y}|{choicesStr}|{choicesDataStr}|{eventCallsStr}");
        }

        return string.Join(";", nodeData);
    }

    private bool CanPasteSerializedDataImplementation(string serializedData)
    {
        return !string.IsNullOrEmpty(serializedData);
    }

    private void UnserializeAndPasteImplementation(string operationName, string serializedData)
    {
        if (string.IsNullOrEmpty(serializedData)) return;

        var nodeDataList = serializedData.Split(';');
        var offset = new Vector2(30, 30);

        foreach (var nodeDataString in nodeDataList)
        {
            var nodeData = nodeDataString.Split('|');
            if (nodeData.Length >= 6)
            {
                var characterName = nodeData[0];
                var avatarPath = nodeData[1];
                var dialogueText = nodeData[2];
                var x = float.Parse(nodeData[3]) + offset.x;
                var y = float.Parse(nodeData[4]) + offset.y;

                Sprite avatarSprite = null;
                if (!string.IsNullOrEmpty(avatarPath))
                {
                    avatarSprite = AssetDatabase.LoadAssetAtPath<Sprite>(avatarPath);
                }

                List<ChoiceData> choicesData = new List<ChoiceData>();
                if (nodeData.Length > 6)
                {
                    try
                    {
                        var choiceDataList = JsonUtility.FromJson<SerializableChoiceDataList>(nodeData[6]);
                        choicesData = choiceDataList.choicesData;
                    }
                    catch
                    {
                    }
                }

                List<DialogueEventCall> eventCalls = new List<DialogueEventCall>();
                if (nodeData.Length > 7)
                {
                    try
                    {
                        var eventCallList = JsonUtility.FromJson<SerializableEventCallList>(nodeData[7]);
                        eventCalls = eventCallList.eventCalls;
                    }
                    catch
                    {
                    }
                }

                var node = CreateDialogueNode(characterName, avatarSprite, dialogueText, new Vector2(x, y));
                node.SetChoicesData(choicesData);
                node.SetEventCalls(eventCalls);
            }
        }
    }

    public void DuplicateSelectedNodes()
    {
        var selectedNodes = selection.OfType<DialogueNode>().ToList();
        if (selectedNodes.Count == 0) return;

        var serializedData = SerializeGraphElementsImplementation(selectedNodes);
        UnserializeAndPasteImplementation("Duplicate", serializedData);
    }

    public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
    {
        return ports.ToList().Where(endPort =>
            endPort.direction != startPort.direction &&
            endPort.node != startPort.node &&
            endPort.portType == startPort.portType).ToList();
    }
}

[System.Serializable]
public class SerializableEventCallList
{
    public List<DialogueEventCall> eventCalls = new List<DialogueEventCall>();
}

[System.Serializable]
public class SerializableChoiceDataList
{
    public List<ChoiceData> choicesData = new List<ChoiceData>();
}

public partial class DialogueNode : Node
{
    private DialogueTreeEditor editorWindow;
    private TextField characterNameField;
    private ObjectField avatarField;
    private TextField dialogueTextField;
    private VisualElement eventsContainer;
    private Button addEventButton;
    private VisualElement choicesContainer;
    private Button addChoiceButton;
    private Port inputPort;
    private Port defaultOutputPort;
    private List<Port> choiceOutputPorts = new List<Port>();
    private string nodeId;
    private int nodeIndex;

    public string CharacterName { get; private set; }
    public Sprite AvatarSprite { get; private set; }
    public string DialogueText { get; private set; }
    public List<ChoiceData> ChoicesData { get; private set; } = new List<ChoiceData>();
    public List<DialogueEventCall> EventCalls { get; private set; } = new List<DialogueEventCall>();
    public int NodeIndex => nodeIndex;

    public event System.Action OnNodeChanged;

    public DialogueNode(string characterName = "Character", Sprite avatarSprite = null, string dialogueText = "New Dialogue", int index = 0, DialogueTreeEditor editor = null)
    {
        this.CharacterName = characterName;
        this.AvatarSprite = avatarSprite;
        this.DialogueText = dialogueText;
        this.nodeIndex = index;
        this.nodeId = System.Guid.NewGuid().ToString();
        this.editorWindow = editor;

        UpdateTitle();

        CreateInputPort();
        CreateDefaultOutputPort();
        CreateCharacterNameField();
        CreateAvatarField();
        CreateDialogueTextField();
        CreateEventsSection();
        CreateChoicesSection();

        RefreshExpandedState();
        RefreshPorts();
    }

    private void UpdateTitle()
    {
        title = $"Node [{nodeIndex}]";
    }

    public void SetNodeIndex(int index)
    {
        nodeIndex = index;
        UpdateTitle();
    }

    private void NotifyChange()
    {
        OnNodeChanged?.Invoke();
    }

    private void CreateInputPort()
    {
        inputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
        inputPort.portName = "Input";
        inputContainer.Add(inputPort);
    }

    private void CreateDefaultOutputPort()
    {
        defaultOutputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
        defaultOutputPort.portName = "Next";
        outputContainer.Add(defaultOutputPort);
    }

    private void CreateCharacterNameField()
    {
        characterNameField = new TextField("Character Name:")
        {
            value = CharacterName
        };

        characterNameField.style.minWidth = 300;

        characterNameField.RegisterValueChangedCallback(evt =>
        {
            CharacterName = evt.newValue;
            NotifyChange();
        });

        mainContainer.Add(characterNameField);
    }

    private void CreateAvatarField()
    {
        var avatarContainer = new VisualElement();

        avatarField = new ObjectField("Avatar Sprite:")
        {
            objectType = typeof(Sprite),
            value = AvatarSprite,
            allowSceneObjects = false
        };

        avatarField.style.minWidth = 300;

        var warningLabel = new Label();
        warningLabel.style.color = new StyleColor(Color.red);
        warningLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        warningLabel.style.paddingLeft = 5;
        warningLabel.style.paddingTop = 2;
        warningLabel.style.display = DisplayStyle.None;

        avatarField.RegisterValueChangedCallback(evt =>
        {
            AvatarSprite = evt.newValue as Sprite;

            if (AvatarSprite != null)
            {
                string assetPath = AssetDatabase.GetAssetPath(AvatarSprite);
                if (!assetPath.Contains("/Resources/"))
                {
                    warningLabel.text = $"⚠ WARNING: '{AvatarSprite.name}' is NOT in a Resources folder!\nPath: {assetPath}\nMove it to a Resources folder or it won't load at runtime!";
                    warningLabel.style.display = DisplayStyle.Flex;
                    Debug.LogError($"Avatar Sprite Error: '{AvatarSprite.name}' at '{assetPath}' is not in a Resources folder and will not be loadable at runtime!");
                }
                else
                {
                    warningLabel.style.display = DisplayStyle.None;
                }
            }
            else
            {
                warningLabel.style.display = DisplayStyle.None;
            }

            NotifyChange();
        });

        avatarContainer.Add(avatarField);
        avatarContainer.Add(warningLabel);
        mainContainer.Add(avatarContainer);

        if (AvatarSprite != null)
        {
            string assetPath = AssetDatabase.GetAssetPath(AvatarSprite);
            if (!assetPath.Contains("/Resources/"))
            {
                warningLabel.text = $"⚠ WARNING: '{AvatarSprite.name}' is NOT in a Resources folder!\nPath: {assetPath}\nMove it to a Resources folder or it won't load at runtime!";
                warningLabel.style.display = DisplayStyle.Flex;
            }
        }
    }

    private void CreateDialogueTextField()
    {
        dialogueTextField = new TextField("Dialogue:")
        {
            value = DialogueText,
            multiline = true
        };

        dialogueTextField.style.minWidth = 300;
        dialogueTextField.style.minHeight = 60;

        dialogueTextField.RegisterValueChangedCallback(evt =>
        {
            DialogueText = evt.newValue;
            NotifyChange();
        });

        mainContainer.Add(dialogueTextField);
    }

    private void CreateEventsSection()
    {
        var eventsLabel = new Label("Events (UnityEvent):");
        eventsLabel.style.marginTop = 10;
        eventsLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        mainContainer.Add(eventsLabel);

        eventsContainer = new VisualElement();
        eventsContainer.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f, 0.3f));
        eventsContainer.style.borderTopColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f));
        eventsContainer.style.borderTopWidth = 1;
        eventsContainer.style.borderBottomColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f));
        eventsContainer.style.borderBottomWidth = 1;
        eventsContainer.style.borderLeftColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f));
        eventsContainer.style.borderLeftWidth = 1;
        eventsContainer.style.borderRightColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f));
        eventsContainer.style.borderRightWidth = 1;
        eventsContainer.style.paddingTop = 5;
        eventsContainer.style.paddingBottom = 5;
        eventsContainer.style.paddingLeft = 5;
        eventsContainer.style.paddingRight = 5;
        eventsContainer.style.marginTop = 2;
        mainContainer.Add(eventsContainer);

        addEventButton = new Button(() =>
        {
            AddEventCall();
            NotifyChange();
        })
        {
            text = "+ Add Event"
        };
        addEventButton.style.marginTop = 2;
        mainContainer.Add(addEventButton);

        UpdateEventsDisplay();
    }

    private void AddEventCall()
    {
        EventCalls.Add(new DialogueEventCall());
        UpdateEventsDisplay();
    }

    private void RemoveEventCall(int index)
    {
        if (index >= 0 && index < EventCalls.Count)
        {
            EventCalls.RemoveAt(index);
            UpdateEventsDisplay();
        }
    }

    private void UpdateEventsDisplay()
    {
        eventsContainer.Clear();

        if (EventCalls.Count == 0)
        {
            var noEventsLabel = new Label("List is Empty");
            noEventsLabel.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f));
            noEventsLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            noEventsLabel.style.paddingLeft = 10;
            noEventsLabel.style.paddingTop = 5;
            noEventsLabel.style.paddingBottom = 5;
            eventsContainer.Add(noEventsLabel);
            return;
        }

        for (int i = 0; i < EventCalls.Count; i++)
        {
            int currentIndex = i;
            var eventCall = EventCalls[i];

            var eventContainer = new VisualElement();
            eventContainer.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f, 0.5f));
            eventContainer.style.marginTop = 3;
            eventContainer.style.paddingTop = 5;
            eventContainer.style.paddingBottom = 5;
            eventContainer.style.paddingLeft = 5;
            eventContainer.style.paddingRight = 5;

            var titleRow = new VisualElement();
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.alignItems = Align.Center;

            var titleLabel = new Label($"Event {i}");
            titleLabel.style.flexGrow = 1;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;

            var removeButton = new Button(() =>
            {
                RemoveEventCall(currentIndex);
                NotifyChange();
            })
            {
                text = "×"
            };
            removeButton.style.width = 20;
            removeButton.style.height = 18;
            removeButton.style.fontSize = 12;

            titleRow.Add(titleLabel);
            titleRow.Add(removeButton);
            eventContainer.Add(titleRow);

            GameObject currentGameObject = null;
            if (!string.IsNullOrEmpty(eventCall.targetObjectName))
            {
                currentGameObject = GameObject.Find(eventCall.targetObjectName);
            }

            var gameObjectField = new ObjectField("Target GameObject:")
            {
                objectType = typeof(GameObject),
                value = currentGameObject,
                allowSceneObjects = true
            };
            gameObjectField.style.marginTop = 3;
            gameObjectField.RegisterValueChangedCallback(evt =>
            {
                if (currentIndex < EventCalls.Count)
                {
                    var selectedGO = evt.newValue as GameObject;
                    EventCalls[currentIndex].targetObjectName = selectedGO != null ? selectedGO.name : "";
                    UpdateEventsDisplay();
                    NotifyChange();
                }
            });
            eventContainer.Add(gameObjectField);

            if (currentGameObject != null)
            {
                var components = currentGameObject.GetComponents<Component>();
                var componentNames = new List<string> { "None" };
                var componentTypes = new List<System.Type> { null };

                foreach (var comp in components)
                {
                    if (comp != null)
                    {
                        componentNames.Add(comp.GetType().Name);
                        componentTypes.Add(comp.GetType());
                    }
                }

                int selectedComponentIndex = 0;
                if (!string.IsNullOrEmpty(eventCall.componentTypeName))
                {
                    selectedComponentIndex = componentNames.IndexOf(eventCall.componentTypeName);
                    if (selectedComponentIndex < 0) selectedComponentIndex = 0;
                }

                var componentDropdown = new PopupField<string>("Component:", componentNames, selectedComponentIndex);
                componentDropdown.style.marginTop = 3;
                componentDropdown.RegisterValueChangedCallback(evt =>
                {
                    if (currentIndex < EventCalls.Count)
                    {
                        int index = componentNames.IndexOf(evt.newValue);
                        EventCalls[currentIndex].componentTypeName = evt.newValue != "None" ? evt.newValue : "";
                        UpdateEventsDisplay();
                        NotifyChange();
                    }
                });
                eventContainer.Add(componentDropdown);

                if (selectedComponentIndex > 0 && componentTypes[selectedComponentIndex] != null)
                {
                    var selectedComponent = currentGameObject.GetComponent(componentTypes[selectedComponentIndex]);
                    if (selectedComponent != null)
                    {
                        var methods = componentTypes[selectedComponentIndex]
                            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly)
                            .Where(m => !m.IsSpecialName && m.GetParameters().Length <= 1)
                            .ToList();

                        var methodNames = new List<string> { "None" };
                        var methodInfos = new List<System.Reflection.MethodInfo> { null };

                        foreach (var method in methods)
                        {
                            var parameters = method.GetParameters();
                            if (parameters.Length == 0)
                            {
                                methodNames.Add(method.Name + " ()");
                                methodInfos.Add(method);
                            }
                            else if (parameters.Length == 1)
                            {
                                var paramType = parameters[0].ParameterType;
                                if (paramType == typeof(int) || paramType == typeof(float) ||
                                    paramType == typeof(string) || paramType == typeof(bool))
                                {
                                    methodNames.Add($"{method.Name} ({paramType.Name})");
                                    methodInfos.Add(method);
                                }
                            }
                        }

                        int selectedMethodIndex = 0;
                        if (!string.IsNullOrEmpty(eventCall.methodName))
                        {
                            for (int j = 0; j < methodInfos.Count; j++)
                            {
                                if (methodInfos[j] != null && methodInfos[j].Name == eventCall.methodName)
                                {
                                    selectedMethodIndex = j;
                                    break;
                                }
                            }
                        }

                        var methodDropdown = new PopupField<string>("Function:", methodNames, selectedMethodIndex);
                        methodDropdown.style.marginTop = 3;
                        methodDropdown.RegisterValueChangedCallback(evt =>
                        {
                            if (currentIndex < EventCalls.Count)
                            {
                                int index = methodNames.IndexOf(evt.newValue);
                                if (index > 0 && methodInfos[index] != null)
                                {
                                    var method = methodInfos[index];
                                    EventCalls[currentIndex].methodName = method.Name;

                                    var parameters = method.GetParameters();
                                    if (parameters.Length == 0)
                                    {
                                        EventCalls[currentIndex].parameterType = ParameterType.None;
                                    }
                                    else if (parameters.Length == 1)
                                    {
                                        var paramType = parameters[0].ParameterType;
                                        if (paramType == typeof(int))
                                            EventCalls[currentIndex].parameterType = ParameterType.Int;
                                        else if (paramType == typeof(float))
                                            EventCalls[currentIndex].parameterType = ParameterType.Float;
                                        else if (paramType == typeof(string))
                                            EventCalls[currentIndex].parameterType = ParameterType.String;
                                        else if (paramType == typeof(bool))
                                            EventCalls[currentIndex].parameterType = ParameterType.Bool;
                                    }
                                }
                                else
                                {
                                    EventCalls[currentIndex].methodName = "";
                                    EventCalls[currentIndex].parameterType = ParameterType.None;
                                }
                                UpdateEventsDisplay();
                                NotifyChange();
                            }
                        });
                        eventContainer.Add(methodDropdown);

                        if (selectedMethodIndex > 0 && methodInfos[selectedMethodIndex] != null)
                        {
                            var parameters = methodInfos[selectedMethodIndex].GetParameters();
                            if (parameters.Length == 1)
                            {
                                var paramContainer = new VisualElement();
                                paramContainer.style.marginTop = 3;
                                paramContainer.style.paddingLeft = 10;

                                var paramType = parameters[0].ParameterType;

                                if (paramType == typeof(string))
                                {
                                    var stringField = new TextField("Parameter:")
                                    {
                                        value = eventCall.stringParameter
                                    };
                                    stringField.RegisterValueChangedCallback(evt =>
                                    {
                                        if (currentIndex < EventCalls.Count)
                                        {
                                            EventCalls[currentIndex].stringParameter = evt.newValue;
                                            NotifyChange();
                                        }
                                    });
                                    paramContainer.Add(stringField);
                                }
                                else if (paramType == typeof(int))
                                {
                                    var intField = new IntegerField("Parameter:")
                                    {
                                        value = eventCall.intParameter
                                    };
                                    intField.RegisterValueChangedCallback(evt =>
                                    {
                                        if (currentIndex < EventCalls.Count)
                                        {
                                            EventCalls[currentIndex].intParameter = evt.newValue;
                                            NotifyChange();
                                        }
                                    });
                                    paramContainer.Add(intField);
                                }
                                else if (paramType == typeof(float))
                                {
                                    var floatField = new FloatField("Parameter:")
                                    {
                                        value = eventCall.floatParameter
                                    };
                                    floatField.RegisterValueChangedCallback(evt =>
                                    {
                                        if (currentIndex < EventCalls.Count)
                                        {
                                            EventCalls[currentIndex].floatParameter = evt.newValue;
                                            NotifyChange();
                                        }
                                    });
                                    paramContainer.Add(floatField);
                                }
                                else if (paramType == typeof(bool))
                                {
                                    var boolField = new Toggle("Parameter:")
                                    {
                                        value = eventCall.boolParameter
                                    };
                                    boolField.RegisterValueChangedCallback(evt =>
                                    {
                                        if (currentIndex < EventCalls.Count)
                                        {
                                            EventCalls[currentIndex].boolParameter = evt.newValue;
                                            NotifyChange();
                                        }
                                    });
                                    paramContainer.Add(boolField);
                                }

                                eventContainer.Add(paramContainer);
                            }
                        }
                    }
                }
            }
            else
            {
                var hintLabel = new Label("Select a GameObject first");
                hintLabel.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f));
                hintLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
                hintLabel.style.marginTop = 3;
                hintLabel.style.paddingLeft = 10;
                eventContainer.Add(hintLabel);
            }

            eventsContainer.Add(eventContainer);
        }
    }

    private void CreateChoicesSection()
    {
        var choicesLabel = new Label("Player Choices:");
        choicesLabel.style.marginTop = 10;
        choicesLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        mainContainer.Add(choicesLabel);

        choicesContainer = new VisualElement();
        mainContainer.Add(choicesContainer);

        addChoiceButton = new Button(() =>
        {
            AddChoice(new ChoiceData { text = "New Choice" });
            NotifyChange();
        })
        {
            text = "Add Choice"
        };
        addChoiceButton.style.marginTop = 5;
        mainContainer.Add(addChoiceButton);
    }

    private void AddChoice(ChoiceData choiceData)
    {
        int index = ChoicesData.Count;
        ChoicesData.Add(choiceData);

        RebuildChoiceUI(index);
        CreateChoiceOutputPort(index, choiceData.text);
        RefreshExpandedState();
        RefreshPorts();
    }

    private void RemoveChoice(int index)
    {
        if (index >= 0 && index < ChoicesData.Count)
        {
            var connectionsToRestore = new List<(int choiceIndex, Port inputPort)>();
            var graphView = GetFirstAncestorOfType<DialogueGraphView>();

            if (graphView != null)
            {
                for (int i = 0; i < choiceOutputPorts.Count; i++)
                {
                    if (i == index) continue;

                    var port = choiceOutputPorts[i];
                    foreach (var edge in port.connections.ToList())
                    {
                        if (edge.input != null)
                        {
                            connectionsToRestore.Add((i, edge.input));
                            graphView.RemoveElement(edge);
                        }
                    }
                }
            }

            if (index < choiceOutputPorts.Count)
            {
                var port = choiceOutputPorts[index];
                if (graphView != null)
                {
                    var edges = port.connections.ToList();
                    foreach (var edge in edges)
                    {
                        graphView.RemoveElement(edge);
                    }
                }
                outputContainer.Remove(port);
                choiceOutputPorts.RemoveAt(index);
            }

            ChoicesData.RemoveAt(index);
            choicesContainer.Clear();

            foreach (var port in choiceOutputPorts)
            {
                outputContainer.Remove(port);
            }
            choiceOutputPorts.Clear();

            for (int i = 0; i < ChoicesData.Count; i++)
            {
                RebuildChoiceUI(i);
                CreateChoiceOutputPort(i, ChoicesData[i].text);
            }

            if (graphView != null)
            {
                foreach (var (oldIndex, inputPort) in connectionsToRestore)
                {
                    int newIndex = oldIndex > index ? oldIndex - 1 : oldIndex;

                    if (newIndex >= 0 && newIndex < choiceOutputPorts.Count)
                    {
                        var newPort = choiceOutputPorts[newIndex];
                        var newEdge = newPort.ConnectTo(inputPort);
                        graphView.AddElement(newEdge);
                    }
                }
            }

            RefreshExpandedState();
            RefreshPorts();
        }
    }

    private void RebuildChoiceUI(int index)
    {
        var choiceContainer = new VisualElement();
        choiceContainer.style.marginTop = 10;
        choiceContainer.style.marginBottom = 5;
        choiceContainer.style.borderLeftWidth = 3;
        choiceContainer.style.borderLeftColor = GetChoiceColor(index);
        choiceContainer.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f, 0.5f));
        choiceContainer.style.paddingTop = 8;
        choiceContainer.style.paddingBottom = 8;
        choiceContainer.style.paddingLeft = 8;
        choiceContainer.style.paddingRight = 8;
        choiceContainer.style.borderTopLeftRadius = 4;
        choiceContainer.style.borderTopRightRadius = 4;
        choiceContainer.style.borderBottomLeftRadius = 4;
        choiceContainer.style.borderBottomRightRadius = 4;

        var titleRow = new VisualElement();
        titleRow.style.flexDirection = FlexDirection.Row;
        titleRow.style.alignItems = Align.Center;
        titleRow.style.marginBottom = 5;
        titleRow.style.paddingBottom = 5;
        titleRow.style.borderBottomWidth = 1;
        titleRow.style.borderBottomColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f));

        var choiceLabel = new Label($"Choice {index + 1}");
        choiceLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        choiceLabel.style.fontSize = 11;
        choiceLabel.style.color = GetChoiceColor(index);
        choiceLabel.style.flexGrow = 1;

        titleRow.Add(choiceLabel);
        choiceContainer.Add(titleRow);

        var inputRow = new VisualElement();
        inputRow.style.flexDirection = FlexDirection.Row;
        inputRow.style.alignItems = Align.Center;
        inputRow.style.flexWrap = Wrap.NoWrap;
        inputRow.style.marginBottom = 5;

        var textLabel = new Label("Text:");
        textLabel.style.width = 40;
        textLabel.style.fontSize = 10;
        textLabel.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f));
        textLabel.style.marginRight = 5;
        textLabel.style.flexShrink = 0;

        var choiceField = new TextField();
        choiceField.value = ChoicesData[index].text;
        choiceField.style.flexGrow = 1;
        choiceField.style.flexShrink = 1;
        choiceField.style.minWidth = 100;

        int currentIndex = index;
        choiceField.RegisterValueChangedCallback(evt =>
        {
            if (currentIndex < ChoicesData.Count)
            {
                ChoicesData[currentIndex].text = evt.newValue;
                if (currentIndex < choiceOutputPorts.Count)
                {
                    choiceOutputPorts[currentIndex].portName = $"{currentIndex + 1}: {evt.newValue}";
                }
                NotifyChange();
            }
        });

        var removeButton = new Button(() =>
        {
            RemoveChoice(currentIndex);
            NotifyChange();
        })
        {
            text = "×"
        };
        removeButton.style.width = 22;
        removeButton.style.height = 22;
        removeButton.style.flexShrink = 0;
        removeButton.style.marginLeft = 5;
        removeButton.style.fontSize = 14;
        removeButton.style.backgroundColor = new StyleColor(new Color(0.6f, 0.2f, 0.2f));

        inputRow.Add(textLabel);
        inputRow.Add(choiceField);
        inputRow.Add(removeButton);
        choiceContainer.Add(inputRow);

        var conditionsContent = new VisualElement();

        var conditionsHeader = new VisualElement();
        conditionsHeader.style.flexDirection = FlexDirection.Row;
        conditionsHeader.style.alignItems = Align.Center;
        conditionsHeader.style.marginTop = 8;
        conditionsHeader.style.marginBottom = 5;

        var conditionsLabel = new Label("Conditions");
        conditionsLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        conditionsLabel.style.fontSize = 10;
        conditionsLabel.style.color = new StyleColor(new Color(0.8f, 0.8f, 0.8f));
        conditionsLabel.style.flexGrow = 1;

        var addCondButton = new Button(() =>
        {
            if (currentIndex < ChoicesData.Count)
            {
                ChoicesData[currentIndex].conditions.Add(new ChoiceCondition());
                UpdateConditionsDisplay(conditionsContent, currentIndex);
                NotifyChange();
            }
        })
        {
            text = "+"
        };
        addCondButton.style.width = 20;
        addCondButton.style.height = 18;
        addCondButton.style.fontSize = 12;

        conditionsHeader.Add(conditionsLabel);
        conditionsHeader.Add(addCondButton);
        choiceContainer.Add(conditionsHeader);

        UpdateConditionsDisplay(conditionsContent, currentIndex);

        choiceContainer.Add(conditionsContent);
        choicesContainer.Add(choiceContainer);
    }

    private void UpdateConditionsDisplay(VisualElement container, int choiceIndex)
    {
        container.Clear();

        if (choiceIndex >= ChoicesData.Count) return;

        var choiceData = ChoicesData[choiceIndex];

        if (choiceData.conditions.Count == 0)
        {
            container.style.backgroundColor = StyleKeyword.Null;
            container.style.paddingTop = 0;
            container.style.paddingBottom = 0;
            container.style.paddingLeft = 0;
            container.style.paddingRight = 0;
            container.style.marginTop = 0;
            return;
        }

        container.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f, 0.8f));
        container.style.paddingTop = 8;
        container.style.paddingBottom = 8;
        container.style.paddingLeft = 8;
        container.style.paddingRight = 8;
        container.style.marginTop = 3;
        container.style.borderTopLeftRadius = 3;
        container.style.borderTopRightRadius = 3;
        container.style.borderBottomLeftRadius = 3;
        container.style.borderBottomRightRadius = 3;

        for (int i = 0; i < choiceData.conditions.Count; i++)
        {
            int condIndex = i;
            var condition = choiceData.conditions[i];

            var condContainer = new VisualElement();
            condContainer.style.marginTop = i > 0 ? 5 : 0;
            condContainer.style.paddingTop = 8;
            condContainer.style.paddingBottom = 8;
            condContainer.style.paddingLeft = 8;
            condContainer.style.paddingRight = 8;
            condContainer.style.backgroundColor = new StyleColor(new Color(0.12f, 0.12f, 0.12f));
            condContainer.style.borderTopLeftRadius = 3;
            condContainer.style.borderTopRightRadius = 3;
            condContainer.style.borderBottomLeftRadius = 3;
            condContainer.style.borderBottomRightRadius = 3;

            var condHeader = new VisualElement();
            condHeader.style.flexDirection = FlexDirection.Row;
            condHeader.style.alignItems = Align.Center;
            condHeader.style.marginBottom = 8;

            var condLabel = new Label($"Condition {i + 1}");
            condLabel.style.flexGrow = 1;
            condLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            condLabel.style.fontSize = 10;
            condLabel.style.color = new StyleColor(new Color(0.9f, 0.9f, 0.9f));

            var removeCondButton = new Button(() =>
            {
                if (choiceIndex < ChoicesData.Count)
                {
                    ChoicesData[choiceIndex].conditions.RemoveAt(condIndex);
                    UpdateConditionsDisplay(container, choiceIndex);
                    NotifyChange();
                }
            })
            {
                text = "×"
            };
            removeCondButton.style.width = 18;
            removeCondButton.style.height = 18;
            removeCondButton.style.fontSize = 12;

            condHeader.Add(condLabel);
            condHeader.Add(removeCondButton);
            condContainer.Add(condHeader);

            GameObject currentGameObject = null;
            if (!string.IsNullOrEmpty(condition.targetObjectName))
            {
                currentGameObject = GameObject.Find(condition.targetObjectName);
            }

            var gameObjectField = new ObjectField("Target GameObject:")
            {
                objectType = typeof(GameObject),
                value = currentGameObject,
                allowSceneObjects = true
            };
            gameObjectField.style.marginTop = 3;
            gameObjectField.RegisterValueChangedCallback(evt =>
            {
                if (choiceIndex < ChoicesData.Count && condIndex < ChoicesData[choiceIndex].conditions.Count)
                {
                    var selectedGO = evt.newValue as GameObject;
                    ChoicesData[choiceIndex].conditions[condIndex].targetObjectName = selectedGO != null ? selectedGO.name : "";
                    ChoicesData[choiceIndex].conditions[condIndex].componentTypeName = "";
                    ChoicesData[choiceIndex].conditions[condIndex].variableName = "";
                    UpdateConditionsDisplay(container, choiceIndex);
                    NotifyChange();
                }
            });
            condContainer.Add(gameObjectField);

            if (currentGameObject != null)
            {
                var components = currentGameObject.GetComponents<Component>();
                var componentNames = new List<string> { "None" };
                var componentTypes = new List<System.Type> { null };

                foreach (var comp in components)
                {
                    if (comp != null)
                    {
                        componentNames.Add(comp.GetType().Name);
                        componentTypes.Add(comp.GetType());
                    }
                }

                int selectedComponentIndex = 0;
                if (!string.IsNullOrEmpty(condition.componentTypeName))
                {
                    selectedComponentIndex = componentNames.IndexOf(condition.componentTypeName);
                    if (selectedComponentIndex < 0) selectedComponentIndex = 0;
                }

                var componentDropdown = new PopupField<string>("Component:", componentNames, selectedComponentIndex);
                componentDropdown.style.marginTop = 3;
                componentDropdown.RegisterValueChangedCallback(evt =>
                {
                    if (choiceIndex < ChoicesData.Count && condIndex < ChoicesData[choiceIndex].conditions.Count)
                    {
                        ChoicesData[choiceIndex].conditions[condIndex].componentTypeName = evt.newValue != "None" ? evt.newValue : "";
                        ChoicesData[choiceIndex].conditions[condIndex].variableName = "";
                        UpdateConditionsDisplay(container, choiceIndex);
                        NotifyChange();
                    }
                });
                condContainer.Add(componentDropdown);

                if (selectedComponentIndex > 0 && componentTypes[selectedComponentIndex] != null)
                {
                    var componentType = componentTypes[selectedComponentIndex];

                    var fields = componentType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                        .Where(f => f.FieldType == typeof(int) || f.FieldType == typeof(float) || f.FieldType == typeof(bool))
                        .ToList();

                    var properties = componentType.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                        .Where(p => (p.PropertyType == typeof(int) || p.PropertyType == typeof(float) || p.PropertyType == typeof(bool)) && p.CanRead)
                        .ToList();

                    var variableNames = new List<string> { "None" };
                    variableNames.AddRange(fields.Select(f => f.Name));
                    variableNames.AddRange(properties.Select(p => p.Name));

                    if (variableNames.Count > 1)
                    {
                        int selectedVarIndex = string.IsNullOrEmpty(condition.variableName) ? 0 : variableNames.IndexOf(condition.variableName);
                        if (selectedVarIndex < 0) selectedVarIndex = 0;

                        var varRow = new VisualElement();
                        varRow.style.flexDirection = FlexDirection.Row;
                        varRow.style.alignItems = Align.Center;
                        varRow.style.marginTop = 5;
                        varRow.style.flexWrap = Wrap.NoWrap;

                        var varLabel = new Label("Variable:");
                        varLabel.style.width = 60;
                        varLabel.style.fontSize = 10;
                        varLabel.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f));
                        varLabel.style.marginRight = 5;
                        varLabel.style.flexShrink = 0;

                        var varDropdown = new PopupField<string>(variableNames, selectedVarIndex);
                        varDropdown.style.width = 120;
                        varDropdown.style.marginRight = 5;
                        varDropdown.style.flexShrink = 0;
                        varDropdown.RegisterValueChangedCallback(evt =>
                        {
                            if (choiceIndex < ChoicesData.Count && condIndex < ChoicesData[choiceIndex].conditions.Count)
                            {
                                ChoicesData[choiceIndex].conditions[condIndex].variableName = evt.newValue == "None" ? "" : evt.newValue;
                                UpdateConditionsDisplay(container, choiceIndex);
                                NotifyChange();
                            }
                        });

                        varRow.Add(varLabel);
                        varRow.Add(varDropdown);

                        if (selectedVarIndex > 0)
                        {
                            System.Type selectedVarType = null;
                            string selectedVarName = variableNames[selectedVarIndex];

                            var field = fields.FirstOrDefault(f => f.Name == selectedVarName);
                            if (field != null)
                            {
                                selectedVarType = field.FieldType;
                            }
                            else
                            {
                                var property = properties.FirstOrDefault(p => p.Name == selectedVarName);
                                if (property != null)
                                {
                                    selectedVarType = property.PropertyType;
                                }
                            }

                            List<ComparisonType> comparisonTypes;
                            if (selectedVarType == typeof(bool))
                            {
                                comparisonTypes = new List<ComparisonType>
                            {
                                ComparisonType.Equal,
                                ComparisonType.NotEqual
                            };
                            }
                            else
                            {
                                comparisonTypes = new List<ComparisonType>
                            {
                                ComparisonType.Equal,
                                ComparisonType.NotEqual,
                                ComparisonType.Greater,
                                ComparisonType.Less,
                                ComparisonType.GreaterOrEqual,
                                ComparisonType.LessOrEqual
                            };
                            }

                            var comparisonNames = comparisonTypes.Select(c => GetComparisonDisplayName(c)).ToList();

                            int selectedCompIndex = comparisonTypes.IndexOf(condition.comparison);
                            if (selectedCompIndex < 0) selectedCompIndex = 0;

                            var compDropdown = new PopupField<string>(comparisonNames, selectedCompIndex);
                            compDropdown.style.width = 70;
                            compDropdown.style.marginRight = 5;
                            compDropdown.style.fontSize = 10;
                            compDropdown.style.flexShrink = 0;
                            compDropdown.RegisterValueChangedCallback(evt =>
                            {
                                if (choiceIndex < ChoicesData.Count && condIndex < ChoicesData[choiceIndex].conditions.Count)
                                {
                                    int index = comparisonNames.IndexOf(evt.newValue);
                                    ChoicesData[choiceIndex].conditions[condIndex].comparison = comparisonTypes[index];
                                    NotifyChange();
                                }
                            });

                            varRow.Add(compDropdown);

                            if (selectedVarType == typeof(bool))
                            {
                                var boolValues = new List<string> { "True", "False" };
                                int selectedBoolIndex = 0;

                                if (!string.IsNullOrEmpty(condition.compareValue))
                                {
                                    if (condition.compareValue.Equals("False", System.StringComparison.OrdinalIgnoreCase))
                                    {
                                        selectedBoolIndex = 1;
                                    }
                                }
                                else
                                {
                                    ChoicesData[choiceIndex].conditions[condIndex].compareValue = "True";
                                }

                                var boolDropdown = new PopupField<string>(boolValues, selectedBoolIndex);
                                boolDropdown.style.width = 80;
                                boolDropdown.style.fontSize = 10;
                                boolDropdown.style.flexShrink = 0;
                                boolDropdown.RegisterValueChangedCallback(evt =>
                                {
                                    if (choiceIndex < ChoicesData.Count && condIndex < ChoicesData[choiceIndex].conditions.Count)
                                    {
                                        ChoicesData[choiceIndex].conditions[condIndex].compareValue = evt.newValue;
                                        NotifyChange();
                                    }
                                });

                                varRow.Add(boolDropdown);
                            }
                            else
                            {
                                var valueField = new TextField();
                                valueField.value = condition.compareValue;
                                valueField.style.width = 80;
                                valueField.style.fontSize = 10;
                                valueField.style.flexShrink = 0;
                                valueField.RegisterValueChangedCallback(evt =>
                                {
                                    if (choiceIndex < ChoicesData.Count && condIndex < ChoicesData[choiceIndex].conditions.Count)
                                    {
                                        ChoicesData[choiceIndex].conditions[condIndex].compareValue = evt.newValue;
                                        NotifyChange();
                                    }
                                });

                                varRow.Add(valueField);
                            }
                        }

                        condContainer.Add(varRow);
                    }
                    else
                    {
                        var noVarsLabel = new Label("No public int/float/bool variables found in this component");
                        noVarsLabel.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f));
                        noVarsLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
                        noVarsLabel.style.marginTop = 5;
                        noVarsLabel.style.paddingLeft = 10;
                        condContainer.Add(noVarsLabel);
                    }
                }
            }
            else
            {
                var hintLabel = new Label("Select a GameObject first");
                hintLabel.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f));
                hintLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
                hintLabel.style.marginTop = 5;
                hintLabel.style.paddingLeft = 10;
                condContainer.Add(hintLabel);
            }

            container.Add(condContainer);
        }

        if (choiceData.conditions.Count > 1)
        {
            var logicRow = new VisualElement();
            logicRow.style.flexDirection = FlexDirection.Row;
            logicRow.style.marginTop = 8;
            logicRow.style.alignItems = Align.Center;
            logicRow.style.justifyContent = Justify.Center;

            var andToggle = new Button(() =>
            {
                if (choiceIndex < ChoicesData.Count)
                {
                    ChoicesData[choiceIndex].conditionLogic = ConditionLogic.AND;
                    UpdateConditionsDisplay(container, choiceIndex);
                    NotifyChange();
                }
            })
            {
                text = "AND"
            };
            andToggle.style.width = 60;
            andToggle.style.height = 22;
            andToggle.style.fontSize = 10;
            andToggle.style.unityFontStyleAndWeight = choiceData.conditionLogic == ConditionLogic.AND ?
                FontStyle.Bold : FontStyle.Normal;
            andToggle.style.backgroundColor = choiceData.conditionLogic == ConditionLogic.AND ?
                new StyleColor(new Color(0.3f, 0.5f, 0.3f)) :
                new StyleColor(new Color(0.25f, 0.25f, 0.25f));

            var orToggle = new Button(() =>
            {
                if (choiceIndex < ChoicesData.Count)
                {
                    ChoicesData[choiceIndex].conditionLogic = ConditionLogic.OR;
                    UpdateConditionsDisplay(container, choiceIndex);
                    NotifyChange();
                }
            })
            {
                text = "OR"
            };
            orToggle.style.width = 60;
            orToggle.style.height = 22;
            orToggle.style.fontSize = 10;
            orToggle.style.marginLeft = 5;
            orToggle.style.unityFontStyleAndWeight = choiceData.conditionLogic == ConditionLogic.OR ?
                FontStyle.Bold : FontStyle.Normal;
            orToggle.style.backgroundColor = choiceData.conditionLogic == ConditionLogic.OR ?
                new StyleColor(new Color(0.3f, 0.5f, 0.3f)) :
                new StyleColor(new Color(0.25f, 0.25f, 0.25f));

            logicRow.Add(andToggle);
            logicRow.Add(orToggle);
            container.Add(logicRow);
        }
    }

    private string GetComparisonDisplayName(ComparisonType comparison)
    {
        switch (comparison)
        {
            case ComparisonType.Equal: return "==";
            case ComparisonType.NotEqual: return "!=";
            case ComparisonType.Greater: return ">";
            case ComparisonType.Less: return "<";
            case ComparisonType.GreaterOrEqual: return ">=";
            case ComparisonType.LessOrEqual: return "<=";
            default: return "==";
        }
    }

    private void CreateChoiceOutputPort(int index, string choiceText)
    {
        var outputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
        outputPort.portName = $"{index + 1}: {choiceText}";
        choiceOutputPorts.Add(outputPort);
        outputContainer.Add(outputPort);
    }

    public void SetChoicesData(List<ChoiceData> choicesData)
    {
        ChoicesData.Clear();
        choicesContainer.Clear();

        foreach (var port in choiceOutputPorts)
        {
            outputContainer.Remove(port);
        }
        choiceOutputPorts.Clear();

        for (int i = 0; i < choicesData.Count; i++)
        {
            ChoicesData.Add(choicesData[i]);
            RebuildChoiceUI(i);
            CreateChoiceOutputPort(i, choicesData[i].text);
        }

        RefreshExpandedState();
        RefreshPorts();
    }

    public void SetEventCalls(List<DialogueEventCall> eventCalls)
    {
        EventCalls = eventCalls ?? new List<DialogueEventCall>();
        UpdateEventsDisplay();
    }

    public int GetChoiceIndexForPort(Port port)
    {
        if (port == defaultOutputPort)
            return -1;

        return choiceOutputPorts.IndexOf(port);
    }

    public Port GetOutputPortByIndex(int index)
    {
        return index >= 0 && index < choiceOutputPorts.Count ? choiceOutputPorts[index] : null;
    }

    public Port GetDefaultOutputPort() => defaultOutputPort;
    public Port GetInputPort() => inputPort;
    public string GetId() => nodeId;
    public void SetId(string id) => nodeId = id;

    private StyleColor GetChoiceColor(int index)
    {
        var colors = new Color[]
        {
            new Color(0.4f, 0.7f, 1f),
            new Color(0.5f, 1f, 0.5f),
            new Color(1f, 0.8f, 0.4f),
            new Color(1f, 0.5f, 0.8f),
            new Color(0.8f, 0.5f, 1f),
            new Color(0.5f, 1f, 1f),
        };

        return new StyleColor(colors[index % colors.Length]);
    }
}