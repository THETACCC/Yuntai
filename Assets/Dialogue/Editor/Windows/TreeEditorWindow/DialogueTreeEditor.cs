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
    private DialogueGraphViewEditor graphView;
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
    [MenuItem("Tools/Dialogue System/Tree Editor Window")]
    public static void OpenWindow()
    {
        DialogueTreeEditor window = GetWindow<DialogueTreeEditor>();
        window.titleContent = new GUIContent("Dialogue Tree Editor");
        window.minSize = new Vector2(1000, 600);
        window.Show();
        window.ForceInitialize();
    }

    public static void CreateNewFromMenu()
    {
        DialogueTreeEditor window = GetWindow<DialogueTreeEditor>();
        window.titleContent = new GUIContent("Dialogue Tree Editor");
        window.minSize = new Vector2(1000, 600);
        window.Show();
        window.ForceInitialize();
        if (window.hasUnsavedChanges && !EditorUtility.DisplayDialog("New Document", "You have unsaved changes. Create new document without saving?", "Yes", "Cancel")) return;
        window.NewDialogueTree();
    }

    public static void LoadFromMenu()
    {
        DialogueTreeEditor window = GetWindow<DialogueTreeEditor>();
        window.titleContent = new GUIContent("Dialogue Tree Editor");
        window.minSize = new Vector2(1000, 600);
        window.Show();
        window.ForceInitialize();
        window.LoadDialogueTree();
    }

    public static void SaveCurrentFromMenu() { DialogueTreeEditor window = GetWindow<DialogueTreeEditor>(); if (window != null && window.graphView != null) window.SaveDialogueTree(); }
    public static void SaveAsFromMenu() { DialogueTreeEditor window = GetWindow<DialogueTreeEditor>(); if (window != null && window.graphView != null) window.SaveAsDialogueTree(); else EditorUtility.DisplayDialog("Error", "Please open the Dialogue Tree Editor first.", "OK"); }
    #endregion

    #region Unity Lifecycle
    private void OnEnable()
    {
        currentFilePath = EditorPrefs.GetString(CURRENT_FILE_KEY, "");
        rootVisualElement.Clear();
        AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
        EditorApplication.delayCall += DelayedInitialize;
    }

    private void OnDisable() { AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload; if (graphView != null) graphView = null; }
    private void OnDestroy() { CheckUnsavedChangesBeforeClose(); }

    private void OnGUI()
    {
        if (Event.current != null && Event.current.type == EventType.KeyDown && (Event.current.control || Event.current.command) && Event.current.keyCode == KeyCode.S)
        { SaveDialogueTree(); Event.current.Use(); }
    }

    private void UpdateStatusBar()
    {
        if (fileNameLabel == null) return;
        if (!string.IsNullOrEmpty(currentFilePath))
        {
            string dtreeFileName = Path.ChangeExtension(Path.GetFileName(currentFilePath), ".dtree");
            fileNameLabel.text = hasUnsavedChanges ? "* " + dtreeFileName + " (unsaved)" : dtreeFileName;
            fileNameLabel.style.color = new StyleColor(hasUnsavedChanges ? new Color(1f, 0.8f, 0.4f) : new Color(0.8f, 0.8f, 0.8f));
            fileNameLabel.tooltip = Path.ChangeExtension(currentFilePath, ".dtree");
        }
        else
        {
            fileNameLabel.text = hasUnsavedChanges ? "* Untitled (unsaved)" : "No File";
            fileNameLabel.style.color = new StyleColor(hasUnsavedChanges ? new Color(1f, 0.8f, 0.4f) : new Color(0.6f, 0.6f, 0.6f));
            fileNameLabel.tooltip = "";
        }
    }
    #endregion

    #region Initialization
    private void OnBeforeAssemblyReload()
    {
        if (graphView != null && graphView.GetNodeCount() > 0)
        {
            try { DialogueTreeData treeData = graphView.SerializeDialogueTree(); serializedGraphJson = JsonUtility.ToJson(treeData, false); hasSerializedData = true; wasUnsaved = hasUnsavedChanges; }
            catch (System.Exception e) { Debug.LogError($"[Dialogue Editor] Failed to serialize: {e.Message}"); hasSerializedData = false; }
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
                    EditorApplication.delayCall += () => { if (graphView != null) { graphView.CenterOnNode0(); graphView.RefreshAllNodesLanguage(); } };
                    UpdateStatusBar();
                    hasSerializedData = false; serializedGraphJson = ""; wasUnsaved = false;
                    return;
                }
            }
            catch (System.Exception e) { Debug.LogError($"[Dialogue Editor] Failed to restore: {e.Message}"); }
            finally { hasSerializedData = false; serializedGraphJson = ""; wasUnsaved = false; }
        }
        hasUnsavedChanges = false;
        if (!string.IsNullOrEmpty(currentFilePath))
        {
            string dtreePath = Path.ChangeExtension(currentFilePath, ".dtree");
            string pathToLoad = File.Exists(dtreePath) ? dtreePath : currentFilePath;
            Debug.Log($"[DialogueTreeEditor] Attempting to restore file: {pathToLoad}");
            if (File.Exists(pathToLoad))
            {
                string projectDirectory = Directory.GetParent(Application.dataPath).FullName;
                if (pathToLoad.StartsWith(projectDirectory) || Path.IsPathRooted(pathToLoad)) LoadFromFile(pathToLoad);
                else { currentFilePath = ""; EditorPrefs.DeleteKey(CURRENT_FILE_KEY); }
            }
            else { Debug.LogWarning($"[DialogueTreeEditor] File not found: {pathToLoad}"); currentFilePath = ""; EditorPrefs.DeleteKey(CURRENT_FILE_KEY); }
        }
        UpdateStatusBar();
    }

    public void ForceInitialize() { if (rootVisualElement.childCount == 0) { rootVisualElement.Clear(); CreateMainLayout(); } titleContent = new GUIContent("Dialogue Tree Editor"); }

    private void CreateMainLayout()
    {
        var toolbar = new VisualElement();
        toolbar.style.flexDirection = FlexDirection.Row;
        toolbar.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f, 1f));
        toolbar.style.paddingTop = 5; toolbar.style.paddingBottom = 5; toolbar.style.paddingLeft = 10; toolbar.style.paddingRight = 10;
        toolbar.style.borderBottomWidth = 1; toolbar.style.borderBottomColor = new StyleColor(new Color(0.1f, 0.1f, 0.1f, 1f));
        toolbar.style.minHeight = 25; toolbar.style.alignItems = Align.Center;
        fileNameLabel = new Label("No File"); fileNameLabel.style.fontSize = 12; fileNameLabel.style.color = new StyleColor(new Color(0.8f, 0.8f, 0.8f));
        toolbar.Add(fileNameLabel);
        var spacer = new VisualElement(); spacer.style.width = 20; toolbar.Add(spacer);
        chineseButton = CreateLanguageButton("中文", Language.ChineseSimplified); toolbar.Add(chineseButton);
        englishButton = CreateLanguageButton("English", Language.English); toolbar.Add(englishButton);
        japaneseButton = CreateLanguageButton("日本語", Language.Japanese); toolbar.Add(japaneseButton);
        UpdateLanguageButtonStyles();
        rootVisualElement.Add(toolbar);
        graphView = new DialogueGraphViewEditor();
        graphView.SetEditorWindow(this);
        graphView.style.flexGrow = 1;
        graphView.graphViewChanged += OnGraphViewChanged;
        rootVisualElement.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
        rootVisualElement.Add(graphView);
        UpdateStatusBar();
    }

    private void OnKeyDown(KeyDownEvent evt)
    {
        if ((evt.ctrlKey || evt.commandKey) && evt.keyCode == KeyCode.S) { SaveDialogueTree(); evt.StopPropagation(); Debug.Log("Ctrl+S detected - Saving..."); }
    }

    private UnityEditor.Experimental.GraphView.GraphViewChange OnGraphViewChanged(UnityEditor.Experimental.GraphView.GraphViewChange graphViewChange)
    {
        if (graphView != null && graphView.GetNodeCount() > 0) MarkAsChanged();
        return graphViewChange;
    }
    #endregion

    #region Public Methods
    public void MarkAsChanged() { hasUnsavedChanges = true; UpdateStatusBar(); }
    public bool HasUnsavedChanges => hasUnsavedChanges;
    public void NewDialogueTree() { currentFilePath = ""; hasUnsavedChanges = false; EditorPrefs.DeleteKey(CURRENT_FILE_KEY); if (graphView != null) graphView.ClearGraph(); UpdateStatusBar(); }
    #endregion

    #region Save/Load
    public void SaveDialogueTree()
    {
        Debug.Log("[DialogueTreeEditor] SaveDialogueTree called");
        if (graphView == null) { Debug.LogError("[DialogueTreeEditor] GraphView is null, cannot save"); EditorUtility.DisplayDialog("Save Failed", "Editor is not properly initialized.", "OK"); return; }
        if (string.IsNullOrEmpty(currentFilePath)) SaveAsDialogueTree();
        else SaveToFile(currentFilePath, false);
    }

    public void SaveAsDialogueTree()
    {
        Debug.Log("[DialogueTreeEditor] SaveAsDialogueTree called");
        if (graphView == null) { EditorUtility.DisplayDialog("Save Failed", "Editor is not properly initialized.", "OK"); return; }
        string path = EditorUtility.SaveFilePanel("Save Dialogue Tree", Path.Combine(Application.dataPath, "StreamingAssets"), string.IsNullOrEmpty(currentFilePath) ? "DialogueSequence" : Path.GetFileNameWithoutExtension(currentFilePath), "json");
        if (!string.IsNullOrEmpty(path)) { SaveToFile(path, false); currentFilePath = path; EditorPrefs.SetString(CURRENT_FILE_KEY, currentFilePath); }
    }

    private void SaveToFile(string path, bool isAutoSave)
    {
        if (graphView == null) { Debug.LogError("[DialogueTreeEditor] GraphView is null in SaveToFile"); return; }
        if (!path.EndsWith(".json")) path = Path.ChangeExtension(path, ".json");
        string directory = Path.GetDirectoryName(path);
        if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
        try
        {
            SaveRuntimeJsonFile(path);
            string dtreePath = Path.ChangeExtension(path, ".dtree");
            SaveEditorFormatFile(dtreePath);
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            EditorApplication.delayCall += () =>
            {
                if (path.StartsWith(Application.dataPath)) AssetDatabase.ImportAsset("Assets" + path.Substring(Application.dataPath.Length));
                if (dtreePath.StartsWith(Application.dataPath)) AssetDatabase.ImportAsset("Assets" + dtreePath.Substring(Application.dataPath.Length));
            };
            hasUnsavedChanges = false;
            UpdateStatusBar();
            if (!isAutoSave) EditorUtility.DisplayDialog("Save Successful", $"Runtime JSON saved to:\n{path}\n\nEditor DTREE saved to:\n{dtreePath}", "OK");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[DialogueTreeEditor] Failed to save: {e.Message}\n{e.StackTrace}");
            if (!isAutoSave) EditorUtility.DisplayDialog("Save Failed", $"Failed to save:\n{e.Message}", "OK");
        }
    }

    private void SaveRuntimeJsonFile(string path)
    {
        List<RuntimeDialogueData> exportData = graphView.GetDialogueSequence();
        var nodeIdToIndex = new Dictionary<string, int>();
        foreach (var node in graphView.nodes.Cast<DialogueNodeEditor>().OrderBy(n => n.NodeIndex))
            nodeIdToIndex[node.GetId()] = node.NodeIndex;

        string formattedJson = "{\n  \"conversations\": [\n";
        for (int i = 0; i < exportData.Count; i++)
        {
            var item = exportData[i];
            formattedJson += "    {\n";
            formattedJson += $"      \"index\": {item.index},\n";
            formattedJson += $"      \"name\": {SerializeLocalizedText(item.name, 3)},\n";
            formattedJson += $"      \"avatarAddr\": \"{EscapeJsonString(item.avatarAddr)}\",\n";
            formattedJson += $"      \"isPlayer\": {item.isPlayer.ToString().ToLower()},\n";
            // ★ content 后加逗号，然后写 textAlignment
            formattedJson += $"      \"content\": {SerializeLocalizedText(item.content, 3)},\n";
            formattedJson += $"      \"textAlignment\": {(int)item.textAlignment}";

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
                    else { formattedJson += "\n"; }
                    formattedJson += "        }";
                    if (j < item.conditionalBranches.Count - 1) formattedJson += ",";
                    formattedJson += "\n";
                }
                formattedJson += "      ]";
            }
            else
            {
                int nextIndex = (!string.IsNullOrEmpty(item.nextNodeId) && nodeIdToIndex.ContainsKey(item.nextNodeId)) ? nodeIdToIndex[item.nextNodeId] : -1;
                formattedJson += $",\n      \"nextIndex\": {nextIndex}";
            }

            // Choices
            if (item.choices.Count > 0)
            {
                formattedJson += ",\n      \"choices\": [\n";
                for (int j = 0; j < item.choices.Count; j++)
                {
                    var choice = item.choices[j];
                    int targetIndex = (!string.IsNullOrEmpty(choice.nextNodeId) && nodeIdToIndex.ContainsKey(choice.nextNodeId)) ? nodeIdToIndex[choice.nextNodeId] : -1;
                    formattedJson += "        {\n";
                    formattedJson += $"          \"text\": {SerializeLocalizedText(choice.text, 5)},\n";
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
                    else { formattedJson += "\n"; }
                    formattedJson += "        }";
                    if (j < item.choices.Count - 1) formattedJson += ",";
                    formattedJson += "\n";
                }
                formattedJson += "      ]";
            }

            // Events
            if (item.eventCalls.Count > 0)
            {
                formattedJson += ",\n      \"eventCalls\": [\n";
                for (int j = 0; j < item.eventCalls.Count; j++)
                {
                    var evt = item.eventCalls[j];
                    formattedJson += "        {\n";
                    formattedJson += $"          \"targetObjectID\": \"{EscapeJsonString(evt.targetObjectID)}\",\n";
                    formattedJson += $"          \"targetObjectName\": \"{EscapeJsonString(evt.targetObjectName)}\",\n";
                    formattedJson += $"          \"componentTypeName\": \"{EscapeJsonString(evt.componentTypeName)}\",\n";
                    formattedJson += $"          \"methodName\": \"{EscapeJsonString(evt.methodName)}\",\n";
                    formattedJson += $"          \"parameterType\": \"{evt.parameterType}\",\n";
                    formattedJson += $"          \"stringParameter\": \"{EscapeJsonString(evt.stringParameter)}\",\n";
                    formattedJson += $"          \"intParameter\": {evt.intParameter},\n";
                    formattedJson += $"          \"floatParameter\": {evt.floatParameter},\n";
                    formattedJson += $"          \"boolParameter\": {evt.boolParameter.ToString().ToLower()},\n";
                    formattedJson += $"          \"triggerTiming\": {(int)evt.triggerTiming}\n";
                    formattedJson += "        }";
                    if (j < item.eventCalls.Count - 1) formattedJson += ",";
                    formattedJson += "\n";
                }
                formattedJson += "      ]";
            }

            formattedJson += "\n    }";
            if (i < exportData.Count - 1) formattedJson += ",";
            formattedJson += "\n";
        }
        formattedJson += "  ],\n  \"currentIndex\": 0\n}";
        formattedJson = formattedJson.Replace("\r\n", "\n");
        System.Text.UTF8Encoding utf8WithoutBom = new System.Text.UTF8Encoding(false);
        if (!File.Exists(path) || File.ReadAllText(path) != formattedJson)
            File.WriteAllText(path, formattedJson, utf8WithoutBom);
        Debug.Log($"[DialogueTreeEditor] Runtime JSON saved, length: {formattedJson.Length}");
    }

    private void SaveEditorFormatFile(string path)
    {
        DialogueTreeData treeData = graphView.SerializeDialogueTree();
        string json = JsonUtility.ToJson(treeData, true).Trim().Replace("\r\n", "\n");
        System.Text.UTF8Encoding utf8WithoutBom = new System.Text.UTF8Encoding(false);
        if (!File.Exists(path) || File.ReadAllText(path) != json)
            File.WriteAllText(path, json, utf8WithoutBom);
        Debug.Log($"[DialogueTreeEditor] Editor DTREE saved");
    }

    public void LoadDialogueTree()
    {
        if (hasUnsavedChanges && !EditorUtility.DisplayDialog("Unsaved Changes", "You have unsaved changes. Load new file without saving?", "Yes", "Cancel")) return;
        string path = EditorUtility.OpenFilePanel("Load Dialogue Tree", Path.Combine(Application.dataPath, "StreamingAssets"), "dtree");
        if (!string.IsNullOrEmpty(path)) { LoadFromFile(path); currentFilePath = Path.ChangeExtension(path, ".json"); EditorPrefs.SetString(CURRENT_FILE_KEY, currentFilePath); }
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
                currentFilePath = Path.ChangeExtension(path, ".json");
                EditorPrefs.SetString(CURRENT_FILE_KEY, currentFilePath);
                EditorApplication.delayCall += () => { if (graphView != null) { graphView.CenterOnNode0(); graphView.RefreshAllNodesLanguage(); } };
                UpdateStatusBar();
            }
            else { Debug.LogError("Failed to load dialogue tree data"); EditorUtility.DisplayDialog("Load Failed", "Failed to load dialogue tree data or invalid file format", "OK"); }
        }
        catch (System.Exception e) { Debug.LogError($"Failed to load dialogue tree: {e.Message}"); EditorUtility.DisplayDialog("Load Failed", $"Failed to load dialogue tree:\n{e.Message}", "OK"); }
    }
    #endregion

    #region Helper Methods
    private void CheckUnsavedChangesBeforeClose()
    {
        if (hasUnsavedChanges && graphView != null && graphView.GetNodeCount() > 0)
        {
            int result = EditorUtility.DisplayDialogComplex("Unsaved Changes", "You have unsaved changes. What would you like to do?", "Save", "Don't Save", "Cancel");
            switch (result) { case 0: SaveDialogueTree(); break; }
        }
    }

    private string SerializeLocalizedText(LocalizedText text, int indentLevel = 2)
    {
        if (text == null) text = new LocalizedText();
        string indent = new string(' ', indentLevel * 2);
        return "{\n" + $"{indent}  \"en\": \"{EscapeJsonString(text.en)}\",\n" + $"{indent}  \"zh\": \"{EscapeJsonString(text.zh)}\",\n" + $"{indent}  \"ja\": \"{EscapeJsonString(text.ja)}\"\n" + $"{indent}}}";
    }

    private string EscapeJsonString(string str)
    {
        if (string.IsNullOrEmpty(str)) return "";
        return str.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
    }
    #endregion

    #region Character Update
    public void RefreshAllCharacterDisplays()
    {
        if (graphView == null) return;
        var nodes = graphView.nodes.Cast<DialogueNodeEditor>().ToList();
        foreach (var node in nodes) node.RefreshCharacterDisplay();
        Debug.Log($"[Dialogue Editor] Refreshed character displays for {nodes.Count} nodes");
    }

    public static void RefreshAllOpenEditors()
    {
        var windows = Resources.FindObjectsOfTypeAll<DialogueTreeEditor>();
        foreach (var window in windows) window.RefreshAllCharacterDisplays();
        Debug.Log($"[Dialogue Editor] Refreshed {windows.Length} open editor window(s)");
    }

    #region Language Management
    private Button CreateLanguageButton(string text, Language language)
    {
        var button = new Button(() => SwitchLanguage(language)) { text = text };
        button.style.height = 20; button.style.minWidth = 60; button.style.fontSize = 11; button.style.marginLeft = 3; button.style.marginRight = 3;
        return button;
    }

    private void SwitchLanguage(Language language) { if (currentLanguage == language) return; currentLanguage = language; UpdateLanguageButtonStyles(); graphView?.RefreshAllNodesLanguage(); }

    private void UpdateLanguageButtonStyles()
    {
        UpdateButtonStyle(englishButton, currentLanguage == Language.English);
        UpdateButtonStyle(chineseButton, currentLanguage == Language.ChineseSimplified);
        UpdateButtonStyle(japaneseButton, currentLanguage == Language.Japanese);
    }

    private void UpdateButtonStyle(Button button, bool isActive)
    {
        if (button == null) return;
        if (isActive) { button.style.backgroundColor = new StyleColor(new Color(0.3f, 0.5f, 0.8f)); button.style.color = new StyleColor(Color.white); button.style.unityFontStyleAndWeight = FontStyle.Bold; }
        else { button.style.backgroundColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f)); button.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f)); button.style.unityFontStyleAndWeight = FontStyle.Normal; }
    }

    public Language GetCurrentLanguage() => currentLanguage;
    public void RefreshLanguageDisplay() { graphView?.RefreshAllNodesLanguage(); }
    #endregion
    #endregion
}
