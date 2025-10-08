using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.Experimental.GraphView;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DialogueSystem.Editor
{
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
            var window = GetWindow<DialogueTreeEditor>();
            window.titleContent = new GUIContent("Dialogue Tree Editor");
            window.minSize = new Vector2(1000, 600);
            window.Show();
            window.ForceInitialize();
        }

        [MenuItem("Tools/Dialogue Tree Editor/Create New")]
        public static void CreateNewFromMenu()
        {
            var window = GetWindow<DialogueTreeEditor>();
            window.Show();
            window.ForceInitialize();
            if (window.hasUnsavedChanges && !EditorUtility.DisplayDialog("New Document",
                "You have unsaved changes. Create new document without saving?", "Yes", "Cancel"))
                return;
            window.NewDialogueTree();
        }

        [MenuItem("Tools/Dialogue Tree Editor/Load")]
        public static void LoadFromMenu()
        {
            var window = GetWindow<DialogueTreeEditor>();
            window.Show();
            window.ForceInitialize();
            window.LoadDialogueTree();
        }

        [MenuItem("Tools/Dialogue Tree Editor/Save Current")]
        public static void SaveCurrentFromMenu()
        {
            var window = GetWindow<DialogueTreeEditor>();
            if (window?.graphView != null) window.SaveDialogueTree();
        }

        [MenuItem("Tools/Dialogue Tree Editor/Save As...")]
        public static void SaveAsFromMenu()
        {
            var window = GetWindow<DialogueTreeEditor>();
            if (window?.graphView != null)
                window.SaveAsDialogueTree();
            else
                EditorUtility.DisplayDialog("Error", "Please open the editor first.", "OK");
        }

        private void OnEnable()
        {
            currentFilePath = EditorPrefs.GetString(CURRENT_FILE_KEY, "");
            rootVisualElement.Clear();
            EditorApplication.delayCall += DelayedInitialize;
        }

        private void DelayedInitialize()
        {
            CreateMainLayout();
            hasUnsavedChanges = false;

            if (!string.IsNullOrEmpty(currentFilePath) && File.Exists(currentFilePath))
            {
                string projectDir = Directory.GetParent(Application.dataPath).FullName;
                if (currentFilePath.StartsWith(projectDir) || Path.IsPathRooted(currentFilePath))
                    LoadFromFile(currentFilePath);
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

        private void OnDestroy()
        {
            if (hasUnsavedChanges && graphView?.GetNodeCount() > 0)
            {
                int result = EditorUtility.DisplayDialogComplex("Unsaved Changes",
                    "You have unsaved changes. What would you like to do?",
                    "Save", "Don't Save", "Cancel");
                if (result == 0) SaveDialogueTree();
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

        private void CreateMainLayout()
        {
            var mainContainer = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, flexGrow = 1 }
            };

            variablesPanel = new VariablesPanel(this);
            variablesPanel.style.width = 250;
            variablesPanel.style.borderRightWidth = 2;
            variablesPanel.style.borderRightColor = new Color(0.1f, 0.1f, 0.1f);
            variablesPanel.SetVariables(variables);

            graphView = new DialogueGraphView();
            graphView.SetEditorWindow(this);
            graphView.style.flexGrow = 1;
            graphView.graphViewChanged += OnGraphViewChanged;

            mainContainer.Add(variablesPanel);
            mainContainer.Add(graphView);
            rootVisualElement.Add(mainContainer);
        }

        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            if (graphView?.GetNodeCount() > 0) MarkAsChanged();
            return change;
        }

        public void MarkAsChanged() => hasUnsavedChanges = true;
        public bool HasUnsavedChanges => hasUnsavedChanges;
        public List<DialogueVariable> GetVariables() => variables;

        public void NotifyVariablesChanged()
        {
            graphView?.RefreshAllNodesConditions();
        }

        public void NewDialogueTree()
        {
            currentFilePath = "";
            hasUnsavedChanges = false;
            EditorPrefs.DeleteKey(CURRENT_FILE_KEY);
            variables.Clear();
            graphView?.ClearGraph();
            variablesPanel?.SetVariables(variables);
        }

        public void SaveDialogueTree()
        {
            if (string.IsNullOrEmpty(currentFilePath))
                SaveAsDialogueTree();
            else
                SaveToFile(currentFilePath);
        }

        public void SaveAsDialogueTree()
        {
            string path = EditorUtility.SaveFilePanel("Save Dialogue Tree",
                Path.Combine(Application.dataPath, "StreamingAssets"),
                string.IsNullOrEmpty(currentFilePath) ? "DialogueSequence" : Path.GetFileNameWithoutExtension(currentFilePath),
                "json");

            if (!string.IsNullOrEmpty(path))
            {
                SaveToFile(path);
                currentFilePath = path;
                EditorPrefs.SetString(CURRENT_FILE_KEY, currentFilePath);
            }
        }

        private void SaveToFile(string path)
        {
            if (graphView == null) return;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));

                SaveRuntimeJsonFile(path);
                SaveEditorFormatFile(Path.ChangeExtension(path, ".dtree"));

                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                hasUnsavedChanges = false;

                Debug.Log($"Saved: {path}");
                EditorUtility.DisplayDialog("Save Successful", $"Saved to:\n{path}", "OK");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Save failed: {e.Message}");
                EditorUtility.DisplayDialog("Save Failed", $"Error:\n{e.Message}", "OK");
            }
        }

        private void SaveRuntimeJsonFile(string path)
        {
            var exportData = graphView.GetDialogueSequence();
            var nodeIdToIndex = graphView.nodes.Cast<DialogueNode>()
                .OrderBy(n => n.NodeIndex)
                .ToDictionary(n => n.GetId(), n => n.NodeIndex);

            var json = new System.Text.StringBuilder();
            json.Append("{\n  \"variables\": [\n");

            for (int i = 0; i < variables.Count; i++)
            {
                var v = variables[i];
                json.Append($"    {{\"name\":\"{Escape(v.name)}\",\"type\":\"{v.type}\",\"defaultValue\":\"{Escape(v.defaultValue)}\"}}");
                if (i < variables.Count - 1) json.Append(",");
                json.Append("\n");
            }

            json.Append("  ],\n  \"conversations\": [\n");

            for (int i = 0; i < exportData.Count; i++)
            {
                var item = exportData[i];
                json.Append("    {\n");
                json.Append($"      \"index\":{item.index},\n");
                json.Append($"      \"name\":\"{Escape(item.name)}\",\n");
                json.Append($"      \"avatarAddr\":\"{Escape(item.avatarAddr)}\",\n");
                json.Append($"      \"content\":\"{Escape(item.content)}\",\n");
                json.Append($"      \"nextIndex\":{(nodeIdToIndex.ContainsKey(item.nextNodeId ?? "") ? nodeIdToIndex[item.nextNodeId] : -1)},\n");

                json.Append("      \"choices\":[");
                for (int j = 0; j < item.choices.Count; j++)
                {
                    var choice = item.choices[j];
                    json.Append($"{{\"text\":\"{Escape(choice.text)}\",");
                    json.Append($"\"targetIndex\":{(nodeIdToIndex.ContainsKey(choice.nextNodeId ?? "") ? nodeIdToIndex[choice.nextNodeId] : -1)}");

                    if (choice.conditions.Count > 0)
                    {
                        json.Append(",\"conditions\":[");
                        for (int k = 0; k < choice.conditions.Count; k++)
                        {
                            var c = choice.conditions[k];
                            json.Append($"{{\"variableName\":\"{Escape(c.variableName)}\",\"comparison\":\"{c.comparison}\",\"compareValue\":\"{Escape(c.compareValue)}\"}}");
                            if (k < choice.conditions.Count - 1) json.Append(",");
                        }
                        json.Append($"],\"conditionLogic\":\"{choice.conditionLogic}\"");
                    }
                    json.Append("}");
                    if (j < item.choices.Count - 1) json.Append(",");
                }
                json.Append("],\n");

                json.Append("      \"eventCalls\":[");
                for (int j = 0; j < item.eventCalls.Count; j++)
                {
                    var e = item.eventCalls[j];
                    json.Append($"{{\"targetObjectName\":\"{Escape(e.targetObjectName)}\",");
                    json.Append($"\"componentTypeName\":\"{Escape(e.componentTypeName)}\",");
                    json.Append($"\"methodName\":\"{Escape(e.methodName)}\",");
                    json.Append($"\"parameterType\":\"{e.parameterType}\",");
                    json.Append($"\"stringParameter\":\"{Escape(e.stringParameter)}\",");
                    json.Append($"\"intParameter\":{e.intParameter},");
                    json.Append($"\"floatParameter\":{e.floatParameter},");
                    json.Append($"\"boolParameter\":{e.boolParameter.ToString().ToLower()}}}");
                    if (j < item.eventCalls.Count - 1) json.Append(",");
                }
                json.Append("]\n    }");
                if (i < exportData.Count - 1) json.Append(",");
                json.Append("\n");
            }

            json.Append("  ],\n  \"currentIndex\":0\n}");
            File.WriteAllText(path, json.ToString());
        }

        private void SaveEditorFormatFile(string path)
        {
            var treeData = graphView.SerializeDialogueTree();
            treeData.variables = new List<DialogueVariable>(variables);
            File.WriteAllText(path, JsonUtility.ToJson(treeData, true));
        }

        public void LoadDialogueTree()
        {
            if (hasUnsavedChanges && !EditorUtility.DisplayDialog("Unsaved Changes",
                "Load without saving?", "Yes", "Cancel"))
                return;

            string path = EditorUtility.OpenFilePanel("Load Dialogue Tree",
                Path.Combine(Application.dataPath, "StreamingAssets"), "dtree");

            if (!string.IsNullOrEmpty(path))
            {
                LoadFromFile(path);
                currentFilePath = File.Exists(Path.ChangeExtension(path, ".json"))
                    ? Path.ChangeExtension(path, ".json") : path;
                EditorPrefs.SetString(CURRENT_FILE_KEY, currentFilePath);
            }
        }

        private void LoadFromFile(string path)
        {
            if (graphView == null) return;

            try
            {
                var treeData = JsonUtility.FromJson<DialogueTreeData>(File.ReadAllText(path));
                if (treeData != null)
                {
                    variables = treeData.variables ?? new List<DialogueVariable>();
                    variablesPanel?.SetVariables(variables);
                    graphView.LoadDialogueTree(treeData);
                    hasUnsavedChanges = false;
                    EditorApplication.delayCall += () => graphView?.CenterOnNode0();
                    Debug.Log($"Loaded: {path}");
                }
                else
                {
                    EditorUtility.DisplayDialog("Load Failed", "Invalid file format", "OK");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Load failed: {e.Message}");
                EditorUtility.DisplayDialog("Load Failed", $"Error:\n{e.Message}", "OK");
            }
        }

        private string Escape(string str)
        {
            if (string.IsNullOrEmpty(str)) return "";
            return str.Replace("\\", "\\\\").Replace("\"", "\\\"")
                      .Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
        }
    }
}