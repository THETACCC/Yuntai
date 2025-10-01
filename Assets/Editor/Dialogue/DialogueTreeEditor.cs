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
    public string avatarAssetPath; // 存储Asset路径用于编辑器加载
    public string content;
    public float positionX;
    public float positionY;
    public List<string> choices = new List<string>();
    public List<DialogueEventCall> eventCalls = new List<DialogueEventCall>();
}

[System.Serializable]
public class DialogueConnectionData
{
    public string outputNodeId;
    public string inputNodeId;
    public int choiceIndex;
    public string choiceText;
}

// 运行时格式的数据结构
[System.Serializable]
public class RuntimeDialogueData
{
    public int index;
    public string name;
    public string avatarAddr; // Runtime使用的路径字符串
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
}

// 编辑器窗口类
public class DialogueTreeEditor : EditorWindow
{
    private DialogueGraphView graphView;
    private string currentFilePath = "";
    private new bool hasUnsavedChanges = false;

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
        EditorApplication.delayCall += DelayedInitialize;
    }

    private void DelayedInitialize()
    {
        CreateToolbar();
        CreateGraphView();

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
            CreateToolbar();
            CreateGraphView();
        }

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
                graphView.CreateDialogueNode("Character", null, "New Dialogue");
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

        toolbar.Add(newButton);
        toolbar.Add(createNodeButton);
        toolbar.Add(deleteSelectedButton);
        toolbar.Add(duplicateButton);
        toolbar.Add(saveButton);
        toolbar.Add(saveAsButton);
        toolbar.Add(loadButton);

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
        var dialogueNode = new DialogueNode(characterName, avatarSprite, content, nextNodeIndex++);
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
            // 将Sprite转换为Runtime路径
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
                else if (choiceIndex >= 0 && choiceIndex < node.Choices.Count)
                {
                    var choice = new RuntimeChoice
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

    // 将Sprite转换为运行时路径
    private string ConvertSpriteToRuntimePath(Sprite sprite)
    {
        if (sprite == null) return "";

        string assetPath = AssetDatabase.GetAssetPath(sprite);

        // 检查是否在Resources文件夹内
        int resourcesIndex = assetPath.IndexOf("Resources/");
        if (resourcesIndex >= 0)
        {
            // 提取Resources/之后的路径并移除扩展名
            string resourcePath = assetPath.Substring(resourcesIndex + 10);
            resourcePath = Path.ChangeExtension(resourcePath, null);
            return resourcePath;
        }
        else
        {
            // 如果不在Resources文件夹，警告用户
            Debug.LogWarning($"Avatar sprite '{sprite.name}' at path '{assetPath}' is not in a Resources folder. " +
                           $"It won't be loadable at runtime! Please move it to a Resources folder.");
            return sprite.name; // 返回sprite名称作为fallback
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
                    choices = new List<string>(node.Choices ?? new List<string>()),
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
            // 从Asset路径加载Sprite
            Sprite avatarSprite = null;
            if (!string.IsNullOrEmpty(nodeData.avatarAssetPath))
            {
                avatarSprite = AssetDatabase.LoadAssetAtPath<Sprite>(nodeData.avatarAssetPath);
                if (avatarSprite == null)
                {
                    Debug.LogWarning($"Failed to load sprite at path: {nodeData.avatarAssetPath}");
                }
            }

            var node = CreateDialogueNodeWithIndex(nodeData.name, avatarSprite, nodeData.content,
                new Vector2(nodeData.positionX, nodeData.positionY), nodeData.index);
            node.SetId(nodeData.id);
            node.SetChoices(nodeData.choices);
            node.SetEventCalls(nodeData.eventCalls);
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

    private DialogueNode CreateDialogueNodeWithIndex(string characterName, Sprite avatarSprite, string content, Vector2 position, int index)
    {
        var dialogueNode = new DialogueNode(characterName, avatarSprite, content, index);
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
            var eventCallsStr = JsonUtility.ToJson(new SerializableEventCallList { eventCalls = node.EventCalls });
            var avatarPath = node.AvatarSprite != null ? AssetDatabase.GetAssetPath(node.AvatarSprite) : "";
            nodeData.Add($"{node.CharacterName}|{avatarPath}|{node.DialogueText}|{position.x}|{position.y}|{choicesStr}|{eventCallsStr}");
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
                var choicesStr = nodeData[5];

                // 加载Sprite
                Sprite avatarSprite = null;
                if (!string.IsNullOrEmpty(avatarPath))
                {
                    avatarSprite = AssetDatabase.LoadAssetAtPath<Sprite>(avatarPath);
                }

                List<DialogueEventCall> eventCalls = new List<DialogueEventCall>();
                if (nodeData.Length > 6)
                {
                    try
                    {
                        var eventCallList = JsonUtility.FromJson<SerializableEventCallList>(nodeData[6]);
                        eventCalls = eventCallList.eventCalls;
                    }
                    catch
                    {
                        // 如果解析失败，使用空列表
                    }
                }

                var node = CreateDialogueNode(characterName, avatarSprite, dialogueText, new Vector2(x, y));
                if (!string.IsNullOrEmpty(choicesStr))
                {
                    var choices = choicesStr.Split('~').ToList();
                    node.SetChoices(choices);
                }
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

// 对话节点类
public class DialogueNode : Node
{
    private TextField characterNameField;
    private ObjectField avatarField; // 改用ObjectField
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
    public Sprite AvatarSprite { get; private set; } // 改用Sprite
    public string DialogueText { get; private set; }
    public List<string> Choices { get; private set; } = new List<string>();
    public List<DialogueEventCall> EventCalls { get; private set; } = new List<DialogueEventCall>();
    public int NodeIndex => nodeIndex;

    public event System.Action OnNodeChanged;

    public DialogueNode(string characterName = "Character", Sprite avatarSprite = null, string dialogueText = "New Dialogue", int index = 0)
    {
        this.CharacterName = characterName;
        this.AvatarSprite = avatarSprite;
        this.DialogueText = dialogueText;
        this.nodeIndex = index;
        this.nodeId = System.Guid.NewGuid().ToString();

        UpdateTitle();

        CreateInputPort();
        CreateDefaultOutputPort();
        CreateCharacterNameField();
        CreateAvatarField(); // 改用新方法
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

    // 新的Avatar字段 - 使用ObjectField
    private void CreateAvatarField()
    {
        var avatarContainer = new VisualElement();

        avatarField = new ObjectField("Avatar Sprite:")
        {
            objectType = typeof(Sprite),
            value = AvatarSprite,
            allowSceneObjects = false // 只允许选择Asset
        };

        avatarField.style.minWidth = 300;

        // 创建警告标签（默认隐藏）
        var warningLabel = new Label();
        warningLabel.style.color = new StyleColor(Color.red);
        warningLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        warningLabel.style.paddingLeft = 5;
        warningLabel.style.paddingTop = 2;
        warningLabel.style.display = DisplayStyle.None;

        avatarField.RegisterValueChangedCallback(evt =>
        {
            AvatarSprite = evt.newValue as Sprite;

            // 实时检查是否在Resources文件夹
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

        // 初始检查
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

        addEventButton = new Button(() => {
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

            // 标题栏
            var titleRow = new VisualElement();
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.alignItems = Align.Center;

            var titleLabel = new Label($"Event {i}");
            titleLabel.style.flexGrow = 1;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;

            var removeButton = new Button(() => {
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

            // GameObject选择器
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
                    UpdateEventsDisplay(); // 刷新以更新Component列表
                    NotifyChange();
                }
            });
            eventContainer.Add(gameObjectField);

            // Component类型下拉框（如果GameObject已选择）
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
                        UpdateEventsDisplay(); // 刷新以更新Method列表
                        NotifyChange();
                    }
                });
                eventContainer.Add(componentDropdown);

                // Method下拉框（如果Component已选择）
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
                            // 尝试匹配method名称（不包括参数）
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

                                    // 自动设置参数类型
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

                        // 参数输入框（如果方法需要参数）
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
                // 如果没有选择GameObject，显示提示
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
}