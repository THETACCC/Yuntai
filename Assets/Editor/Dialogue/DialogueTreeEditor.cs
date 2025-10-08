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

// ==================== 新增：变量相关数据结构 ====================
[System.Serializable]
public enum VariableType
{
    Bool,
    Int,
    Float,
    String
}

[System.Serializable]
public enum ComparisonType
{
    Equal,              // ==
    NotEqual,           // !=
    Greater,            // >
    Less,               // <
    GreaterOrEqual,     // >=
    LessOrEqual,        // <=
    Contains,           // String contains
    StartsWith,         // String starts with
    EndsWith            // String ends with
}

[System.Serializable]
public class DialogueVariable
{
    public string name;
    public VariableType type;
    public string defaultValue; // 统一用string存储，使用时转换
}

[System.Serializable]
public class ChoiceCondition
{
    public string variableName;
    public ComparisonType comparison;
    public string compareValue;
}

[System.Serializable]
public enum ConditionLogic
{
    AND,
    OR
}

// ==================== 修改后的数据结构 ====================
[System.Serializable]
public class DialogueTreeData
{
    public List<DialogueVariable> variables = new List<DialogueVariable>(); // 新增
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
    public List<ChoiceData> choices = new List<ChoiceData>(); // 修改为ChoiceData
    public List<DialogueEventCall> eventCalls = new List<DialogueEventCall>();
}

[System.Serializable]
public class ChoiceData
{
    public string text;
    public List<ChoiceCondition> conditions = new List<ChoiceCondition>(); // 新增
    public ConditionLogic conditionLogic = ConditionLogic.AND; // 新增
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
    public List<ChoiceCondition> conditions = new List<ChoiceCondition>(); // 新增
    public ConditionLogic conditionLogic = ConditionLogic.AND; // 新增
}

// ==================== 修改后的编辑器窗口类 ====================
public class DialogueTreeEditor : EditorWindow
{
    private DialogueGraphView graphView;
    private VariablesPanel variablesPanel;
    private string currentFilePath = "";
    private new bool hasUnsavedChanges = false;
    private List<DialogueVariable> variables = new List<DialogueVariable>();

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
        EditorApplication.delayCall += DelayedInitialize;
    }

    private void DelayedInitialize()
    {
        CreateToolbar();
        CreateMainLayout();

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
            CreateMainLayout();
        }

        titleContent = new GUIContent("Dialogue Tree Editor");
    }

    private void OnDisable()
    {
        if (graphView != null)
        {
            graphView = null;
        }
        if (variablesPanel != null)
        {
            variablesPanel = null;
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
        // 工具栏已移除，所有功能通过右键菜单访问
    }

    private void CreateMainLayout()
    {
        var mainContainer = new VisualElement();
        mainContainer.style.flexDirection = FlexDirection.Row;
        mainContainer.style.flexGrow = 1;

        // 创建变量面板
        variablesPanel = new VariablesPanel(this);
        variablesPanel.style.width = 250;
        variablesPanel.style.borderRightWidth = 2;
        variablesPanel.style.borderRightColor = new StyleColor(new Color(0.1f, 0.1f, 0.1f));
        variablesPanel.SetVariables(variables);

        // 创建GraphView
        graphView = new DialogueGraphView();
        graphView.SetEditorWindow(this);
        graphView.style.flexGrow = 1;
        graphView.graphViewChanged += OnGraphViewChanged;

        mainContainer.Add(variablesPanel);
        mainContainer.Add(graphView);

        rootVisualElement.Add(mainContainer);
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

    public List<DialogueVariable> GetVariables() => variables;

    public void NotifyVariablesChanged()
    {
        if (graphView != null)
        {
            graphView.RefreshAllNodesConditions();
        }
    }

    public void NewDialogueTree()
    {
        currentFilePath = "";
        hasUnsavedChanges = false;
        EditorPrefs.DeleteKey(CURRENT_FILE_KEY);
        variables.Clear();

        if (graphView != null)
        {
            graphView.ClearGraph();
        }
        if (variablesPanel != null)
        {
            variablesPanel.SetVariables(variables);
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

        string formattedJson = "{\n  \"variables\": [\n";

        // 导出变量定义
        for (int i = 0; i < variables.Count; i++)
        {
            var variable = variables[i];
            formattedJson += "    {\n";
            formattedJson += $"      \"name\": \"{EscapeJsonString(variable.name)}\",\n";
            formattedJson += $"      \"type\": \"{variable.type}\",\n";
            formattedJson += $"      \"defaultValue\": \"{EscapeJsonString(variable.defaultValue)}\"\n";
            formattedJson += "    }";
            if (i < variables.Count - 1) formattedJson += ",";
            formattedJson += "\n";
        }
        formattedJson += "  ],\n";

        formattedJson += "  \"conversations\": [\n";
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

                    // 导出条件
                    if (choice.conditions.Count > 0)
                    {
                        formattedJson += ",\n          \"conditions\": [\n";
                        for (int k = 0; k < choice.conditions.Count; k++)
                        {
                            var condition = choice.conditions[k];
                            formattedJson += "            {\n";
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
        treeData.variables = new List<DialogueVariable>(variables);
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
                variables = treeData.variables ?? new List<DialogueVariable>();
                if (variablesPanel != null)
                {
                    variablesPanel.SetVariables(variables);
                }

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

// ==================== 新增：变量面板类 ====================
public class VariablesPanel : VisualElement
{
    private DialogueTreeEditor editorWindow;
    private List<DialogueVariable> variables;
    private ScrollView scrollView;
    private VisualElement listContainer;

    public VariablesPanel(DialogueTreeEditor editor)
    {
        this.editorWindow = editor;

        style.backgroundColor = new StyleColor(new Color(0.22f, 0.22f, 0.22f));
        style.paddingTop = 5;
        style.paddingBottom = 5;
        style.paddingLeft = 5;
        style.paddingRight = 5;

        CreateHeader();
        CreateScrollView();
    }

    private void CreateHeader()
    {
        var header = new Label("Variables");
        header.style.fontSize = 14;
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.style.marginBottom = 5;
        header.style.unityTextAlign = TextAnchor.MiddleCenter;
        Add(header);

        var addButton = new Button(ShowAddVariableMenu);
        addButton.text = "+ Add Variable";
        addButton.style.marginBottom = 8;
        Add(addButton);
    }

    private void ShowAddVariableMenu()
    {
        var menu = new GenericMenu();
        menu.AddItem(new GUIContent("Bool"), false, () => AddVariable(VariableType.Bool));
        menu.AddItem(new GUIContent("Int"), false, () => AddVariable(VariableType.Int));
        menu.AddItem(new GUIContent("Float"), false, () => AddVariable(VariableType.Float));
        menu.AddItem(new GUIContent("String"), false, () => AddVariable(VariableType.String));
        menu.ShowAsContext();
    }

    private void AddVariable(VariableType type)
    {
        string baseName = type.ToString().ToLower() + "Var";
        string name = baseName;
        int counter = 1;

        while (variables.Any(v => v.name == name))
        {
            name = baseName + counter;
            counter++;
        }

        var variable = new DialogueVariable
        {
            name = name,
            type = type,
            defaultValue = GetDefaultValueForType(type)
        };

        variables.Add(variable);
        editorWindow.MarkAsChanged();
        editorWindow.NotifyVariablesChanged(); // 通知变量改变
        RefreshDisplay();
    }

    private string GetDefaultValueForType(VariableType type)
    {
        switch (type)
        {
            case VariableType.Bool: return "false";
            case VariableType.Int: return "0";
            case VariableType.Float: return "0.0";
            case VariableType.String: return "";
            default: return "";
        }
    }

    private void CreateScrollView()
    {
        scrollView = new ScrollView();
        scrollView.style.flexGrow = 1;

        listContainer = new VisualElement();
        scrollView.Add(listContainer);

        Add(scrollView);
    }

    public void SetVariables(List<DialogueVariable> vars)
    {
        this.variables = vars;
        RefreshDisplay();
    }

    private void RefreshDisplay()
    {
        listContainer.Clear();

        foreach (var variable in variables)
        {
            AddVariableUI(variable);
        }
    }

    private void AddVariableUI(DialogueVariable variable)
    {
        var varRow = new VisualElement();
        varRow.style.flexDirection = FlexDirection.Row;
        varRow.style.marginBottom = 3;
        varRow.style.paddingTop = 3;
        varRow.style.paddingBottom = 3;
        varRow.style.paddingLeft = 3;
        varRow.style.paddingRight = 3;
        varRow.style.backgroundColor = new StyleColor(new Color(0.18f, 0.18f, 0.18f));

        // 类型图标
        var typeLabel = new Label(GetTypeIcon(variable.type));
        typeLabel.style.width = 20;
        typeLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        typeLabel.style.fontSize = 12;
        typeLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        typeLabel.style.marginRight = 5;

        // 根据类型设置颜色
        typeLabel.style.color = GetTypeColor(variable.type);

        // 变量名
        var nameField = new TextField();
        nameField.value = variable.name;
        nameField.style.flexGrow = 1;
        nameField.style.minWidth = 60;
        nameField.style.marginRight = 5;
        nameField.RegisterValueChangedCallback(evt =>
        {
            if (!string.IsNullOrEmpty(evt.newValue) && !variables.Any(v => v != variable && v.name == evt.newValue))
            {
                variable.name = evt.newValue;
                editorWindow.MarkAsChanged();
                editorWindow.NotifyVariablesChanged(); // 通知变量名称改变
            }
            else
            {
                nameField.SetValueWithoutNotify(variable.name);
            }
        });

        // 默认值输入
        VisualElement valueField = null;
        switch (variable.type)
        {
            case VariableType.Bool:
                var boolField = new Toggle();
                boolField.value = variable.defaultValue == "true";
                boolField.style.width = 40;
                boolField.RegisterValueChangedCallback(evt =>
                {
                    variable.defaultValue = evt.newValue.ToString().ToLower();
                    editorWindow.MarkAsChanged();
                });
                valueField = boolField;
                break;

            case VariableType.Int:
                var intField = new IntegerField();
                int.TryParse(variable.defaultValue, out int intValue);
                intField.value = intValue;
                intField.style.width = 60;
                intField.RegisterValueChangedCallback(evt =>
                {
                    variable.defaultValue = evt.newValue.ToString();
                    editorWindow.MarkAsChanged();
                });
                valueField = intField;
                break;

            case VariableType.Float:
                var floatField = new FloatField();
                float.TryParse(variable.defaultValue, out float floatValue);
                floatField.value = floatValue;
                floatField.style.width = 60;
                floatField.RegisterValueChangedCallback(evt =>
                {
                    variable.defaultValue = evt.newValue.ToString();
                    editorWindow.MarkAsChanged();
                });
                valueField = floatField;
                break;

            case VariableType.String:
                var stringField = new TextField();
                stringField.value = variable.defaultValue;
                stringField.style.width = 80;
                stringField.RegisterValueChangedCallback(evt =>
                {
                    variable.defaultValue = evt.newValue;
                    editorWindow.MarkAsChanged();
                });
                valueField = stringField;
                break;
        }

        // 删除按钮
        var deleteButton = new Button(() =>
        {
            variables.Remove(variable);
            editorWindow.MarkAsChanged();
            editorWindow.NotifyVariablesChanged(); // 通知变量改变
            RefreshDisplay();
        });
        deleteButton.text = "×";
        deleteButton.style.width = 20;
        deleteButton.style.height = 20;
        deleteButton.style.marginLeft = 5;
        deleteButton.style.fontSize = 14;

        varRow.Add(typeLabel);
        varRow.Add(nameField);
        if (valueField != null)
            varRow.Add(valueField);
        varRow.Add(deleteButton);

        listContainer.Add(varRow);
    }

    private string GetTypeIcon(VariableType type)
    {
        switch (type)
        {
            case VariableType.Bool: return "B";
            case VariableType.Int: return "I";
            case VariableType.Float: return "F";
            case VariableType.String: return "S";
            default: return "?";
        }
    }

    private Color GetTypeColor(VariableType type)
    {
        switch (type)
        {
            case VariableType.Bool: return new Color(0.8f, 0.4f, 0.4f); // 红色
            case VariableType.Int: return new Color(0.4f, 0.7f, 1f);   // 蓝色
            case VariableType.Float: return new Color(0.5f, 1f, 0.5f); // 绿色
            case VariableType.String: return new Color(1f, 0.8f, 0.4f); // 橙色
            default: return Color.white;
        }
    }
}

// ==================== GraphView 主视图类 ====================
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

    public void RefreshAllNodesConditions()
    {
        var allNodes = nodes.Cast<DialogueNode>().ToList();
        foreach (var node in allNodes)
        {
            node.RefreshConditionsUI();
        }
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

            var node = CreateDialogueNodeWithIndex(nodeData.name, avatarSprite, nodeData.content,
                new Vector2(nodeData.positionX, nodeData.positionY), nodeData.index);
            node.SetId(nodeData.id);
            node.SetChoicesData(nodeData.choices);
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

// ==================== 对话节点类 ====================
public class DialogueNode : Node
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

        addChoiceButton = new Button(() => {
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
            if (index < choiceOutputPorts.Count)
            {
                var port = choiceOutputPorts[index];
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

            RefreshExpandedState();
            RefreshPorts();
        }
    }

    private void RebuildChoiceUI(int index)
    {
        var choiceContainer = new VisualElement();
        choiceContainer.style.marginTop = 5;
        choiceContainer.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f, 0.5f));
        choiceContainer.style.paddingTop = 5;
        choiceContainer.style.paddingBottom = 5;
        choiceContainer.style.paddingLeft = 5;
        choiceContainer.style.paddingRight = 5;

        var headerRow = new VisualElement();
        headerRow.style.flexDirection = FlexDirection.Row;
        headerRow.style.alignItems = Align.Center;

        var choiceField = new TextField();
        choiceField.value = ChoicesData[index].text;
        choiceField.style.flexGrow = 1;

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

        var removeButton = new Button(() => {
            RemoveChoice(currentIndex);
            NotifyChange();
        })
        {
            text = "×"
        };
        removeButton.style.width = 20;
        removeButton.style.height = 18;

        headerRow.Add(choiceField);
        headerRow.Add(removeButton);
        choiceContainer.Add(headerRow);

        // 条件部分 - 使用Label代替Foldout避免显示方框
        var conditionsHeader = new VisualElement();
        conditionsHeader.style.flexDirection = FlexDirection.Row;
        conditionsHeader.style.marginTop = 8;
        conditionsHeader.style.marginBottom = 3;
        conditionsHeader.style.paddingBottom = 3;
        conditionsHeader.style.borderBottomWidth = 1;
        conditionsHeader.style.borderBottomColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f));

        var conditionsLabel = new Label("Conditions");
        conditionsLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        conditionsLabel.style.fontSize = 10;
        conditionsLabel.style.color = new StyleColor(new Color(0.8f, 0.8f, 0.8f));

        conditionsHeader.Add(conditionsLabel);
        choiceContainer.Add(conditionsHeader);

        var conditionsContent = new VisualElement();
        conditionsContent.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f, 0.8f));
        conditionsContent.style.paddingTop = 8;
        conditionsContent.style.paddingBottom = 8;
        conditionsContent.style.paddingLeft = 8;
        conditionsContent.style.paddingRight = 8;
        conditionsContent.style.marginTop = 0;

        UpdateConditionsDisplay(conditionsContent, currentIndex);

        choiceContainer.Add(conditionsContent);
        choicesContainer.Add(choiceContainer);
    }

    public void RefreshConditionsUI()
    {
        // 重新构建所有choice的UI以刷新变量列表
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

        RefreshExpandedState();
        RefreshPorts();
    }

    private void UpdateConditionsDisplay(VisualElement container, int choiceIndex)
    {
        container.Clear();

        if (choiceIndex >= ChoicesData.Count) return;

        var choiceData = ChoicesData[choiceIndex];

        if (choiceData.conditions.Count == 0)
        {
            var emptyLabel = new Label("No conditions");
            emptyLabel.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
            emptyLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            emptyLabel.style.fontSize = 10;
            emptyLabel.style.paddingTop = 5;
            emptyLabel.style.paddingBottom = 5;
            emptyLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            container.Add(emptyLabel);
        }
        else
        {
            for (int i = 0; i < choiceData.conditions.Count; i++)
            {
                int condIndex = i;
                var condition = choiceData.conditions[i];

                var condContainer = new VisualElement();
                condContainer.style.marginTop = 5;
                condContainer.style.paddingTop = 8;
                condContainer.style.paddingBottom = 8;
                condContainer.style.paddingLeft = 8;
                condContainer.style.paddingRight = 8;
                condContainer.style.backgroundColor = new StyleColor(new Color(0.12f, 0.12f, 0.12f));
                condContainer.style.borderTopLeftRadius = 3;
                condContainer.style.borderTopRightRadius = 3;
                condContainer.style.borderBottomLeftRadius = 3;
                condContainer.style.borderBottomRightRadius = 3;

                // 标题行
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

                // 变量选择
                var variables = editorWindow?.GetVariables() ?? new List<DialogueVariable>();
                var variableNames = new List<string> { "None" };
                variableNames.AddRange(variables.Select(v => v.name));

                int selectedVarIndex = string.IsNullOrEmpty(condition.variableName) ? 0 : variableNames.IndexOf(condition.variableName);
                if (selectedVarIndex < 0) selectedVarIndex = 0;

                // Variable 行
                var varRow = new VisualElement();
                varRow.style.flexDirection = FlexDirection.Row;
                varRow.style.alignItems = Align.Center;
                varRow.style.marginBottom = 5;

                var varLabel = new Label("Var:");
                varLabel.style.width = 35;
                varLabel.style.fontSize = 10;
                varLabel.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f));

                var varDropdown = new PopupField<string>(variableNames, selectedVarIndex);
                varDropdown.style.flexGrow = 1;
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
                condContainer.Add(varRow);

                if (selectedVarIndex > 0)
                {
                    var selectedVariable = variables[selectedVarIndex - 1];

                    // 运算符和值在同一行
                    var opValueRow = new VisualElement();
                    opValueRow.style.flexDirection = FlexDirection.Row;
                    opValueRow.style.alignItems = Align.Center;

                    // 运算符
                    var comparisonTypes = GetComparisonTypesForVariable(selectedVariable.type);
                    var comparisonNames = comparisonTypes.Select(c => GetComparisonDisplayName(c)).ToList();

                    int selectedCompIndex = comparisonTypes.IndexOf(condition.comparison);
                    if (selectedCompIndex < 0) selectedCompIndex = 0;

                    var compDropdown = new PopupField<string>(comparisonNames, selectedCompIndex);
                    compDropdown.style.width = 70;
                    compDropdown.style.marginRight = 5;
                    compDropdown.style.fontSize = 10;
                    compDropdown.RegisterValueChangedCallback(evt =>
                    {
                        if (choiceIndex < ChoicesData.Count && condIndex < ChoicesData[choiceIndex].conditions.Count)
                        {
                            int index = comparisonNames.IndexOf(evt.newValue);
                            ChoicesData[choiceIndex].conditions[condIndex].comparison = comparisonTypes[index];
                            NotifyChange();
                        }
                    });

                    opValueRow.Add(compDropdown);

                    // 值输入
                    VisualElement valueField = null;
                    switch (selectedVariable.type)
                    {
                        case VariableType.Bool:
                            bool boolValue = condition.compareValue == "true";
                            var boolToggle = new Toggle();
                            boolToggle.value = boolValue;
                            boolToggle.style.flexGrow = 1;
                            boolToggle.RegisterValueChangedCallback(evt =>
                            {
                                if (choiceIndex < ChoicesData.Count && condIndex < ChoicesData[choiceIndex].conditions.Count)
                                {
                                    ChoicesData[choiceIndex].conditions[condIndex].compareValue = evt.newValue.ToString().ToLower();
                                    NotifyChange();
                                }
                            });
                            valueField = boolToggle;
                            break;

                        case VariableType.Int:
                            int.TryParse(condition.compareValue, out int intValue);
                            var intField = new IntegerField();
                            intField.value = intValue;
                            intField.style.flexGrow = 1;
                            intField.style.fontSize = 10;
                            intField.RegisterValueChangedCallback(evt =>
                            {
                                if (choiceIndex < ChoicesData.Count && condIndex < ChoicesData[choiceIndex].conditions.Count)
                                {
                                    ChoicesData[choiceIndex].conditions[condIndex].compareValue = evt.newValue.ToString();
                                    NotifyChange();
                                }
                            });
                            valueField = intField;
                            break;

                        case VariableType.Float:
                            float.TryParse(condition.compareValue, out float floatValue);
                            var floatField = new FloatField();
                            floatField.value = floatValue;
                            floatField.style.flexGrow = 1;
                            floatField.style.fontSize = 10;
                            floatField.RegisterValueChangedCallback(evt =>
                            {
                                if (choiceIndex < ChoicesData.Count && condIndex < ChoicesData[choiceIndex].conditions.Count)
                                {
                                    ChoicesData[choiceIndex].conditions[condIndex].compareValue = evt.newValue.ToString();
                                    NotifyChange();
                                }
                            });
                            valueField = floatField;
                            break;

                        case VariableType.String:
                            var stringField = new TextField();
                            stringField.value = condition.compareValue;
                            stringField.style.flexGrow = 1;
                            stringField.style.fontSize = 10;
                            stringField.RegisterValueChangedCallback(evt =>
                            {
                                if (choiceIndex < ChoicesData.Count && condIndex < ChoicesData[choiceIndex].conditions.Count)
                                {
                                    ChoicesData[choiceIndex].conditions[condIndex].compareValue = evt.newValue;
                                    NotifyChange();
                                }
                            });
                            valueField = stringField;
                            break;
                    }

                    if (valueField != null)
                    {
                        opValueRow.Add(valueField);
                    }

                    condContainer.Add(opValueRow);
                }

                container.Add(condContainer);
            }

            // Logic选择 - 更紧凑的样式
            if (choiceData.conditions.Count > 1)
            {
                var logicRow = new VisualElement();
                logicRow.style.flexDirection = FlexDirection.Row;
                logicRow.style.marginTop = 8;
                logicRow.style.marginBottom = 3;
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

        // Add Condition按钮 - 更明显的样式
        var addCondButton = new Button(() =>
        {
            if (choiceIndex < ChoicesData.Count)
            {
                ChoicesData[choiceIndex].conditions.Add(new ChoiceCondition());
                UpdateConditionsDisplay(container, choiceIndex);
                NotifyChange();
            }
        })
        {
            text = "+ Add Condition"
        };
        addCondButton.style.marginTop = 5;
        addCondButton.style.height = 20;
        addCondButton.style.fontSize = 10;
        container.Add(addCondButton);
    }

    private List<ComparisonType> GetComparisonTypesForVariable(VariableType varType)
    {
        switch (varType)
        {
            case VariableType.Bool:
                return new List<ComparisonType> { ComparisonType.Equal, ComparisonType.NotEqual };

            case VariableType.Int:
            case VariableType.Float:
                return new List<ComparisonType>
                {
                    ComparisonType.Equal,
                    ComparisonType.NotEqual,
                    ComparisonType.Greater,
                    ComparisonType.Less,
                    ComparisonType.GreaterOrEqual,
                    ComparisonType.LessOrEqual
                };

            case VariableType.String:
                return new List<ComparisonType>
                {
                    ComparisonType.Equal,
                    ComparisonType.NotEqual,
                    ComparisonType.Contains,
                    ComparisonType.StartsWith,
                    ComparisonType.EndsWith
                };

            default:
                return new List<ComparisonType> { ComparisonType.Equal };
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
            case ComparisonType.Contains: return "Contains";
            case ComparisonType.StartsWith: return "Starts With";
            case ComparisonType.EndsWith: return "Ends With";
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
}