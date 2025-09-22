using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.Experimental.GraphView;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

// 数据结构用于序列化
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
    public string avatarAddr;
    public string content;
    public float positionX;
    public float positionY;
    public List<string> choices = new List<string>();
}

[System.Serializable]
public class DialogueConnectionData
{
    public string outputNodeId;
    public string inputNodeId;
    public int choiceIndex;
    public string choiceText;
}

// 导出格式的数据结构
[System.Serializable]
public class ExportDialogueData
{
    public int index;
    public string name;
    public string avatarAddr;
    public string content;
    public List<ExportChoice> choices = new List<ExportChoice>();
    public string nextNodeId;
}

[System.Serializable]
public class ExportChoice
{
    public string text;
    public string nextNodeId;
}

// 编辑器窗口类
public class DialogueTreeEditor : EditorWindow
{
    private DialogueGraphView graphView;
    private string currentFilePath = "";
    private new bool hasUnsavedChanges = false;

    // 修改为基于项目的存储键
    private string CURRENT_FILE_KEY => $"DialogueTreeEditor_CurrentFile_{Application.dataPath.GetHashCode()}";

    [MenuItem("Tools/Dialogue Tree Editor/Open Editor")]
    public static void OpenWindow()
    {
        DialogueTreeEditor window = GetWindow<DialogueTreeEditor>();
        window.titleContent = new GUIContent("Dialogue Tree Editor");
        window.minSize = new Vector2(800, 600);
        window.Show();
        window.ForceInitialize();
    }

    [MenuItem("Tools/Dialogue Tree Editor/Create New")]
    public static void CreateNewFromMenu()
    {
        DialogueTreeEditor window = GetWindow<DialogueTreeEditor>();
        window.titleContent = new GUIContent("Dialogue Tree Editor");
        window.minSize = new Vector2(800, 600);
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
        window.minSize = new Vector2(800, 600);
        window.Show();
        window.ForceInitialize();
        window.LoadDialogueTree();
    }

    [MenuItem("Tools/Dialogue Tree Editor/Export")]
    public static void ExportDialogueFromMenu()
    {
        DialogueTreeEditor window = GetWindow<DialogueTreeEditor>();
        window.titleContent = new GUIContent("Dialogue Tree Editor");
        if (window != null && window.graphView != null)
        {
            window.ExportDialogueSequence();
        }
        else
        {
            window.ForceInitialize();
            if (window.graphView != null)
            {
                window.ExportDialogueSequence();
            }
            else
            {
                EditorUtility.DisplayDialog("Error", "Please open the Dialogue Tree Editor first and create some dialogue nodes.", "OK");
            }
        }
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
        // 获取基于项目的当前文件路径
        currentFilePath = EditorPrefs.GetString(CURRENT_FILE_KEY, "");
        rootVisualElement.Clear();
        EditorApplication.delayCall += DelayedInitialize;
    }

    private void DelayedInitialize()
    {
        CreateToolbar();
        CreateGraphView();

        // 初始化时重置未保存标志
        hasUnsavedChanges = false;

        // 只有在文件存在且属于当前项目时才自动加载
        if (!string.IsNullOrEmpty(currentFilePath) && File.Exists(currentFilePath))
        {
            // 检查文件是否在当前项目路径下
            string projectPath = Application.dataPath;
            string projectDirectory = Directory.GetParent(projectPath).FullName;

            if (currentFilePath.StartsWith(projectDirectory) || Path.IsPathRooted(currentFilePath))
            {
                LoadFromFile(currentFilePath);
            }
            else
            {
                // 如果文件不在当前项目下，清除路径
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
            CreateToolbar();
            CreateGraphView();
        }

        // 确保窗口标题正确设置
        titleContent = new GUIContent("Dialogue Tree Editor");
    }

    private void OnDisable()
    {
        if (graphView != null)
        {
            rootVisualElement.Remove(graphView);
            graphView = null;
        }
    }

    private void OnDestroy()
    {
        CheckUnsavedChangesBeforeClose();
    }

    private void CheckUnsavedChangesBeforeClose()
    {
        // 只有在真正有内容且有未保存更改时才询问
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
            GUI.Label(new Rect(10, 35, 500, 20), $"Current File: {fileName}{status}");
        }
    }

    private void CreateToolbar()
    {
        var toolbar = new VisualElement();
        toolbar.style.flexDirection = FlexDirection.Row;
        toolbar.style.height = 30;
        toolbar.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f));
        toolbar.style.paddingLeft = 10;
        toolbar.style.paddingRight = 10;
        toolbar.style.paddingTop = 5;
        toolbar.style.paddingBottom = 5;

        var newButton = new Button(() => {
            if (hasUnsavedChanges)
            {
                if (!EditorUtility.DisplayDialog("New Document",
                    "You have unsaved changes. Create new document without saving?",
                    "Yes", "Cancel"))
                {
                    return;
                }
            }
            NewDialogueTree();
        });
        newButton.text = "New";
        newButton.style.marginRight = 10;

        var createNodeButton = new Button(() => {
            if (graphView != null)
            {
                graphView.CreateDialogueNode("Character", "avatars/default.png", "New Dialogue");
                MarkAsChanged();
            }
        });
        createNodeButton.text = "Create Node";
        createNodeButton.style.marginRight = 10;

        var deleteSelectedButton = new Button(() => {
            if (graphView != null)
            {
                graphView.DeleteSelectedElements();
                MarkAsChanged();
            }
        });
        deleteSelectedButton.text = "Delete Selected";
        deleteSelectedButton.style.marginRight = 10;

        var duplicateButton = new Button(() => {
            if (graphView != null)
            {
                graphView.DuplicateSelectedNodes();
                MarkAsChanged();
            }
        });
        duplicateButton.text = "Duplicate";
        duplicateButton.style.marginRight = 10;

        var saveButton = new Button(() => {
            SaveDialogueTree();
        });
        saveButton.text = "Save (Ctrl+S)";
        saveButton.style.marginRight = 10;

        var saveAsButton = new Button(() => {
            SaveAsDialogueTree();
        });
        saveAsButton.text = "Save As...";
        saveAsButton.style.marginRight = 10;

        var loadButton = new Button(() => {
            LoadDialogueTree();
        });
        loadButton.text = "Load";
        loadButton.style.marginRight = 10;

        var exportButton = new Button(() => {
            ExportDialogueSequence();
        });
        exportButton.text = "Export";

        toolbar.Add(newButton);
        toolbar.Add(createNodeButton);
        toolbar.Add(deleteSelectedButton);
        toolbar.Add(duplicateButton);
        toolbar.Add(saveButton);
        toolbar.Add(saveAsButton);
        toolbar.Add(loadButton);
        toolbar.Add(exportButton);

        rootVisualElement.Add(toolbar);
    }

    private void CreateGraphView()
    {
        graphView = new DialogueGraphView();
        graphView.SetEditorWindow(this);
        graphView.StretchToParentSize();
        graphView.graphViewChanged += OnGraphViewChanged;
        rootVisualElement.Add(graphView);
    }

    private GraphViewChange OnGraphViewChanged(GraphViewChange graphViewChange)
    {
        // 只在有实际内容变化时才标记为已更改
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
        hasUnsavedChanges = false; // 明确重置标志
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
            Path.Combine(Application.dataPath, "DialogueTrees"),
            string.IsNullOrEmpty(currentFilePath) ? "DialogueTree" : Path.GetFileNameWithoutExtension(currentFilePath),
            "dtree"
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

        // 确保保存目录存在
        string directory = Path.GetDirectoryName(path);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        DialogueTreeData treeData = graphView.SerializeDialogueTree();
        string json = JsonUtility.ToJson(treeData, true);

        try
        {
            File.WriteAllText(path, json);
            AssetDatabase.Refresh();
            hasUnsavedChanges = false;

            if (!isAutoSave)
            {
                Debug.Log($"Dialogue tree saved to: {path}");
                EditorUtility.DisplayDialog("Save Successful", $"Dialogue tree saved to:\n{path}", "OK");
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
            Path.Combine(Application.dataPath, "DialogueTrees"),
            "dtree"
        );

        if (!string.IsNullOrEmpty(path))
        {
            LoadFromFile(path);
            currentFilePath = path;
            EditorPrefs.SetString(CURRENT_FILE_KEY, currentFilePath);
        }
    }

    private void LoadFromFile(string path)
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

                // Center on node 0 after loading
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

    public void ExportDialogueSequence()
    {
        if (graphView == null) return;

        string path = EditorUtility.SaveFilePanel(
            "Export Dialogue Sequence",
            Path.Combine(Application.dataPath, "StreamingAssets"),
            "DialogueSequence",
            "json"
        );

        if (string.IsNullOrEmpty(path)) return;

        try
        {
            List<ExportDialogueData> exportData = graphView.GetDialogueSequence();

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

                // 处理 nextIndex - 默认的下一个节点
                int nextIndex = -1;
                if (!string.IsNullOrEmpty(item.nextNodeId) && nodeIdToIndex.ContainsKey(item.nextNodeId))
                {
                    nextIndex = nodeIdToIndex[item.nextNodeId];
                }
                formattedJson += $",\n      \"nextIndex\": {nextIndex}";

                // 处理choices数组
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
                        formattedJson += $"          \"targetIndex\": {targetIndex}\n";
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

                formattedJson += "\n    }";
                if (i < exportData.Count - 1) formattedJson += ",";
                formattedJson += "\n";
            }
            formattedJson += "  ],\n";
            formattedJson += "  \"currentIndex\": 0\n";
            formattedJson += "}";

            File.WriteAllText(path, formattedJson);
            AssetDatabase.Refresh();
            Debug.Log($"Dialogue sequence exported to: {path}");
            EditorUtility.DisplayDialog("Export Successful", $"Dialogue sequence exported to:\n{path}", "OK");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to export dialogue sequence: {e.Message}");
            EditorUtility.DisplayDialog("Export Failed", $"Failed to export dialogue sequence:\n{e.Message}", "OK");
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

// GraphView 主视图类
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
        if (node0 != null)
        {
            // Get the position of node 0
            var nodePosition = node0.GetPosition();
            var nodeBounds = new Rect(nodePosition.position, nodePosition.size);

            // Calculate the center point of the node
            var nodeCenter = nodeBounds.center;

            // Get the viewport rect
            var viewportRect = contentRect;

            // Calculate the target position to center the node in the viewport
            var targetScale = Vector3.one; // Reset zoom to 1:1
            var targetPosition = -nodeCenter + viewportRect.center / targetScale;

            // Update the view transform to center on the node
            UpdateViewTransform(targetPosition, targetScale);
        }
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
                CreateDialogueNode("Character", "avatars/default.png", "New Dialogue",
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

        evt.menu.AppendAction("Export",
            action => {
                if (editorWindow != null)
                {
                    editorWindow.ExportDialogueSequence();
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

    public DialogueNode CreateDialogueNode(string characterName, string avatarAddr, string content, Vector2 position = default)
    {
        var dialogueNode = new DialogueNode(characterName, avatarAddr, content, nextNodeIndex++);
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

    public List<ExportDialogueData> GetDialogueSequence()
    {
        var exportDict = new Dictionary<string, ExportDialogueData>();
        var nodes = this.nodes.Cast<DialogueNode>().ToList();
        var edges = this.edges.ToList();

        foreach (var node in nodes)
        {
            var exportData = new ExportDialogueData
            {
                index = node.NodeIndex,
                name = node.CharacterName,
                avatarAddr = node.AvatarAddr,
                content = node.DialogueText,
                choices = new List<ExportChoice>(),
                nextNodeId = null
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
                else if (choiceIndex >= 0 && choiceIndex < node.Choices.Count)
                {
                    var choice = new ExportChoice
                    {
                        text = node.Choices[choiceIndex],
                        nextNodeId = targetNode.GetId()
                    };
                    exportData.choices.Add(choice);
                }
            }

            exportData.choices = exportData.choices.OrderBy(c => node.Choices.IndexOf(c.text)).ToList();
        }

        return exportDict.Values.OrderBy(d => d.index).ToList();
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
                    avatarAddr = node.AvatarAddr ?? "",
                    content = node.DialogueText ?? "",
                    positionX = node.GetPosition().x,
                    positionY = node.GetPosition().y,
                    choices = new List<string>(node.Choices ?? new List<string>())
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

                    if (choiceIndex >= 0 && choiceIndex < outputNode.Choices.Count)
                    {
                        choiceText = outputNode.Choices[choiceIndex];
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

        foreach (var nodeData in sortedNodes)
        {
            var node = CreateDialogueNodeWithIndex(nodeData.name, nodeData.avatarAddr, nodeData.content,
                new Vector2(nodeData.positionX, nodeData.positionY), nodeData.index);
            node.SetId(nodeData.id);
            node.SetChoices(nodeData.choices);
            nodeDict[nodeData.id] = node;
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
    }

    private DialogueNode CreateDialogueNodeWithIndex(string characterName, string avatarAddr, string content, Vector2 position, int index)
    {
        var dialogueNode = new DialogueNode(characterName, avatarAddr, content, index);
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
            var choicesStr = string.Join("~", node.Choices);
            nodeData.Add($"{node.CharacterName}|{node.AvatarAddr}|{node.DialogueText}|{position.x}|{position.y}|{choicesStr}");
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
                var avatarAddr = nodeData[1];
                var dialogueText = nodeData[2];
                var x = float.Parse(nodeData[3]) + offset.x;
                var y = float.Parse(nodeData[4]) + offset.y;
                var choicesStr = nodeData[5];

                var node = CreateDialogueNode(characterName, avatarAddr, dialogueText, new Vector2(x, y));
                if (!string.IsNullOrEmpty(choicesStr))
                {
                    var choices = choicesStr.Split('~').ToList();
                    node.SetChoices(choices);
                }
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

// 对话节点类
public class DialogueNode : Node
{
    private TextField characterNameField;
    private TextField avatarAddrField;
    private TextField dialogueTextField;
    private VisualElement choicesContainer;
    private Button addChoiceButton;
    private Port inputPort;
    private Port defaultOutputPort;
    private List<Port> choiceOutputPorts = new List<Port>();
    private string nodeId;
    private int nodeIndex;

    public string CharacterName { get; private set; }
    public string AvatarAddr { get; private set; }
    public string DialogueText { get; private set; }
    public List<string> Choices { get; private set; } = new List<string>();
    public int NodeIndex => nodeIndex;

    public event System.Action OnNodeChanged;

    public DialogueNode(string characterName = "Character", string avatarAddr = "avatars/default.png", string dialogueText = "New Dialogue", int index = 0)
    {
        this.CharacterName = characterName;
        this.AvatarAddr = avatarAddr;
        this.DialogueText = dialogueText;
        this.nodeIndex = index;
        this.nodeId = System.Guid.NewGuid().ToString();

        UpdateTitle();

        CreateInputPort();
        CreateDefaultOutputPort();
        CreateCharacterNameField();
        CreateAvatarAddrField();
        CreateDialogueTextField();
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

        characterNameField.style.minWidth = 200;

        characterNameField.RegisterValueChangedCallback(evt =>
        {
            CharacterName = evt.newValue;
            NotifyChange();
        });

        mainContainer.Add(characterNameField);
    }

    private void CreateAvatarAddrField()
    {
        avatarAddrField = new TextField("Avatar Path:")
        {
            value = AvatarAddr
        };

        avatarAddrField.style.minWidth = 200;

        avatarAddrField.RegisterValueChangedCallback(evt =>
        {
            AvatarAddr = evt.newValue;
            NotifyChange();
        });

        mainContainer.Add(avatarAddrField);
    }

    private void CreateDialogueTextField()
    {
        dialogueTextField = new TextField("Dialogue:")
        {
            value = DialogueText,
            multiline = true
        };

        dialogueTextField.style.minWidth = 200;
        dialogueTextField.style.minHeight = 60;

        dialogueTextField.RegisterValueChangedCallback(evt =>
        {
            DialogueText = evt.newValue;
            NotifyChange();
        });

        mainContainer.Add(dialogueTextField);
    }

    private void CreateChoicesSection()
    {
        var choicesLabel = new Label("Player Choices:");
        choicesLabel.style.marginTop = 10;
        mainContainer.Add(choicesLabel);

        choicesContainer = new VisualElement();
        mainContainer.Add(choicesContainer);

        addChoiceButton = new Button(() => {
            AddChoice("New Choice");
            NotifyChange();
        })
        {
            text = "Add Choice"
        };
        addChoiceButton.style.marginTop = 5;
        mainContainer.Add(addChoiceButton);
    }

    private void AddChoice(string choiceText)
    {
        int index = Choices.Count;
        Choices.Add(choiceText);

        var choiceContainer = new VisualElement();
        choiceContainer.style.flexDirection = FlexDirection.Row;
        choiceContainer.style.marginTop = 2;

        var choiceField = new TextField()
        {
            value = choiceText
        };
        choiceField.style.flexGrow = 1;
        choiceField.RegisterValueChangedCallback(evt =>
        {
            if (index < Choices.Count)
            {
                Choices[index] = evt.newValue;
                if (index < choiceOutputPorts.Count)
                {
                    choiceOutputPorts[index].portName = $"{index + 1}: {evt.newValue}";
                }
                NotifyChange();
            }
        });

        var removeButton = new Button(() => {
            RemoveChoice(index);
            NotifyChange();
        })
        {
            text = "X"
        };
        removeButton.style.width = 20;

        choiceContainer.Add(choiceField);
        choiceContainer.Add(removeButton);
        choicesContainer.Add(choiceContainer);

        CreateChoiceOutputPort(index, choiceText);
        RefreshExpandedState();
        RefreshPorts();
    }

    private void RemoveChoice(int index)
    {
        if (index >= 0 && index < Choices.Count)
        {
            if (index < choiceOutputPorts.Count)
            {
                var port = choiceOutputPorts[index];
                outputContainer.Remove(port);
                choiceOutputPorts.RemoveAt(index);
            }

            Choices.RemoveAt(index);
            choicesContainer.Clear();

            foreach (var port in choiceOutputPorts)
            {
                outputContainer.Remove(port);
            }
            choiceOutputPorts.Clear();

            for (int i = 0; i < Choices.Count; i++)
            {
                RebuildChoiceUI(i);
                CreateChoiceOutputPort(i, Choices[i]);
            }

            RefreshExpandedState();
            RefreshPorts();
        }
    }

    private void RebuildChoiceUI(int index)
    {
        var choiceContainer = new VisualElement();
        choiceContainer.style.flexDirection = FlexDirection.Row;
        choiceContainer.style.marginTop = 2;

        var choiceField = new TextField()
        {
            value = Choices[index]
        };
        choiceField.style.flexGrow = 1;

        int currentIndex = index;
        choiceField.RegisterValueChangedCallback(evt =>
        {
            if (currentIndex < Choices.Count)
            {
                Choices[currentIndex] = evt.newValue;
                if (currentIndex < choiceOutputPorts.Count)
                {
                    choiceOutputPorts[currentIndex].portName = $"{currentIndex + 1}: {evt.newValue}";
                }
                NotifyChange();
            }
        });

        var removeButton = new Button(() => {
            RemoveChoice(currentIndex);
            NotifyChange();
        })
        {
            text = "X"
        };
        removeButton.style.width = 20;

        choiceContainer.Add(choiceField);
        choiceContainer.Add(removeButton);
        choicesContainer.Add(choiceContainer);
    }

    private void CreateChoiceOutputPort(int index, string choiceText)
    {
        var outputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
        outputPort.portName = $"{index + 1}: {choiceText}";
        choiceOutputPorts.Add(outputPort);
        outputContainer.Add(outputPort);
    }

    public void SetChoices(List<string> choices)
    {
        Choices.Clear();
        choicesContainer.Clear();

        foreach (var port in choiceOutputPorts)
        {
            outputContainer.Remove(port);
        }
        choiceOutputPorts.Clear();

        for (int i = 0; i < choices.Count; i++)
        {
            Choices.Add(choices[i]);
            RebuildChoiceUI(i);
            CreateChoiceOutputPort(i, choices[i]);
        }

        RefreshExpandedState();
        RefreshPorts();
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
}