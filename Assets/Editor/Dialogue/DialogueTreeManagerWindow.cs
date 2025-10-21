using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DialogueSystem;

// 简单的输入对话框辅助类
public class EditorInputDialog : EditorWindow
{
    private string inputText = "";
    private string dialogTitle = "";
    private string message = "";
    private System.Action<string> onResult;

    public static void ShowAsync(string title, string message, string defaultValue, System.Action<string> onResult)
    {
        var window = CreateInstance<EditorInputDialog>();
        window.titleContent = new GUIContent(title);
        window.dialogTitle = title;
        window.message = message;
        window.inputText = defaultValue;
        window.minSize = new Vector2(300, 100);
        window.maxSize = new Vector2(300, 100);
        window.onResult = onResult;
        window.ShowUtility();
    }

    private void OnDestroy()
    {
        onResult?.Invoke(inputText);
    }

    private void OnGUI()
    {
        if (position.width <= 0 || position.height <= 0)
            return;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField(message, EditorStyles.wordWrappedLabel);
        EditorGUILayout.Space(5);

        GUI.SetNextControlName("InputField");
        inputText = EditorGUILayout.TextField(inputText);

        EditorGUILayout.Space(10);

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        if (GUILayout.Button("OK", GUILayout.Width(80)))
        {
            Close();
        }

        if (GUILayout.Button("Cancel", GUILayout.Width(80)))
        {
            inputText = "";
            Close();
        }

        EditorGUILayout.EndHorizontal();

        if (Event.current.type == EventType.Layout)
        {
            EditorGUI.FocusTextInControl("InputField");
        }

        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return)
        {
            Close();
        }
    }
}

public class DialogueTreeManagerWindow : EditorWindow
{
    [System.Serializable]
    private class VirtualFolder
    {
        public string name;
        public string id;
        public string description = "";
        public bool isExpanded = true;
        public List<string> fileGuids = new List<string>();
        public List<VirtualFolder> subfolders = new List<VirtualFolder>();
    }

    [System.Serializable]
    private class VirtualFolderData
    {
        public List<VirtualFolder> rootFolders = new List<VirtualFolder>();
        public List<string> rootFileGuids = new List<string>();
    }

    private VirtualFolderData folderData;
    private CharacterLibraryData characterLibrary;
    private Dictionary<string, string> guidToPath = new Dictionary<string, string>();
    private Vector2 scrollPos;
    private VirtualFolder draggedFromFolder;
    private string draggedFileGuid;
    private CharacterData draggedCharacter;
    private bool charactersExpanded = true;
    private string editingCharacterId = "";  // 正在编辑的角色ID
    private Dictionary<string, Sprite> tempSelectedSprites = new Dictionary<string, Sprite>();  // 临时选择的sprite

    // 拖拽排序相关
    private VirtualFolder draggedFolder;
    private VirtualFolder draggedFolderParent;
    private bool isDraggingForReorder = false;

    // 插入位置提示
    private VirtualFolder insertBeforeFolder = null;  // 在哪个文件夹之前/后插入
    private string insertBeforeFileGuid = null;       // 在哪个文件之前/后插入
    private VirtualFolder insertParentFolder = null;  // 插入的父文件夹
    private bool insertAfter = false;                 // true=插入到目标后面, false=插入到目标前面

    [MenuItem("Tools/Dialogue Tree Manager")]
    public static void ShowWindow()
    {
        var window = GetWindow<DialogueTreeManagerWindow>();
        window.titleContent = new GUIContent("Dialogue Manager");
        window.minSize = new Vector2(400, 500);
        window.Show();
    }

    private void OnEnable()
    {
        LoadVirtualFolderStructure();
        LoadCharacterLibrary();
        ScanAllDialogueTrees();
    }

    private void OnDisable()
    {
        SaveVirtualFolderStructure();
        SaveCharacterLibraryInternal();
    }

    private void OnGUI()
    {
        DrawToolbar();
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
        DrawCharactersSection();
        EditorGUILayout.Space(5);

        foreach (var folder in folderData.rootFolders)
        {
            DrawVirtualFolder(folder, 0, null);
        }

        foreach (var guid in folderData.rootFileGuids.ToList())
        {
            if (guidToPath.ContainsKey(guid))
            {
                DrawFile(guid, 0, null);
            }
        }

        EditorGUILayout.EndScrollView();

        if (Event.current.type == EventType.DragExited)
        {
            draggedCharacter = null;

            // 清除所有插入位置提示
            insertBeforeFolder = null;
            insertBeforeFileGuid = null;
            insertParentFolder = null;
            insertAfter = false;
            Repaint();
        }
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
        {
            ScanAllDialogueTrees();
        }

        var exportContent = new GUIContent("Export", "Export all dialogues to CSV");
        if (GUILayout.Button(exportContent, EditorStyles.toolbarButton, GUILayout.Width(60)))
        {
            ExportAllToCSV();
        }

        GUILayout.FlexibleSpace();

        int fileCount = guidToPath.Count;
        int charCount = characterLibrary?.characters?.Length ?? 0;
        EditorGUILayout.LabelField($"Files: {fileCount} | Characters: {charCount}", EditorStyles.miniLabel);

        EditorGUILayout.EndHorizontal();
    }

    #region Characters Section

    private void DrawCharactersSection()
    {
        EditorGUILayout.BeginVertical();

        Rect rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.Height(22));

        if (Event.current.type == EventType.Repaint)
        {
            GUI.Box(rect, "", "box");
        }

        Rect arrowRect = new Rect(rect.x + 5, rect.y + 3, 15, rect.height);
        if (GUI.Button(arrowRect, charactersExpanded ? "▼" : "▶", EditorStyles.label))
        {
            charactersExpanded = !charactersExpanded;
        }

        Rect labelRect = new Rect(rect.x + 25, rect.y + 3, rect.width - 140, rect.height);
        GUI.Label(labelRect, "All Characters", EditorStyles.boldLabel);

        Rect newCharRect = new Rect(rect.xMax - 135, rect.y + 2, 130, 18);
        if (GUI.Button(newCharRect, "+ New Character", EditorStyles.miniButton))
        {
            CreateNewCharacter();
        }

        EditorGUILayout.EndVertical();

        if (charactersExpanded)
        {
            if (characterLibrary?.characters != null && characterLibrary.characters.Length > 0)
            {
                for (int i = 0; i < characterLibrary.characters.Length; i++)
                {
                    DrawCharacter(characterLibrary.characters[i], i);
                }
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(25);
                EditorGUILayout.LabelField("No characters. Click '+ New Character' to create one.", EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
            }
        }
    }

    private void DrawCharacter(CharacterData character, int index)
    {
        bool isEditing = editingCharacterId == character.id;

        EditorGUILayout.BeginVertical();
        GUILayout.Space(2);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(25);

        // 使用box样式作为背景
        var boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.padding = new RectOffset(8, 8, 8, 8);

        EditorGUILayout.BeginVertical(boxStyle, GUILayout.MinHeight(isEditing ? 120 : 70));

        // 第一行：名字和按钮
        EditorGUILayout.BeginHorizontal();

        if (isEditing)
        {
            EditorGUILayout.LabelField("Name:", GUILayout.Width(45));
            string newName = EditorGUILayout.TextField(character.characterName);
            if (newName != character.characterName)
            {
                character.characterName = newName;
                SaveCharacterLibrary(character.id);
            }
        }
        else
        {
            EditorGUILayout.LabelField(character.characterName, EditorStyles.boldLabel);
        }

        GUILayout.FlexibleSpace();

        if (!isEditing)
        {
            if (GUILayout.Button("Edit", EditorStyles.miniButton, GUILayout.Width(60)))
            {
                editingCharacterId = character.id;
            }
        }
        else
        {
            if (GUILayout.Button("Done", EditorStyles.miniButton, GUILayout.Width(60)))
            {
                // 检查是否有未保存的错误选择
                bool hasError = false;
                if (tempSelectedSprites.ContainsKey(character.id))
                {
                    var tempSprite = tempSelectedSprites[character.id];
                    string tempPath = AssetDatabase.GetAssetPath(tempSprite);
                    if (!tempPath.Contains("/Resources/"))
                    {
                        hasError = true;
                    }
                }

                if (hasError)
                {
                    EditorUtility.DisplayDialog("Cannot Save",
                        "The selected avatar is not in a Resources folder!\n\nPlease select a sprite from a Resources folder, or clear the selection to continue.",
                        "OK");
                }
                else
                {
                    editingCharacterId = "";
                    tempSelectedSprites.Remove(character.id);
                }
            }
        }

        GUI.backgroundColor = new Color(1f, 0.7f, 0.7f);
        if (GUILayout.Button("Del", EditorStyles.miniButton, GUILayout.Width(60)))
        {
            DeleteCharacter(index);
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndHorizontal();

        // 编辑模式下显示avatar选择
        if (isEditing)
        {
            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Avatar:", GUILayout.Width(45));

            // 获取当前显示的sprite（优先使用临时选择，否则从保存的路径加载）
            Sprite displaySprite = null;
            if (tempSelectedSprites.ContainsKey(character.id))
            {
                displaySprite = tempSelectedSprites[character.id];
            }
            else if (!string.IsNullOrEmpty(character.avatarAssetPath))
            {
                displaySprite = AssetDatabase.LoadAssetAtPath<Sprite>(character.avatarAssetPath);
            }

            EditorGUI.BeginChangeCheck();
            Sprite newSprite = (Sprite)EditorGUILayout.ObjectField(displaySprite, typeof(Sprite), false);

            if (EditorGUI.EndChangeCheck())
            {
                // 存储用户选择的sprite（无论是否在Resources）
                if (newSprite != null)
                {
                    tempSelectedSprites[character.id] = newSprite;

                    string assetPath = AssetDatabase.GetAssetPath(newSprite);

                    // 只有在 Resources 文件夹才保存
                    if (assetPath.Contains("/Resources/"))
                    {
                        character.avatarAssetPath = assetPath;
                        SaveCharacterLibrary(character.id);
                        // 保存成功后清除临时选择
                        tempSelectedSprites.Remove(character.id);
                    }
                }
                else
                {
                    // 清空选择
                    tempSelectedSprites.Remove(character.id);
                    character.avatarAssetPath = "";
                    SaveCharacterLibrary(character.id);
                }
            }

            EditorGUILayout.EndHorizontal();

            // 显示状态提示
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(45);

            // 判断当前显示的sprite的状态
            string currentPath = "";
            if (displaySprite != null)
            {
                currentPath = AssetDatabase.GetAssetPath(displaySprite);
            }

            if (string.IsNullOrEmpty(currentPath))
            {
                var hintStyle = new GUIStyle(EditorStyles.miniLabel);
                hintStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
                EditorGUILayout.LabelField("(Must be in a Resources folder)", hintStyle);
            }
            else if (!currentPath.Contains("/Resources/"))
            {
                // 红色警告
                var errorStyle = new GUIStyle(EditorStyles.miniLabel);
                errorStyle.normal.textColor = new Color(1f, 0.3f, 0.3f);
                EditorGUILayout.LabelField("ERROR: Not in a Resources folder!", errorStyle);
            }
            else
            {
                // 绿色成功
                var successStyle = new GUIStyle(EditorStyles.miniLabel);
                successStyle.normal.textColor = new Color(0.3f, 1f, 0.3f);
                EditorGUILayout.LabelField($"OK: {Path.GetFileName(currentPath)}", successStyle);
            }

            EditorGUILayout.EndHorizontal();
        }
        else
        {
            // 非编辑模式显示简单信息
            EditorGUILayout.BeginHorizontal();

            // 显示avatar预览
            Sprite avatarSprite = null;
            if (!string.IsNullOrEmpty(character.avatarAssetPath))
            {
                avatarSprite = AssetDatabase.LoadAssetAtPath<Sprite>(character.avatarAssetPath);
            }

            if (avatarSprite != null)
            {
                Texture2D texture = avatarSprite.texture;
                Rect spriteRect = avatarSprite.rect;
                Rect previewRect = GUILayoutUtility.GetRect(40, 40, GUILayout.Width(40), GUILayout.Height(40));

                Rect texCoords = new Rect(
                    spriteRect.x / texture.width,
                    spriteRect.y / texture.height,
                    spriteRect.width / texture.width,
                    spriteRect.height / texture.height
                );
                GUI.DrawTextureWithTexCoords(previewRect, texture, texCoords);
            }
            else
            {
                Rect previewRect = GUILayoutUtility.GetRect(40, 40, GUILayout.Width(40), GUILayout.Height(40));
                GUI.Box(previewRect, "No\nImg", EditorStyles.miniLabel);
            }

            EditorGUILayout.BeginVertical();
            GUILayout.FlexibleSpace();

            var pathStyle = new GUIStyle(EditorStyles.miniLabel);
            pathStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
            string displayPath = string.IsNullOrEmpty(character.avatarAssetPath) ?
                "(No avatar)" : Path.GetFileName(character.avatarAssetPath);
            EditorGUILayout.LabelField(displayPath, pathStyle);

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();

        // 处理拖拽（只在非编辑模式）
        if (!isEditing)
        {
            Rect lastRect = GUILayoutUtility.GetLastRect();
            HandleCharacterDrag(lastRect, character);
        }
    }

    private void HandleCharacterDrag(Rect rect, CharacterData character)
    {
        Event e = Event.current;

        if (e.type == EventType.MouseDown && rect.Contains(e.mousePosition))
        {
            draggedCharacter = character;
        }

        if (e.type == EventType.MouseDrag && draggedCharacter == character)
        {
            DragAndDrop.PrepareStartDrag();
            DragAndDrop.SetGenericData("CharacterData", character);
            DragAndDrop.objectReferences = new UnityEngine.Object[0];
            DragAndDrop.StartDrag($"Character: {character.characterName}");
            e.Use();
        }
    }

    private void CreateNewCharacter()
    {
        var newChar = new CharacterData("New Character", "");

        var list = new List<CharacterData>();
        if (characterLibrary.characters != null)
        {
            list.AddRange(characterLibrary.characters);
        }
        list.Add(newChar);
        characterLibrary.characters = list.ToArray();

        SaveCharacterLibraryInternal();

        // 自动进入编辑模式
        editingCharacterId = newChar.id;
    }

    private void DeleteCharacter(int index)
    {
        if (EditorUtility.DisplayDialog("Delete Character",
            $"Delete character '{characterLibrary.characters[index].characterName}'?\n\nNote: Dialogue nodes using this character will show 'Unknown Character'.",
            "Delete", "Cancel"))
        {
            string deletedCharacterId = characterLibrary.characters[index].id;

            var list = new List<CharacterData>(characterLibrary.characters);
            list.RemoveAt(index);
            characterLibrary.characters = list.ToArray();

            // 清理临时选择
            tempSelectedSprites.Remove(deletedCharacterId);

            SaveCharacterLibraryInternal();

            EditorApplication.delayCall += () =>
            {
                DialogueTreeEditor.RefreshAllOpenEditors();
            };
        }
    }

    private void SaveCharacterLibraryInternal()
    {
        try
        {
            string savePath = GetCharacterLibraryPath();
            string json = JsonUtility.ToJson(characterLibrary, true);

            string folder = Path.GetDirectoryName(savePath);
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            File.WriteAllText(savePath, json);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to save character library: {e.Message}");
        }
    }

    private void SaveCharacterLibrary(string modifiedCharacterId)
    {
        try
        {
            string savePath = GetCharacterLibraryPath();
            string json = JsonUtility.ToJson(characterLibrary, true);

            string folder = Path.GetDirectoryName(savePath);
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            File.WriteAllText(savePath, json);

            EditorApplication.delayCall += () =>
            {
                DialogueTreeEditor.RefreshAllOpenEditors();
                RegenerateAffectedRuntimeJSON(modifiedCharacterId);
            };
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to save character library: {e.Message}");
        }
    }

    private void LoadCharacterLibrary()
    {
        try
        {
            string loadPath = GetCharacterLibraryPath();

            if (File.Exists(loadPath))
            {
                string json = File.ReadAllText(loadPath);
                characterLibrary = JsonUtility.FromJson<CharacterLibraryData>(json);
                Debug.Log($"Loaded character library from: {loadPath}");
            }
            else
            {
                characterLibrary = new CharacterLibraryData();
                Debug.Log("No existing character library found, created new one");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to load character library: {e.Message}");
            characterLibrary = new CharacterLibraryData();
        }
    }

    private void RegenerateAffectedRuntimeJSON(string modifiedCharacterId)
    {
        int regeneratedCount = 0;
        List<string> affectedFiles = new List<string>();

        foreach (var kvp in guidToPath)
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
                            RegenerateSingleRuntimeJSON(treeData, jsonPath);
                            affectedFiles.Add(Path.GetFileName(dtreePath));
                            regeneratedCount++;
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Failed to check/regenerate {Path.GetFileName(dtreePath)}: {e.Message}");
            }
        }

        if (regeneratedCount > 0)
        {
            AssetDatabase.Refresh();
            Debug.Log($"[Character Library] Auto-regenerated {regeneratedCount} affected runtime JSON file(s)");

            string fileList = string.Join("\n• ", affectedFiles);
            EditorUtility.DisplayDialog("Character Updated",
                $"Character saved!\n\nAuto-regenerated {regeneratedCount} file(s):\n\n• {fileList}",
                "OK");
        }
    }

    private void RegenerateSingleRuntimeJSON(DialogueTreeData treeData, string jsonPath)
    {
        var tempGraphView = new DialogueGraphView();
        tempGraphView.LoadDialogueTree(treeData);

        List<RuntimeDialogueData> exportData = tempGraphView.GetDialogueSequence();
        var nodeIdToIndex = new Dictionary<string, int>();

        foreach (var node in treeData.nodes.OrderBy(n => n.index))
        {
            nodeIdToIndex[node.id] = node.index;
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

        File.WriteAllText(jsonPath, formattedJson);
    }

    #endregion

    #region Virtual Folders

    private void DrawVirtualFolder(VirtualFolder folder, int indentLevel, VirtualFolder parentFolder)
    {
        // 绘制插入线提示（在文件夹之前）
        if (insertBeforeFolder == folder && insertParentFolder == parentFolder && !insertAfter)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(indentLevel * 20);
            Rect insertLineRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.Height(3));
            EditorGUI.DrawRect(insertLineRect, new Color(0.3f, 0.6f, 1f, 0.8f)); // 蓝色插入线
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.BeginVertical();
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(indentLevel * 20);

        Rect rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.Height(22));

        if (Event.current.type == EventType.Repaint)
        {
            GUI.Box(rect, "", "box");
        }

        Rect arrowRect = new Rect(rect.x + 5, rect.y + 3, 15, rect.height);
        if (GUI.Button(arrowRect, folder.isExpanded ? "▼" : "▶", EditorStyles.label))
        {
            folder.isExpanded = !folder.isExpanded;
        }

        Rect labelRect = new Rect(rect.x + 25, rect.y + 3, rect.width - 200, rect.height);
        GUI.Label(labelRect, folder.name, EditorStyles.boldLabel);

        if (folder.id != "default_folder")
        {
            Rect renameRect = new Rect(rect.xMax - 130, rect.y + 2, 60, 18);
            if (GUI.Button(renameRect, "Rename", EditorStyles.miniButton))
            {
                RenameFolder(folder);
            }

            Rect deleteRect = new Rect(rect.xMax - 65, rect.y + 2, 60, 18);
            GUI.backgroundColor = new Color(1f, 0.7f, 0.7f);
            if (GUI.Button(deleteRect, "Del", EditorStyles.miniButton))
            {
                string folderName = folder.name;
                EditorApplication.delayCall += () =>
                {
                    if (EditorUtility.DisplayDialog("Delete Folder",
                        $"Delete folder '{folderName}'? All files will move to 'All Dialogues' folder.",
                        "Delete", "Cancel"))
                    {
                        DeleteVirtualFolder(folder, parentFolder);
                    }
                };
            }
            GUI.backgroundColor = Color.white;
        }
        else
        {
            // All Dialogues 文件夹的 New 按钮 - 创建新 dtree 文件
            Rect newFileRect = new Rect(rect.xMax - 135, rect.y + 2, 130, 18);
            if (GUI.Button(newFileRect, "+ New Dialogue", EditorStyles.miniButton))
            {
                CreateNewDialogueFile(folder);
            }
        }

        EditorGUILayout.EndHorizontal();

        if (!string.IsNullOrEmpty(folder.description))
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(indentLevel * 20 + 25);

            var descStyle = new GUIStyle(EditorStyles.miniLabel);
            descStyle.fontSize = 10;
            descStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
            descStyle.fontStyle = FontStyle.Italic;
            descStyle.wordWrap = true;

            Rect descRect = GUILayoutUtility.GetRect(
                new GUIContent(folder.description),
                descStyle,
                GUILayout.ExpandWidth(true)
            );

            if (Event.current.type == EventType.MouseDown && descRect.Contains(Event.current.mousePosition))
            {
                if (Event.current.clickCount == 2)
                {
                    EditDescription(folder);
                    Event.current.Use();
                }
            }

            GUI.Label(descRect, folder.description, descStyle);
            EditorGUILayout.EndHorizontal();
        }
        else if (folder.id != "default_folder")
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(indentLevel * 20 + 25);

            var hintStyle = new GUIStyle(EditorStyles.miniLabel);
            hintStyle.fontSize = 9;
            hintStyle.normal.textColor = new Color(0.5f, 0.5f, 0.5f);
            hintStyle.fontStyle = FontStyle.Italic;

            Rect hintRect = GUILayoutUtility.GetRect(
                new GUIContent("Add description..."),
                hintStyle,
                GUILayout.Width(150)
            );

            if (GUI.Button(hintRect, "Add description...", hintStyle))
            {
                EditDescription(folder);
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndVertical();
        HandleFolderDragAndDrop(rect, folder);
        HandleFolderDragForReorder(rect, folder, parentFolder);
        HandleFolderContextMenu(rect, folder);

        if (folder.isExpanded)
        {
            foreach (var subfolder in folder.subfolders.ToList())
            {
                DrawVirtualFolder(subfolder, indentLevel + 1, folder);
            }

            foreach (var guid in folder.fileGuids.ToList())
            {
                if (guidToPath.ContainsKey(guid))
                {
                    DrawFile(guid, indentLevel + 1, folder);
                }
            }
        }

        // 绘制插入线提示（在文件夹之后）
        if (insertBeforeFolder == folder && insertParentFolder == parentFolder && insertAfter)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(indentLevel * 20);
            Rect insertLineRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.Height(3));
            EditorGUI.DrawRect(insertLineRect, new Color(0.3f, 0.6f, 1f, 0.8f)); // 蓝色插入线
            EditorGUILayout.EndHorizontal();
        }
    }

    private void EditDescription(VirtualFolder folder)
    {
        EditorApplication.delayCall += () =>
        {
            EditorInputDialog.ShowAsync("Edit Description", "Enter folder description:", folder.description, (newDesc) =>
            {
                if (newDesc != null)
                {
                    folder.description = newDesc.Trim();
                    SaveVirtualFolderStructure();
                }
            });
        };
    }

    private void DrawFile(string guid, int indentLevel, VirtualFolder parentFolder)
    {
        if (!guidToPath.ContainsKey(guid)) return;

        // 绘制插入线提示（在文件之前）
        if (insertBeforeFileGuid == guid && insertParentFolder == parentFolder && !insertAfter)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(indentLevel * 20);
            Rect insertLineRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.Height(3));
            EditorGUI.DrawRect(insertLineRect, new Color(0.3f, 0.6f, 1f, 0.8f)); // 蓝色插入线
            EditorGUILayout.EndHorizontal();
        }

        string filePath = guidToPath[guid];
        string fileName = Path.GetFileNameWithoutExtension(filePath);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(indentLevel * 20);

        Rect rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.Height(20));

        if (Event.current.type == EventType.Repaint)
        {
            GUI.Box(rect, "", "box");
        }

        Rect labelRect = new Rect(rect.x + 5, rect.y + 2, rect.width - 10, rect.height);
        GUI.Label(labelRect, fileName);

        EditorGUILayout.EndHorizontal();

        if (Event.current.type == EventType.MouseDown && labelRect.Contains(Event.current.mousePosition))
        {
            if (Event.current.clickCount == 2)
            {
                OpenInDialogueEditor(filePath);
                Event.current.Use();
            }
        }

        HandleFileDrag(rect, guid, parentFolder);
        HandleFileDropForReorder(rect, guid, parentFolder);

        // 绘制插入线提示（在文件之后）
        if (insertBeforeFileGuid == guid && insertParentFolder == parentFolder && insertAfter)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(indentLevel * 20);
            Rect insertLineRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.Height(3));
            EditorGUI.DrawRect(insertLineRect, new Color(0.3f, 0.6f, 1f, 0.8f)); // 蓝色插入线
            EditorGUILayout.EndHorizontal();
        }
    }

    private void RenameFolder(VirtualFolder folder)
    {
        EditorApplication.delayCall += () =>
        {
            EditorInputDialog.ShowAsync("Rename Folder", "Enter new folder name:", folder.name, (newName) =>
            {
                if (!string.IsNullOrWhiteSpace(newName))
                {
                    folder.name = newName.Trim();
                    SaveVirtualFolderStructure();
                }
            });
        };
    }

    private void HandleFolderContextMenu(Rect rect, VirtualFolder folder)
    {
        Event e = Event.current;

        if (e.type == EventType.ContextClick && rect.Contains(e.mousePosition))
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("New Folder"), false, () =>
            {
                CreateVirtualFolder(folder);
            });
            menu.ShowAsContext();
            e.Use();
        }
    }

    private void HandleFolderDragForReorder(Rect rect, VirtualFolder folder, VirtualFolder parentFolder)
    {
        Event e = Event.current;

        // 开始拖拽 - default_folder不能作为拖拽源
        if (folder.id != "default_folder" && e.type == EventType.MouseDown && rect.Contains(e.mousePosition) && e.button == 0)
        {
            // 检查是否点击在按钮区域
            float buttonAreaWidth = 130;
            Rect buttonArea = new Rect(rect.xMax - buttonAreaWidth, rect.y, buttonAreaWidth, rect.height);
            if (!buttonArea.Contains(e.mousePosition))
            {
                draggedFolder = folder;
                draggedFolderParent = parentFolder;
                isDraggingForReorder = false;
            }
        }

        // 拖拽中 - default_folder不能作为拖拽源
        if (folder.id != "default_folder" && e.type == EventType.MouseDrag && draggedFolder == folder && !isDraggingForReorder)
        {
            DragAndDrop.PrepareStartDrag();
            DragAndDrop.SetGenericData("ReorderFolder", folder);
            DragAndDrop.StartDrag("Reordering Folder");
            isDraggingForReorder = true;
            e.Use();
        }

        // 拖拽到目标位置 - default_folder可以作为拖拽目标
        if (isDraggingForReorder && draggedFolder != null && draggedFolder != folder)
        {
            // 只有在同一个父级下才能排序
            if (draggedFolderParent == parentFolder)
            {
                if (e.type == EventType.DragUpdated && rect.Contains(e.mousePosition))
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Move;

                    // 根据鼠标位置判断插入到前面还是后面
                    float mouseY = e.mousePosition.y;
                    float rectMiddle = rect.y + rect.height / 2;
                    bool shouldInsertAfter = mouseY > rectMiddle;

                    // 更新插入位置提示
                    insertBeforeFolder = folder;
                    insertBeforeFileGuid = null;
                    insertParentFolder = parentFolder;
                    insertAfter = shouldInsertAfter;
                    Repaint();

                    e.Use();
                }
                else if (e.type == EventType.DragPerform && rect.Contains(e.mousePosition))
                {
                    // 根据鼠标位置判断插入到前面还是后面
                    float mouseY = e.mousePosition.y;
                    float rectMiddle = rect.y + rect.height / 2;
                    bool shouldInsertAfter = mouseY > rectMiddle;

                    ReorderFolder(draggedFolder, folder, parentFolder, shouldInsertAfter);
                    DragAndDrop.AcceptDrag();
                    draggedFolder = null;
                    draggedFolderParent = null;
                    isDraggingForReorder = false;

                    // 清除插入位置提示
                    insertBeforeFolder = null;
                    insertBeforeFileGuid = null;
                    insertParentFolder = null;
                    insertAfter = false;

                    e.Use();
                }
            }
        }

        // 拖拽结束
        if (e.type == EventType.DragExited || e.type == EventType.MouseUp)
        {
            if (draggedFolder != null)
            {
                draggedFolder = null;
                draggedFolderParent = null;
                isDraggingForReorder = false;

                // 清除插入位置提示
                insertBeforeFolder = null;
                insertBeforeFileGuid = null;
                insertParentFolder = null;
                insertAfter = false;
                Repaint();
            }
        }
    }

    private void ReorderFolder(VirtualFolder sourceFolder, VirtualFolder targetFolder, VirtualFolder parentFolder, bool insertAfter)
    {
        List<VirtualFolder> list = parentFolder == null ? folderData.rootFolders : parentFolder.subfolders;

        int sourceIndex = list.IndexOf(sourceFolder);
        int targetIndex = list.IndexOf(targetFolder);

        if (sourceIndex != -1 && targetIndex != -1 && sourceIndex != targetIndex)
        {
            list.RemoveAt(sourceIndex);

            // 重新获取目标索引（因为移除可能改变了索引）
            targetIndex = list.IndexOf(targetFolder);

            // 如果要插入到后面，索引+1
            if (insertAfter)
            {
                targetIndex++;
            }

            list.Insert(targetIndex, sourceFolder);
            SaveVirtualFolderStructure();
        }
    }

    private void HandleFileDrag(Rect rect, string guid, VirtualFolder parentFolder)
    {
        Event e = Event.current;

        if (e.type == EventType.MouseDown && rect.Contains(e.mousePosition))
        {
            draggedFileGuid = guid;
            draggedFromFolder = parentFolder;
        }

        if (e.type == EventType.MouseDrag && rect.Contains(e.mousePosition) && draggedFileGuid == guid)
        {
            DragAndDrop.PrepareStartDrag();
            DragAndDrop.StartDrag("DraggingFile");
            e.Use();
        }
    }

    private void HandleFileDropForReorder(Rect rect, string guid, VirtualFolder parentFolder)
    {
        if (string.IsNullOrEmpty(draggedFileGuid) || draggedFileGuid == guid) return;

        // 只有在同一个文件夹内才能排序
        if (draggedFromFolder != parentFolder) return;

        Event e = Event.current;

        if (rect.Contains(e.mousePosition))
        {
            if (e.type == EventType.DragUpdated)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Move;

                // 根据鼠标位置判断插入到前面还是后面
                float mouseY = e.mousePosition.y;
                float rectMiddle = rect.y + rect.height / 2;
                bool shouldInsertAfter = mouseY > rectMiddle;

                // 更新插入位置提示
                insertBeforeFileGuid = guid;
                insertBeforeFolder = null;
                insertParentFolder = parentFolder;
                insertAfter = shouldInsertAfter;
                Repaint();

                e.Use();
            }
            else if (e.type == EventType.DragPerform)
            {
                // 根据鼠标位置判断插入到前面还是后面
                float mouseY = e.mousePosition.y;
                float rectMiddle = rect.y + rect.height / 2;
                bool shouldInsertAfter = mouseY > rectMiddle;

                ReorderFile(draggedFileGuid, guid, parentFolder, shouldInsertAfter);
                DragAndDrop.AcceptDrag();
                draggedFileGuid = null;
                draggedFromFolder = null;

                // 清除插入位置提示
                insertBeforeFileGuid = null;
                insertBeforeFolder = null;
                insertParentFolder = null;
                insertAfter = false;

                e.Use();
            }
        }
    }

    private void ReorderFile(string sourceGuid, string targetGuid, VirtualFolder parentFolder, bool insertAfter)
    {
        List<string> list = parentFolder == null ? folderData.rootFileGuids : parentFolder.fileGuids;

        int sourceIndex = list.IndexOf(sourceGuid);
        int targetIndex = list.IndexOf(targetGuid);

        if (sourceIndex != -1 && targetIndex != -1 && sourceIndex != targetIndex)
        {
            list.RemoveAt(sourceIndex);

            // 重新获取目标索引（因为移除可能改变了索引）
            targetIndex = list.IndexOf(targetGuid);

            // 如果要插入到后面，索引+1
            if (insertAfter)
            {
                targetIndex++;
            }

            list.Insert(targetIndex, sourceGuid);
            SaveVirtualFolderStructure();
        }
    }

    private void HandleFolderDragAndDrop(Rect rect, VirtualFolder folder)
    {
        Event e = Event.current;

        if (rect.Contains(e.mousePosition))
        {
            if (e.type == EventType.DragUpdated)
            {
                // 只处理文件拖拽到文件夹，不处理文件夹排序
                if (!string.IsNullOrEmpty(draggedFileGuid) && !isDraggingForReorder)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Move;
                    e.Use();
                }
            }
            else if (e.type == EventType.DragPerform)
            {
                // 只处理文件拖拽到文件夹，不处理文件夹排序
                if (!string.IsNullOrEmpty(draggedFileGuid) && !isDraggingForReorder)
                {
                    MoveFileToFolder(draggedFileGuid, draggedFromFolder, folder);
                    DragAndDrop.AcceptDrag();
                    draggedFileGuid = null;
                    draggedFromFolder = null;

                    // 清除插入位置提示
                    insertBeforeFileGuid = null;
                    insertBeforeFolder = null;
                    insertParentFolder = null;
                    insertAfter = false;

                    e.Use();
                }
            }
        }

        if (e.type == EventType.DragExited)
        {
            draggedFileGuid = null;
        }
    }

    private void MoveFileToFolder(string fileGuid, VirtualFolder fromFolder, VirtualFolder toFolder)
    {
        if (fromFolder == null)
        {
            folderData.rootFileGuids.Remove(fileGuid);
        }
        else
        {
            fromFolder.fileGuids.Remove(fileGuid);
        }

        if (!toFolder.fileGuids.Contains(fileGuid))
        {
            toFolder.fileGuids.Add(fileGuid);
        }

        SaveVirtualFolderStructure();
    }

    private void CreateVirtualFolder(VirtualFolder parent)
    {
        string folderName = "New Folder";
        int counter = 1;

        var existingNames = parent == null
            ? folderData.rootFolders.Select(f => f.name).ToList()
            : parent.subfolders.Select(f => f.name).ToList();

        string finalName = folderName;
        while (existingNames.Contains(finalName))
        {
            finalName = $"{folderName} {counter}";
            counter++;
        }

        var newFolder = new VirtualFolder
        {
            name = finalName,
            id = System.Guid.NewGuid().ToString()
        };

        if (parent == null)
        {
            folderData.rootFolders.Add(newFolder);
        }
        else
        {
            parent.subfolders.Add(newFolder);
        }

        SaveVirtualFolderStructure();
    }

    private void DeleteVirtualFolder(VirtualFolder folder, VirtualFolder parent)
    {
        List<string> allFiles = new List<string>();
        CollectAllFilesRecursive(folder, allFiles);

        VirtualFolder defaultFolder = GetDefaultFolder();
        if (defaultFolder != null && defaultFolder != folder)
        {
            foreach (var fileGuid in allFiles)
            {
                if (!defaultFolder.fileGuids.Contains(fileGuid))
                {
                    defaultFolder.fileGuids.Add(fileGuid);
                }
            }
        }

        if (parent == null)
        {
            folderData.rootFolders.Remove(folder);
        }
        else
        {
            parent.subfolders.Remove(folder);
        }

        SaveVirtualFolderStructure();
    }

    private void CreateNewDialogueFile(VirtualFolder folder)
    {
        // 弹出保存对话框
        string defaultPath = Path.Combine(Application.dataPath, "StreamingAssets");
        string savePath = EditorUtility.SaveFilePanel(
            "Create New Dialogue Tree",
            defaultPath,
            "NewDialogue",
            "dtree"
        );

        if (string.IsNullOrEmpty(savePath))
            return;

        try
        {
            // 创建空的对话树数据
            DialogueTreeData emptyTree = new DialogueTreeData();

            // 创建一个初始节点
            var startNode = new DialogueNodeData
            {
                id = System.Guid.NewGuid().ToString(),
                index = 0,
                characterId = "",
                content = "Start dialogue here...",
                positionX = 100,
                positionY = 100,
                choices = new List<ChoiceData>(),
                eventCalls = new List<DialogueEventCall>(),
                conditionalBranches = new List<ConditionalBranchData>()
            };

            emptyTree.nodes.Add(startNode);

            // 保存 .dtree 文件
            string json = JsonUtility.ToJson(emptyTree, true);
            File.WriteAllText(savePath, json);

            // 创建对应的 .json 运行时文件
            string jsonPath = Path.ChangeExtension(savePath, ".json");
            string runtimeJson = "{\n  \"conversations\": [\n    {\n      \"index\": 0,\n      \"name\": \"\",\n      \"avatarAddr\": \"\",\n      \"content\": \"Start dialogue here...\",\n      \"nextIndex\": -1,\n      \"choices\": [],\n      \"eventCalls\": [],\n      \"conditionalBranches\": []\n    }\n  ],\n  \"currentIndex\": 0\n}";
            File.WriteAllText(jsonPath, runtimeJson);

            // 刷新资源
            AssetDatabase.Refresh();

            // 重新扫描文件
            ScanAllDialogueTrees();

            Debug.Log($"Created new dialogue tree: {savePath}");

            // 打开编辑器
            EditorApplication.delayCall += () =>
            {
                OpenInDialogueEditor(savePath);
            };
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to create dialogue tree: {e.Message}");
            EditorUtility.DisplayDialog("Creation Failed",
                $"Failed to create dialogue tree:\n{e.Message}",
                "OK");
        }
    }

    private void CollectAllFilesRecursive(VirtualFolder folder, List<string> fileList)
    {
        fileList.AddRange(folder.fileGuids);

        foreach (var subfolder in folder.subfolders)
        {
            CollectAllFilesRecursive(subfolder, fileList);
        }
    }

    #endregion

    #region Scanning and Loading

    private void ScanAllDialogueTrees()
    {
        guidToPath.Clear();

        string[] allFiles = Directory.GetFiles(Application.dataPath, "*.dtree", SearchOption.AllDirectories);

        foreach (var file in allFiles)
        {
            string guid = AssetDatabase.AssetPathToGUID("Assets" + file.Substring(Application.dataPath.Length));
            if (string.IsNullOrEmpty(guid))
            {
                guid = file.GetHashCode().ToString();
            }
            guidToPath[guid] = file;
        }

        CleanupDeletedFiles();
        EnsureDefaultFolder();

        VirtualFolder defaultFolder = GetDefaultFolder();
        foreach (var guid in guidToPath.Keys)
        {
            if (!IsFileInAnyFolder(guid))
            {
                defaultFolder.fileGuids.Add(guid);
            }
        }

        SaveVirtualFolderStructure();
        Debug.Log($"Found {guidToPath.Count} dialogue tree files");
    }

    private void EnsureDefaultFolder()
    {
        if (folderData.rootFolders.Count == 0 ||
            !folderData.rootFolders.Any(f => f.id == "default_folder"))
        {
            var defaultFolder = new VirtualFolder
            {
                name = "All Dialogues",
                id = "default_folder",
                isExpanded = true
            };

            defaultFolder.fileGuids.AddRange(folderData.rootFileGuids);
            folderData.rootFileGuids.Clear();

            folderData.rootFolders.Insert(0, defaultFolder);
        }
    }

    private VirtualFolder GetDefaultFolder()
    {
        return folderData.rootFolders.FirstOrDefault(f => f.id == "default_folder");
    }

    private bool IsFileInAnyFolder(string guid)
    {
        if (folderData.rootFileGuids.Contains(guid)) return true;
        return CheckFolderRecursive(folderData.rootFolders, guid);
    }

    private bool CheckFolderRecursive(List<VirtualFolder> folders, string guid)
    {
        foreach (var folder in folders)
        {
            if (folder.fileGuids.Contains(guid)) return true;
            if (CheckFolderRecursive(folder.subfolders, guid)) return true;
        }
        return false;
    }

    private void CleanupDeletedFiles()
    {
        var validGuids = new HashSet<string>(guidToPath.Keys);

        folderData.rootFileGuids.RemoveAll(g => !validGuids.Contains(g));
        CleanupFoldersRecursive(folderData.rootFolders, validGuids);
    }

    private void CleanupFoldersRecursive(List<VirtualFolder> folders, HashSet<string> validGuids)
    {
        foreach (var folder in folders)
        {
            folder.fileGuids.RemoveAll(g => !validGuids.Contains(g));
            CleanupFoldersRecursive(folder.subfolders, validGuids);
        }
    }

    private void SaveVirtualFolderStructure()
    {
        try
        {
            string savePath = GetFolderStructurePath();
            string json = JsonUtility.ToJson(folderData, true);

            string folder = Path.GetDirectoryName(savePath);
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            File.WriteAllText(savePath, json);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to save folder structure: {e.Message}");
        }
    }

    private void LoadVirtualFolderStructure()
    {
        try
        {
            string loadPath = GetFolderStructurePath();

            if (File.Exists(loadPath))
            {
                string json = File.ReadAllText(loadPath);
                folderData = JsonUtility.FromJson<VirtualFolderData>(json);
            }
            else
            {
                folderData = new VirtualFolderData();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to load folder structure: {e.Message}");
            folderData = new VirtualFolderData();
        }
    }

    private void OpenInDialogueEditor(string dtreePath)
    {
        DialogueTreeEditor.OpenWindow();

        EditorApplication.delayCall += () =>
        {
            var window = GetWindow<DialogueTreeEditor>();
            if (window != null)
            {
                window.ForceInitialize();

                EditorApplication.delayCall += () =>
                {
                    window.LoadFromFile(dtreePath);
                };
            }
        };
    }

    #endregion

    #region Export

    private void ExportAllToCSV()
    {
        string savePath = EditorUtility.SaveFilePanel("Export All Dialogues to CSV",
            Application.dataPath, "AllDialogues_export.csv", "csv");

        if (string.IsNullOrEmpty(savePath)) return;

        try
        {
            using (StreamWriter writer = new StreamWriter(savePath, false, System.Text.Encoding.UTF8))
            {
                writer.WriteLine("=== ALL DIALOGUE TREES ===");
                writer.WriteLine("Note: This export is for viewing only.");
                writer.WriteLine("");

                bool isFirst = true;

                foreach (var folder in folderData.rootFolders)
                {
                    if (folder.id == "default_folder")
                    {
                        foreach (var subfolder in folder.subfolders)
                        {
                            if (!isFirst)
                            {
                                writer.WriteLine("");
                                writer.WriteLine("---");
                                writer.WriteLine("");
                            }
                            isFirst = false;

                            ExportFolderRecursive(writer, subfolder, "", 0);
                        }

                        foreach (var guid in folder.fileGuids)
                        {
                            if (guidToPath.ContainsKey(guid))
                            {
                                if (!isFirst)
                                {
                                    writer.WriteLine("");
                                }
                                ExportSingleFileToWriter(writer, guidToPath[guid], 0);
                                isFirst = false;
                            }
                        }
                    }
                    else
                    {
                        if (!isFirst)
                        {
                            writer.WriteLine("");
                            writer.WriteLine("---");
                            writer.WriteLine("");
                        }
                        isFirst = false;

                        ExportFolderRecursive(writer, folder, "", 0);
                    }
                }
            }

            EditorUtility.DisplayDialog("Export Successful",
                $"Exported all dialogues to:\n{savePath}", "OK");
        }
        catch (System.Exception e)
        {
            EditorUtility.DisplayDialog("Export Failed", $"Error: {e.Message}", "OK");
        }
    }

    private void ExportFolderRecursive(StreamWriter writer, VirtualFolder folder, string parentPath, int indentLevel)
    {
        string folderPath = string.IsNullOrEmpty(parentPath) ? folder.name : $"{parentPath}/{folder.name}";
        string indent = new string(',', indentLevel);

        writer.WriteLine($"{indent}FOLDER: {folderPath}");

        if (!string.IsNullOrEmpty(folder.description))
        {
            writer.WriteLine($"{indent}Description: {EscapeCSV(folder.description)}");
        }

        writer.WriteLine();

        foreach (var guid in folder.fileGuids)
        {
            if (guidToPath.ContainsKey(guid))
            {
                ExportSingleFileToWriter(writer, guidToPath[guid], indentLevel + 1);
            }
        }

        foreach (var subfolder in folder.subfolders)
        {
            writer.WriteLine();
            ExportFolderRecursive(writer, subfolder, folderPath, indentLevel + 1);
        }

        writer.WriteLine();
    }

    private void ExportSingleFileToWriter(StreamWriter writer, string filePath, int indentLevel)
    {
        var treeData = LoadDialogueTreeData(filePath);
        if (treeData == null) return;

        string indent = new string(',', indentLevel);

        writer.WriteLine($"{indent}FILE: {Path.GetFileNameWithoutExtension(filePath)}");

        WriteCSVData(writer, treeData, indentLevel);
        writer.WriteLine();
    }

    private void WriteCSVData(StreamWriter writer, DialogueTreeData treeData, int indentLevel)
    {
        int maxChoices = treeData.nodes.Max(n => n.choices != null ? n.choices.Count : 0);

        var connectionMap = new Dictionary<string, int>();
        foreach (var conn in treeData.connections)
        {
            string key = $"{conn.outputNodeId}_{conn.choiceIndex}";
            var targetNode = treeData.nodes.FirstOrDefault(n => n.id == conn.inputNodeId);
            if (targetNode != null)
            {
                connectionMap[key] = targetNode.index;
            }
        }

        string indent = new string(',', indentLevel);

        writer.Write($"{indent}NodeIndex,CharacterName,DialogueContent");
        for (int i = 0; i < maxChoices; i++)
        {
            writer.Write($",Choice{i + 1},GoToNode{i + 1}");
        }
        writer.WriteLine();

        foreach (var node in treeData.nodes.OrderBy(n => n.index))
        {
            writer.Write(indent);
            writer.Write($"{node.index},");

            string characterName = GetCharacterNameById(node.characterId);
            writer.Write($"\"{EscapeCSV(characterName)}\",");

            writer.Write($"\"{EscapeCSV(node.content)}\"");

            for (int i = 0; i < maxChoices; i++)
            {
                if (node.choices != null && i < node.choices.Count)
                {
                    writer.Write($",\"{EscapeCSV(node.choices[i].text)}\"");

                    string key = $"{node.id}_{i}";
                    if (connectionMap.ContainsKey(key))
                    {
                        writer.Write($",{connectionMap[key]}");
                    }
                    else
                    {
                        writer.Write(",");
                    }
                }
                else
                {
                    writer.Write(",,");
                }
            }

            writer.WriteLine();
        }

        bool hasAnyConditions = treeData.nodes.Any(n =>
            n.choices != null && n.choices.Any(c => c.conditions != null && c.conditions.Count > 0));

        if (hasAnyConditions)
        {
            writer.WriteLine();
            writer.WriteLine($"{indent}CHOICE CONDITIONS:");
            writer.WriteLine($"{indent}NodeIndex,ChoiceText,ConditionLogic,Conditions");

            foreach (var node in treeData.nodes.OrderBy(n => n.index))
            {
                if (node.choices != null)
                {
                    for (int i = 0; i < node.choices.Count; i++)
                    {
                        var choice = node.choices[i];
                        if (choice.conditions != null && choice.conditions.Count > 0)
                        {
                            writer.Write($"{indent}{node.index},");
                            writer.Write($"\"{EscapeCSV(choice.text)}\",");
                            writer.Write($"{choice.conditionLogic},");
                            writer.Write("\"");

                            for (int j = 0; j < choice.conditions.Count; j++)
                            {
                                var cond = choice.conditions[j];
                                writer.Write($"[{cond.targetObjectName}.{cond.componentTypeName}.{cond.variableName} {GetComparisonSymbol(cond.comparison)} {cond.compareValue}]");

                                if (j < choice.conditions.Count - 1)
                                {
                                    writer.Write($" {choice.conditionLogic} ");
                                }
                            }

                            writer.WriteLine("\"");
                        }
                    }
                }
            }
        }
    }

    private DialogueTreeData LoadDialogueTreeData(string path)
    {
        try
        {
            string json = File.ReadAllText(path);
            return JsonUtility.FromJson<DialogueTreeData>(json);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to load dialogue tree from {path}: {e.Message}");
            return null;
        }
    }

    #endregion

    #region Helper Methods

    private string GetFolderStructurePath()
    {
        var script = MonoScript.FromScriptableObject(this);
        string scriptPath = AssetDatabase.GetAssetPath(script);

        if (string.IsNullOrEmpty(scriptPath))
        {
            return "Assets/Editor/Dialogue/DialogueTreeFolderStructure.json";
        }

        string scriptFolder = Path.GetDirectoryName(scriptPath);
        return Path.Combine(scriptFolder, "DialogueTreeFolderStructure.json");
    }

    private string GetCharacterLibraryPath()
    {
        var script = MonoScript.FromScriptableObject(this);
        string scriptPath = AssetDatabase.GetAssetPath(script);

        if (string.IsNullOrEmpty(scriptPath))
        {
            return "Assets/Editor/Dialogue/CharacterLibrary.json";
        }

        string scriptFolder = Path.GetDirectoryName(scriptPath);
        return Path.Combine(scriptFolder, "CharacterLibrary.json");
    }

    private string GetComparisonSymbol(ComparisonType comparison)
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

    private string EscapeCSV(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text.Replace("\"", "\"\"");
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

    private string GetCharacterNameById(string characterId)
    {
        if (string.IsNullOrEmpty(characterId))
            return "No Character";

        if (characterLibrary?.characters != null)
        {
            var character = System.Array.Find(characterLibrary.characters, c => c.id == characterId);
            if (character != null)
                return character.characterName;
        }

        return "Unknown Character";
    }

    #endregion
}