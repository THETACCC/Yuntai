using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DialogueSystem;
using System;

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
        public List<string> fileGuids = new List<string>();
        public List<VirtualFolder> subfolders = new List<VirtualFolder>();
    }

    [System.Serializable]
    private class VirtualFolderData
    {
        public List<VirtualFolder> rootFolders = new List<VirtualFolder>();
        public List<string> rootFileGuids = new List<string>();
    }

    [System.Serializable]
    private class CharacterFolder
    {
        public string name;
        public string id;
        public string description = "";
        public List<string> characterIds = new List<string>();
        public List<CharacterFolder> subfolders = new List<CharacterFolder>();
    }

    [System.Serializable]
    private class CharacterFolderData
    {
        public List<CharacterFolder> rootFolders = new List<CharacterFolder>();
        public List<string> rootCharacterIds = new List<string>();
    }

    private VirtualFolderData folderData;
    private CharacterLibraryData characterLibrary;
    private CharacterFolderData characterFolderData;
    private Dictionary<string, bool> characterFolderExpandedState = new Dictionary<string, bool>(); // 临时存储角色文件夹展开状态
    private Dictionary<string, string> guidToPath = new Dictionary<string, string>();
    private Dictionary<string, bool> folderExpandedState = new Dictionary<string, bool>(); // 临时存储展开状态，不保存到文件
    private Vector2 scrollPos;
    private VirtualFolder draggedFromFolder;
    private string draggedFileGuid;
    private CharacterData draggedCharacter;
    private bool charactersExpanded = true;
    private string editingCharacterId = "";  // 正在编辑的角色ID
    private Dictionary<string, Sprite> tempSelectedSprites = new Dictionary<string, Sprite>();  // 临时选择的sprite

    // 文件监听相关
    private System.DateTime lastCharacterLibraryTime;
    private System.DateTime lastFolderStructureTime;
    private double nextCheckTime = 0;
    private const double CHECK_INTERVAL = 2.0;  // 每2秒检查一次

    // 缓存的背景texture，避免每帧创建导致GUI混乱
    private Texture2D cachedNormalBg;
    private Texture2D cachedFocusedBg;

    // 拖拽排序相关
    private VirtualFolder draggedFolder;
    private VirtualFolder draggedFolderParent;
    private bool isDraggingForReorder = false;

    // 插入位置提示
    private VirtualFolder insertBeforeFolder = null;  // 在哪个文件夹之前/后插入
    private string insertBeforeFileGuid = null;       // 在哪个文件之前/后插入
    private VirtualFolder insertParentFolder = null;  // 插入的父文件夹
    private bool insertAfter = false;                 // true=插入到目标后面, false=插入到目标前面

    // 角色拖拽排序相关
    private CharacterData draggedCharacterForReorder = null;
    private CharacterFolder draggedCharacterFromFolder = null;
    private bool isDraggingCharacterForReorder = false;
    private string insertBeforeCharacterId = null;  // 在哪个角色之前/后插入

    private CharacterFolder insertBeforeCharacterFolder = null;  // 在哪个角色文件夹之前/后插入
    private CharacterFolder insertCharacterParentFolder = null;  // 角色插入的父文件夹

    // 本地化相关
    private bool localizationSettingsExpanded = false;
    private string csvUrlInput = "";
    private Vector2 csvUrlScrollPos;
    private Dictionary<string, Dictionary<Language, string>> previousLocalizationData =
        new Dictionary<string, Dictionary<Language, string>>();
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
        LoadCharacterFolderStructure();
        LoadLocalizationSettings();
        ScanAllDialogueTrees();

        // 确保default_folder默认展开
        if (!folderExpandedState.ContainsKey("default_folder"))
            folderExpandedState["default_folder"] = true;

        // 确保角色默认文件夹展开
        if (!characterFolderExpandedState.ContainsKey("default_character_folder"))
            characterFolderExpandedState["default_character_folder"] = true;

        // 记录文件修改时间
        UpdateFileModificationTimes();

        // 注册定期检查回调
        EditorApplication.update += CheckFileChanges;

        if (cachedFocusedBg == null)
            cachedFocusedBg = MakeTex(2, 2, new Color(0.3f, 0.3f, 0.3f, 1f));

        // 初始化本地化
        if (!DialogueLocalization.IsLoaded && !string.IsNullOrEmpty(csvUrlInput))
        {
            DialogueLocalization.LoadInEditorSync();
        }
    }

    private void OnDisable()
    {
        // 取消注册定期检查回调
        EditorApplication.update -= CheckFileChanges;

        // SaveVirtualFolderStructure(); // 移除自动保存，只在实际修改时保存
        // SaveCharacterLibraryInternal(); // 移除自动保存，只在实际修改时保存
        // SaveCharacterFolderStructure(); // 移除自动保存，只在实际修改时保存
    }

    private void OnGUI()
    {
        DrawToolbar();
        DrawLocalizationSettings();
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

            // 清除角色拖拽排序状态
            draggedCharacterForReorder = null;
            isDraggingCharacterForReorder = false;
            insertBeforeCharacterId = null;
            draggedCharacterFromFolder = null;
            insertBeforeCharacterFolder = null;
            insertCharacterParentFolder = null;

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
            RefreshAll();
        }

        var exportContent = new GUIContent("Export", "Export all dialogues to CSV");
        if (GUILayout.Button(exportContent, EditorStyles.toolbarButton, GUILayout.Width(60)))
        {
            ExportAllToCSV();
        }

        // === 本地化导出按钮 ===
        var locExportContent = new GUIContent("Loc Export", "Export localization texts to CSV");
        if (GUILayout.Button(locExportContent, EditorStyles.toolbarButton, GUILayout.Width(80)))
        {
            ExportLocalizationToCSV();
        }

        // === 本地化导入按钮 ===
        var locImportContent = new GUIContent("Loc Import", "Import localization texts from CSV");
        if (GUILayout.Button(locImportContent, EditorStyles.toolbarButton, GUILayout.Width(80)))
        {
            ImportLocalizationFromCSV();
        }
        // === 本地化按钮结束 ===

        // === Save All 按钮 ===
        var saveAllContent = new GUIContent("Save All", "Save all dialogue trees");
        if (GUILayout.Button(saveAllContent, EditorStyles.toolbarButton, GUILayout.Width(70)))
        {
            SaveAllDialogueTrees();
        }
        // === Save All 按钮结束 ===

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

        // 添加右键菜单支持
        HandleCharacterSectionContextMenu(rect);

        if (charactersExpanded)
        {
            // 获取default_character_folder并直接展开其内容
            var defaultFolder = characterFolderData.rootFolders.FirstOrDefault(f => f.id == "default_character_folder");
            if (defaultFolder != null)
            {
                // 绘制其他自定义文件夹
                foreach (var folder in characterFolderData.rootFolders)
                {
                    if (folder.id != "default_character_folder")
                    {
                        DrawCharacterFolder(folder, 0, null);
                    }
                }

                // 绘制default_character_folder中的子文件夹
                foreach (var subfolder in defaultFolder.subfolders.ToList())
                {
                    DrawCharacterFolder(subfolder, 0, defaultFolder);
                }

                // 绘制default_character_folder中的角色
                foreach (var characterId in defaultFolder.characterIds.ToList())
                {
                    var character = System.Array.Find(characterLibrary.characters, c => c.id == characterId);
                    if (character != null)
                    {
                        int index = System.Array.IndexOf(characterLibrary.characters, character);
                        DrawCharacter(character, index, defaultFolder);
                    }
                }
            }

            // 绘制根级别的角色（如果有的话）
            foreach (var characterId in characterFolderData.rootCharacterIds.ToList())
            {
                var character = System.Array.Find(characterLibrary.characters, c => c.id == characterId);
                if (character != null)
                {
                    int index = System.Array.IndexOf(characterLibrary.characters, character);
                    DrawCharacter(character, index, null);
                }
            }
        }
    }

    private void HandleCharacterSectionContextMenu(Rect rect)
    {
        Event e = Event.current;

        if (e.type == EventType.ContextClick && rect.Contains(e.mousePosition))
        {
            var defaultFolder = characterFolderData.rootFolders.FirstOrDefault(f => f.id == "default_character_folder");
            if (defaultFolder != null)
            {
                GenericMenu menu = new GenericMenu();
                menu.AddItem(new GUIContent("New Folder"), false, () =>
                {
                    CreateCharacterFolder(defaultFolder);
                });
                menu.ShowAsContext();
                e.Use();
            }
        }
    }
    private void DrawCharacterFolder(CharacterFolder folder, int indentLevel, CharacterFolder parentFolder)
    {
        // 绘制插入线提示（在文件夹之前）
        if (insertBeforeCharacterFolder == folder && insertCharacterParentFolder == parentFolder && !insertAfter)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(indentLevel * 20 + 25);
            Rect insertLineRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.Height(3));
            EditorGUI.DrawRect(insertLineRect, new Color(0.3f, 0.6f, 1f, 0.8f)); // 蓝色插入线
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.BeginVertical();
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(indentLevel * 20 + 25);

        Rect rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.Height(22));

        if (Event.current.type == EventType.Repaint)
        {
            GUI.Box(rect, "", "box");
        }

        Rect arrowRect = new Rect(rect.x + 5, rect.y + 3, 15, rect.height);
        bool isExpanded = characterFolderExpandedState.ContainsKey(folder.id) && characterFolderExpandedState[folder.id];
        if (GUI.Button(arrowRect, isExpanded ? "▼" : "▶", EditorStyles.label))
        {
            characterFolderExpandedState[folder.id] = !isExpanded;
        }

        Rect labelRect = new Rect(rect.x + 25, rect.y + 3, rect.width - 200, rect.height);
        GUI.Label(labelRect, folder.name, EditorStyles.boldLabel);

        if (folder.id != "default_character_folder")
        {
            Rect renameRect = new Rect(rect.xMax - 130, rect.y + 2, 60, 18);
            if (GUI.Button(renameRect, "Rename", EditorStyles.miniButton))
            {
                RenameCharacterFolder(folder);
            }

            Rect deleteRect = new Rect(rect.xMax - 65, rect.y + 2, 60, 18);
            GUI.backgroundColor = new Color(1f, 0.7f, 0.7f);
            if (GUI.Button(deleteRect, "Del", EditorStyles.miniButton))
            {
                string folderName = folder.name;
                EditorApplication.delayCall += () =>
                {
                    if (EditorUtility.DisplayDialog("Delete Folder",
                        $"Delete folder '{folderName}'? All characters will move to 'All Characters' folder.",
                        "Delete", "Cancel"))
                    {
                        DeleteCharacterFolder(folder, parentFolder);
                    }
                };
            }
            GUI.backgroundColor = Color.white;
        }
        else
        {
            // All Characters 文件夹的 New 按钮
            Rect newCharRect = new Rect(rect.xMax - 135, rect.y + 2, 130, 18);
            if (GUI.Button(newCharRect, "+ New Character", EditorStyles.miniButton))
            {
                CreateNewCharacter();
            }
        }

        EditorGUILayout.EndHorizontal();

        // 显示description
        if (!string.IsNullOrEmpty(folder.description))
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(indentLevel * 20 + 50);

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
                    EditCharacterDescription(folder);
                    Event.current.Use();
                }
            }

            GUI.Label(descRect, folder.description, descStyle);
            EditorGUILayout.EndHorizontal();
        }
        else if (folder.id != "default_character_folder")
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(indentLevel * 20 + 50);

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
                EditCharacterDescription(folder);
            }

            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndVertical();
        HandleCharacterFolderDragAndDrop(rect, folder);
        HandleCharacterFolderContextMenu(rect, folder);

        bool isExpandedForChildren = characterFolderExpandedState.ContainsKey(folder.id) && characterFolderExpandedState[folder.id];
        if (isExpandedForChildren)
        {
            foreach (var subfolder in folder.subfolders.ToList())
            {
                DrawCharacterFolder(subfolder, indentLevel + 1, folder);
            }

            foreach (var characterId in folder.characterIds.ToList())
            {
                var character = System.Array.Find(characterLibrary.characters, c => c.id == characterId);
                if (character != null)
                {
                    int index = System.Array.IndexOf(characterLibrary.characters, character);
                    DrawCharacter(character, index, folder);
                }
            }
        }

        // 绘制插入线提示（在文件夹之后）
        if (insertBeforeCharacterFolder == folder && insertCharacterParentFolder == parentFolder && insertAfter)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(indentLevel * 20 + 25);
            Rect insertLineRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.Height(3));
            EditorGUI.DrawRect(insertLineRect, new Color(0.3f, 0.6f, 1f, 0.8f)); // 蓝色插入线
            EditorGUILayout.EndHorizontal();
        }
    }

    private void DrawCharacter(CharacterData character, int index, CharacterFolder parentFolder)
    {
        bool isEditing = editingCharacterId == character.id;

        // 计算缩进级别
        int indentLevel = 0;
        if (parentFolder != null && parentFolder.id != "default_character_folder")
        {
            indentLevel = CalculateCharacterFolderIndent(parentFolder);
        }

        // 绘制插入线提示（在角色之前）
        if (insertBeforeCharacterId == character.id && !insertAfter)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(indentLevel * 20 + 25);
            Rect insertLineRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.Height(3));
            EditorGUI.DrawRect(insertLineRect, new Color(0.3f, 0.6f, 1f, 0.8f)); // 蓝色插入线
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.BeginVertical();
        GUILayout.Space(2);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(indentLevel * 20 + 25);
        // 使用box样式作为背景
        var boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.padding = new RectOffset(8, 8, 8, 8);

        EditorGUILayout.BeginVertical(boxStyle, GUILayout.MinHeight(isEditing ? 230 : 70));

        // 第一行：显示名称和按钮
        EditorGUILayout.BeginHorizontal();

        if (isEditing)
        {
            // 编辑模式：显示 character 字段
            EditorGUILayout.LabelField("Character:", GUILayout.Width(70));

            // 为Character字段创建独立的样式和texture（避免与Name字段共享）
            var characterFieldStyle = new GUIStyle(EditorStyles.textField);
            characterFieldStyle.normal.background = cachedNormalBg;
            characterFieldStyle.focused.background = cachedFocusedBg;
            characterFieldStyle.normal.textColor = Color.white;
            characterFieldStyle.focused.textColor = Color.white;

            string newCharacter = EditorGUILayout.TextField(character.character, characterFieldStyle);

            if (newCharacter != character.character)
            {
                character.character = newCharacter;
            }
        }
        else
        {
            // 非编辑模式：显示 character 作为主标题
            EditorGUILayout.LabelField(character.character, EditorStyles.boldLabel);
        }

        GUILayout.FlexibleSpace();


        // 非编辑模式：显示Edit和Delete按钮
        if (!isEditing)
        {
            if (GUILayout.Button("Edit", EditorStyles.miniButton, GUILayout.Width(60)))
            {
                editingCharacterId = character.id;
            }

            GUI.backgroundColor = new Color(1f, 0.7f, 0.7f);
            if (GUILayout.Button("Del", EditorStyles.miniButton, GUILayout.Width(60)))
            {
                DeleteCharacter(index);
            }
            GUI.backgroundColor = Color.white;
        }

        EditorGUILayout.EndHorizontal();

        // 编辑模式下显示 Name 字段（多语言）
        if (isEditing)
        {
            EditorGUILayout.Space(3);

            // Name 标题
            EditorGUILayout.LabelField("Name:", EditorStyles.boldLabel);

            // 为Name字段创建独立的样式和texture（避免与Character字段共享）
            var nameFieldStyle = new GUIStyle(EditorStyles.textField);
            nameFieldStyle.normal.background = cachedNormalBg;
            nameFieldStyle.focused.background = cachedFocusedBg;
            nameFieldStyle.normal.textColor = Color.white;
            nameFieldStyle.focused.textColor = Color.white;

            // 模式选择下拉框
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(15);
            EditorGUILayout.LabelField("Input Mode:", GUILayout.Width(80));

            string[] modeOptions = new string[] { "Direct Input", "Use ID" };
            int currentMode = character.useNameId ? 1 : 0;
            int newMode = EditorGUILayout.Popup(currentMode, modeOptions);

            if (newMode != currentMode)
            {
                bool oldUseId = character.useNameId;
                bool newUseId = (newMode == 1);

                // 模式切换时的数据迁移
                if (oldUseId && !newUseId)
                {
                    // 从Use ID切换到Direct Input
                    // 只在characterName为空时才从ID读取内容（避免覆盖用户编辑的内容）
                    bool isEmpty = character.characterName == null ||
                                  (string.IsNullOrEmpty(character.characterName.en) &&
                                   string.IsNullOrEmpty(character.characterName.zh) &&
                                   string.IsNullOrEmpty(character.characterName.ja));

                    if (isEmpty && !string.IsNullOrEmpty(character.nameId) && DialogueLocalization.IsLoaded)
                    {
                        var locData = DialogueLocalization.GetAllLanguages(character.nameId);
                        if (locData != null)
                        {
                            if (character.characterName == null)
                            {
                                character.characterName = new LocalizedText();
                            }
                            // 只在空的时候填充
                            character.characterName.zh = locData.ContainsKey(Language.ChineseSimplified) ? locData[Language.ChineseSimplified] : "";
                            character.characterName.en = locData.ContainsKey(Language.English) ? locData[Language.English] : "";
                            character.characterName.ja = locData.ContainsKey(Language.Japanese) ? locData[Language.Japanese] : "";
                        }
                    }
                }

                character.useNameId = newUseId;
            }

            EditorGUILayout.EndHorizontal();

            if (character.useNameId)
            {
                // Use ID 模式
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(15);
                EditorGUILayout.LabelField("Name ID:", GUILayout.Width(70));

                string currentNameId = character.nameId ?? "";
                string newNameId = EditorGUILayout.TextField(currentNameId, nameFieldStyle);

                if (newNameId != currentNameId)
                {
                    character.nameId = newNameId;

                    // 如果有新ID且数据已加载，从Google Sheets读取内容
                    if (!string.IsNullOrEmpty(newNameId) && DialogueLocalization.IsLoaded)
                    {
                        var locData = DialogueLocalization.GetAllLanguages(newNameId);
                        if (locData != null)
                        {
                            character.characterName = new LocalizedText
                            {
                                zh = locData.ContainsKey(Language.ChineseSimplified) ? locData[Language.ChineseSimplified] : "",
                                en = locData.ContainsKey(Language.English) ? locData[Language.English] : "",
                                ja = locData.ContainsKey(Language.Japanese) ? locData[Language.Japanese] : ""
                            };
                        }
                    }
                }

                EditorGUILayout.EndHorizontal();

                // Name 预览 - 显示三个语言
                if (!string.IsNullOrEmpty(character.nameId))
                {
                    if (DialogueLocalization.IsLoaded && DialogueLocalization.HasId(character.nameId))
                    {
                        // 获取所有语言的文本
                        var locData = DialogueLocalization.GetAllLanguages(character.nameId);
                        if (locData != null)
                        {
                            var previewStyle = new GUIStyle(EditorStyles.label);
                            previewStyle.normal.textColor = new Color(0.7f, 0.9f, 0.7f); // 淡绿色
                            previewStyle.wordWrap = true;

                            // 中文
                            if (locData.ContainsKey(Language.ChineseSimplified))
                            {
                                EditorGUILayout.BeginHorizontal();
                                GUILayout.Space(15);
                                EditorGUILayout.LabelField($"中文: {locData[Language.ChineseSimplified]}", previewStyle);
                                EditorGUILayout.EndHorizontal();
                            }

                            // English
                            if (locData.ContainsKey(Language.English))
                            {
                                EditorGUILayout.BeginHorizontal();
                                GUILayout.Space(15);
                                EditorGUILayout.LabelField($"EN: {locData[Language.English]}", previewStyle);
                                EditorGUILayout.EndHorizontal();
                            }

                            // 日本語
                            if (locData.ContainsKey(Language.Japanese))
                            {
                                EditorGUILayout.BeginHorizontal();
                                GUILayout.Space(15);
                                EditorGUILayout.LabelField($"日本語: {locData[Language.Japanese]}", previewStyle);
                                EditorGUILayout.EndHorizontal();
                            }
                        }
                    }
                    else if (!string.IsNullOrEmpty(character.nameId))
                    {
                        // ID不存在，显示错误
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Space(15);
                        var errorStyle = new GUIStyle(EditorStyles.label);
                        errorStyle.normal.textColor = new Color(1f, 0.5f, 0.5f); // 淡红色
                        EditorGUILayout.LabelField($"[错误: ID '{character.nameId}' 不存在]", errorStyle);
                        EditorGUILayout.EndHorizontal();
                    }
                }
            }
            else
            {
                // Direct Input 模式 - 显示三个语言的输入框
                // 中文
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(15);
                EditorGUILayout.LabelField("中文:", GUILayout.Width(70));

                if (character.characterName == null)
                {
                    character.characterName = new LocalizedText();
                }

                string newZh = EditorGUILayout.TextField(character.characterName.zh ?? "", nameFieldStyle);
                if (newZh != character.characterName.zh)
                {
                    character.characterName.zh = newZh;
                }
                EditorGUILayout.EndHorizontal();

                // English
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(15);
                EditorGUILayout.LabelField("English:", GUILayout.Width(70));
                string newEn = EditorGUILayout.TextField(character.characterName.en ?? "", nameFieldStyle);
                if (newEn != character.characterName.en)
                {
                    character.characterName.en = newEn;
                }
                EditorGUILayout.EndHorizontal();

                // 日本語
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(15);
                EditorGUILayout.LabelField("日本語:", GUILayout.Width(70));
                string newJa = EditorGUILayout.TextField(character.characterName.ja ?? "", nameFieldStyle);
                if (newJa != character.characterName.ja)
                {
                    character.characterName.ja = newJa;
                }
                EditorGUILayout.EndHorizontal();
            }

        }

        // 编辑模式下显示avatar选择
        if (isEditing)
        {
            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Avatar:", GUILayout.Width(70));

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
                    }
                }
                else
                {
                    // 清空选择
                    tempSelectedSprites.Remove(character.id);
                    character.avatarAssetPath = "";
                }
            }

            EditorGUILayout.EndHorizontal();

            // 显示状态提示
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(70);

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

            // isPlayer 复选框
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Is Player:", GUILayout.Width(70));

            bool newIsPlayer = EditorGUILayout.Toggle(character.isPlayer);
            if (newIsPlayer != character.isPlayer)
            {
                character.isPlayer = newIsPlayer;
            }

            EditorGUILayout.EndHorizontal();

            // Done 和 Cancel 按钮（右下角）
            EditorGUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Cancel", EditorStyles.miniButton, GUILayout.Width(70)))
            {
                // 取消编辑，不保存
                editingCharacterId = "";
                tempSelectedSprites.Remove(character.id);
                // 重新加载角色数据以恢复原始值
                LoadCharacterLibrary();
            }

            GUI.backgroundColor = new Color(0.7f, 1f, 0.7f);
            if (GUILayout.Button("Done", EditorStyles.miniButton, GUILayout.Width(70)))
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
                    // 统一保存角色数据
                    SaveCharacterLibrary(character.id);
                    Debug.Log($"✓ Saved character: {character.character}");
                    editingCharacterId = "";
                    tempSelectedSprites.Remove(character.id);
                }
            }
            GUI.backgroundColor = Color.white;

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

            // 显示 Name 字段（多语言）
            var nameTitleStyle = new GUIStyle(EditorStyles.miniLabel);
            nameTitleStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f);
            nameTitleStyle.fontStyle = FontStyle.Bold;

            var nameStyle = new GUIStyle(EditorStyles.miniLabel);
            nameStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f);

            // 检查是否有任何名称
            bool hasAnyName = !string.IsNullOrEmpty(character.characterName?.en) ||
                             !string.IsNullOrEmpty(character.characterName?.zh) ||
                             !string.IsNullOrEmpty(character.characterName?.ja);

            if (hasAnyName)
            {
                EditorGUILayout.LabelField("Name:", nameTitleStyle);

                // 显示英文名称
                if (!string.IsNullOrEmpty(character.characterName?.en))
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(10);
                    EditorGUILayout.LabelField($"EN: {character.characterName.en}", nameStyle);
                    EditorGUILayout.EndHorizontal();
                }

                // 显示中文名称
                if (!string.IsNullOrEmpty(character.characterName?.zh))
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(10);
                    EditorGUILayout.LabelField($"中文: {character.characterName.zh}", nameStyle);
                    EditorGUILayout.EndHorizontal();
                }

                // 显示日文名称
                if (!string.IsNullOrEmpty(character.characterName?.ja))
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(10);
                    EditorGUILayout.LabelField($"日本語: {character.characterName.ja}", nameStyle);
                    EditorGUILayout.EndHorizontal();
                }
            }

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
            HandleCharacterDrag(lastRect, character, parentFolder);
            HandleCharacterDropForReorder(lastRect, character);
        }

        // 绘制插入线提示（在角色之后）
        if (insertBeforeCharacterId == character.id && insertAfter)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(indentLevel * 20 + 25);
            Rect insertLineRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.Height(3));
            EditorGUI.DrawRect(insertLineRect, new Color(0.3f, 0.6f, 1f, 0.8f)); // 蓝色插入线
            EditorGUILayout.EndHorizontal();
        }
    }

    private void HandleCharacterDrag(Rect rect, CharacterData character, CharacterFolder parentFolder)
    {
        Event e = Event.current;

        // 开始拖拽
        if (e.type == EventType.MouseDown && rect.Contains(e.mousePosition) && e.button == 0)
        {
            // 检查是否点击在按钮区域
            float buttonAreaWidth = 130;
            Rect buttonArea = new Rect(rect.xMax - buttonAreaWidth, rect.y, buttonAreaWidth, rect.height);
            if (!buttonArea.Contains(e.mousePosition))
            {
                draggedCharacterForReorder = character;
                draggedCharacterFromFolder = parentFolder;
                isDraggingCharacterForReorder = false;
            }
        }

        // 拖拽中
        if (e.type == EventType.MouseDrag && draggedCharacterForReorder == character && !isDraggingCharacterForReorder)
        {
            DragAndDrop.PrepareStartDrag();
            DragAndDrop.SetGenericData("ReorderCharacter", character);  // 用于manager内部排序
            DragAndDrop.SetGenericData("CharacterData", character);     // 用于拖拽到node编辑器
            DragAndDrop.StartDrag("Dragging Character");
            isDraggingCharacterForReorder = true;
            e.Use();
        }

        // 拖拽结束
        if (e.type == EventType.DragExited || e.type == EventType.MouseUp)
        {
            if (draggedCharacterForReorder != null)
            {
                draggedCharacterForReorder = null;
                draggedCharacterFromFolder = null;
                isDraggingCharacterForReorder = false;
                insertBeforeCharacterId = null;
                insertAfter = false;
                Repaint();
            }
        }
    }

    private void HandleCharacterDropForReorder(Rect rect, CharacterData character)
    {
        if (draggedCharacterForReorder == null || draggedCharacterForReorder == character) return;

        Event e = Event.current;

        // 扩大检测范围，包括角色上下各10像素的间隙
        Rect expandedRect = new Rect(rect.x, rect.y - 10, rect.width, rect.height + 20);

        if (expandedRect.Contains(e.mousePosition))
        {
            if (e.type == EventType.DragUpdated)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Move;

                // 根据鼠标位置判断插入到前面还是后面
                float mouseY = e.mousePosition.y;
                float rectMiddle = rect.y + rect.height / 2;
                bool shouldInsertAfter = mouseY > rectMiddle;

                // 更新插入位置提示
                insertBeforeCharacterId = character.id;
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

                ReorderCharacter(draggedCharacterForReorder, character, shouldInsertAfter);
                DragAndDrop.AcceptDrag();
                draggedCharacterForReorder = null;
                isDraggingCharacterForReorder = false;

                // 清除插入位置提示
                insertBeforeCharacterId = null;
                insertAfter = false;

                e.Use();
            }
        }
    }

    private void ReorderCharacter(CharacterData sourceCharacter, CharacterData targetCharacter, bool insertAfter)
    {
        // 找到两个角色所在的文件夹
        var sourceFolder = FindCharacterParentFolder(sourceCharacter.id);
        var targetFolder = FindCharacterParentFolder(targetCharacter.id);

        // 只有在同一个文件夹内才能排序
        if (sourceFolder != targetFolder)
            return;

        List<string> list = sourceFolder == null ? characterFolderData.rootCharacterIds : sourceFolder.characterIds;

        int sourceIndex = list.IndexOf(sourceCharacter.id);
        int targetIndex = list.IndexOf(targetCharacter.id);

        if (sourceIndex != -1 && targetIndex != -1 && sourceIndex != targetIndex)
        {
            list.RemoveAt(sourceIndex);

            // 重新获取目标索引（因为移除可能改变了索引）
            targetIndex = list.IndexOf(targetCharacter.id);

            // 如果要插入到后面，索引+1
            if (insertAfter)
            {
                targetIndex++;
            }

            list.Insert(targetIndex, sourceCharacter.id);
            SaveCharacterFolderStructure();
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

        // 添加到默认文件夹
        var defaultFolder = characterFolderData.rootFolders.FirstOrDefault(f => f.id == "default_character_folder");
        if (defaultFolder != null && !defaultFolder.characterIds.Contains(newChar.id))
        {
            defaultFolder.characterIds.Add(newChar.id);
            SaveCharacterFolderStructure();
        }

        // 自动进入编辑模式
        editingCharacterId = newChar.id;
    }

    private void DeleteCharacter(int index)
    {
        if (EditorUtility.DisplayDialog("Delete Character",
            $"Delete character '{characterLibrary.characters[index].characterName?.en ?? "Unknown"}'?\n\nNote: Dialogue nodes using this character will show 'Unknown Character'.",
            "Delete", "Cancel"))
        {
            string deletedCharacterId = characterLibrary.characters[index].id;

            var list = new List<CharacterData>(characterLibrary.characters);
            list.RemoveAt(index);
            characterLibrary.characters = list.ToArray();

            // 清理临时选择
            tempSelectedSprites.Remove(deletedCharacterId);

            SaveCharacterLibraryInternal();

            // 从文件夹中删除
            CleanupCharacterFromFolders(deletedCharacterId);

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
            // 保存前，只在Use ID模式时从Google Sheets读取角色name
            if (characterLibrary?.characters != null && DialogueLocalization.IsLoaded)
            {
                foreach (var character in characterLibrary.characters)
                {
                    // 只在Use ID模式时才从DialogueLocalization更新
                    if (character.useNameId && !string.IsNullOrEmpty(character.nameId))
                    {
                        var locData = DialogueLocalization.GetAllLanguages(character.nameId);
                        if (locData != null)
                        {
                            character.characterName = new LocalizedText
                            {
                                zh = locData.ContainsKey(Language.ChineseSimplified) ? locData[Language.ChineseSimplified] : "",
                                en = locData.ContainsKey(Language.English) ? locData[Language.English] : "",
                                ja = locData.ContainsKey(Language.Japanese) ? locData[Language.Japanese] : ""
                            };
                        }
                    }
                    // Direct Input模式时不做任何处理，保持characterName不变
                }
            }

            string savePath = GetCharacterLibraryPath();
            string newJson = JsonUtility.ToJson(characterLibrary, true).Trim();

            // 强制统一换行符为 LF（Unix格式）
            newJson = newJson.Replace("\r\n", "\n");

            // 只有在内容真正改变时才写入文件
            bool needsSave = true;
            if (File.Exists(savePath))
            {
                string existingJson = File.ReadAllText(savePath);
                // 也统一现有文件的换行符来比较
                existingJson = existingJson.Replace("\r\n", "\n");

                if (existingJson == newJson)
                {
                    needsSave = false;
                }
            }

            if (needsSave)
            {
                string folder = Path.GetDirectoryName(savePath);
                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                // 使用 UTF8 without BOM，并确保换行符一致
                System.Text.UTF8Encoding utf8WithoutBom = new System.Text.UTF8Encoding(false);
                File.WriteAllText(savePath, newJson, utf8WithoutBom);
                Debug.Log("[Manager] Character library saved (content changed)");
            }
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
            string newJson = JsonUtility.ToJson(characterLibrary, true).Trim();

            // 强制统一换行符为 LF（Unix格式）
            newJson = newJson.Replace("\r\n", "\n");

            // 只有在内容真正改变时才写入文件
            bool needsSave = true;
            if (File.Exists(savePath))
            {
                string existingJson = File.ReadAllText(savePath);
                // 也统一现有文件的换行符来比较
                existingJson = existingJson.Replace("\r\n", "\n");

                if (existingJson == newJson)
                {
                    needsSave = false;
                }
            }

            if (needsSave)
            {
                string folder = Path.GetDirectoryName(savePath);
                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                // 使用 UTF8 without BOM，并确保换行符一致
                System.Text.UTF8Encoding utf8WithoutBom = new System.Text.UTF8Encoding(false);
                File.WriteAllText(savePath, newJson, utf8WithoutBom);
                Debug.Log("[Manager] Character library saved (content changed)");

                EditorApplication.delayCall += () =>
                {
                    DialogueTreeEditor.RefreshAllOpenEditors();
                    RegenerateAffectedRuntimeJSON(modifiedCharacterId);
                };
            }
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

                // 兼容性检查：确保所有角色都有character字段
                bool needsSave = false;
                if (characterLibrary?.characters != null)
                {
                    foreach (var character in characterLibrary.characters)
                    {
                        if (string.IsNullOrEmpty(character.character))
                        {
                            // 如果没有character字段，使用characterName填充
                            character.character = character.characterName?.en ?? "New Character";
                            needsSave = true;
                        }

                        // 不再自动判断模式，完全依赖useNameId字段
                    }
                }

                if (needsSave)
                {
                    SaveCharacterLibraryInternal();
                    Debug.Log("Updated character library with new 'character' field for compatibility");
                }

                //Debug.Log($"Loaded character library from: {loadPath}");
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

    private void LoadCharacterFolderStructure()
    {
        try
        {
            string loadPath = GetCharacterFolderStructurePath();

            if (File.Exists(loadPath))
            {
                string json = File.ReadAllText(loadPath);
                characterFolderData = JsonUtility.FromJson<CharacterFolderData>(json);
            }
            else
            {
                characterFolderData = new CharacterFolderData();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to load character folder structure: {e.Message}");
            characterFolderData = new CharacterFolderData();
        }

        EnsureDefaultCharacterFolder();
    }

    private void SaveCharacterFolderStructure()
    {
        try
        {
            string savePath = GetCharacterFolderStructurePath();
            string newJson = JsonUtility.ToJson(characterFolderData, true).Trim();

            // 强制统一换行符为 LF（Unix格式）
            newJson = newJson.Replace("\r\n", "\n");

            // 只有在内容真正改变时才写入文件
            bool needsSave = true;
            if (File.Exists(savePath))
            {
                string existingJson = File.ReadAllText(savePath);
                // 也统一现有文件的换行符来比较
                existingJson = existingJson.Replace("\r\n", "\n");

                if (existingJson == newJson)
                {
                    needsSave = false;
                }
            }

            if (needsSave)
            {
                string folder = Path.GetDirectoryName(savePath);
                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                // 使用 UTF8 without BOM，并确保换行符一致
                System.Text.UTF8Encoding utf8WithoutBom = new System.Text.UTF8Encoding(false);
                File.WriteAllText(savePath, newJson, utf8WithoutBom);
                Debug.Log("[Manager] Character folder structure saved (content changed)");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to save character folder structure: {e.Message}");
        }
    }

    private void EnsureDefaultCharacterFolder()
    {
        if (characterFolderData.rootFolders.Count == 0 ||
            !characterFolderData.rootFolders.Any(f => f.id == "default_character_folder"))
        {
            var defaultFolder = new CharacterFolder
            {
                name = "All Characters",
                id = "default_character_folder"
            };

            // 将所有现有角色添加到默认文件夹
            if (characterLibrary?.characters != null)
            {
                foreach (var character in characterLibrary.characters)
                {
                    if (!IsCharacterInAnyFolder(character.id))
                    {
                        defaultFolder.characterIds.Add(character.id);
                    }
                }
            }

            characterFolderData.rootFolders.Insert(0, defaultFolder);
        }
        else
        {
            // 确保所有角色都在某个文件夹中
            var defaultFolder = characterFolderData.rootFolders.FirstOrDefault(f => f.id == "default_character_folder");
            if (defaultFolder != null && characterLibrary?.characters != null)
            {
                foreach (var character in characterLibrary.characters)
                {
                    if (!IsCharacterInAnyFolder(character.id))
                    {
                        defaultFolder.characterIds.Add(character.id);
                    }
                }
            }
        }
    }

    private bool IsCharacterInAnyFolder(string characterId)
    {
        if (characterFolderData.rootCharacterIds.Contains(characterId)) return true;
        return CheckCharacterFolderRecursive(characterFolderData.rootFolders, characterId);
    }

    private bool CheckCharacterFolderRecursive(List<CharacterFolder> folders, string characterId)
    {
        foreach (var folder in folders)
        {
            if (folder.characterIds.Contains(characterId)) return true;
            if (CheckCharacterFolderRecursive(folder.subfolders, characterId)) return true;
        }
        return false;
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
            formattedJson += $"      \"name\": {SerializeLocalizedText(item.name, 3)},\n";
            formattedJson += $"      \"avatarAddr\": \"{EscapeJsonString(item.avatarAddr)}\",\n";
            formattedJson += $"      \"isPlayer\": {item.isPlayer.ToString().ToLower()},\n";
            formattedJson += $"      \"content\": {SerializeLocalizedText(item.content, 3)}";

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
                    formattedJson += $"          \"triggerTiming\": {(int)evt.triggerTiming}\n";
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

        // 统一使用 LF 换行符和 UTF8 without BOM
        formattedJson = formattedJson.Replace("\r\n", "\n");
        System.Text.UTF8Encoding utf8WithoutBom = new System.Text.UTF8Encoding(false);
        File.WriteAllText(jsonPath, formattedJson, utf8WithoutBom);
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
        bool isExpanded = folderExpandedState.ContainsKey(folder.id) && folderExpandedState[folder.id];
        if (GUI.Button(arrowRect, isExpanded ? "▼" : "▶", EditorStyles.label))
        {
            folderExpandedState[folder.id] = !isExpanded;
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

        bool isExpandedForChildren = folderExpandedState.ContainsKey(folder.id) && folderExpandedState[folder.id];
        if (isExpandedForChildren)
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
                // 扩大检测范围，包括文件夹上下各10像素的间隙
                Rect expandedRect = new Rect(rect.x, rect.y - 10, rect.width, rect.height + 20);
                if (e.type == EventType.DragUpdated && expandedRect.Contains(e.mousePosition))
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
                else if (e.type == EventType.DragPerform && expandedRect.Contains(e.mousePosition))
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

        // 扩大检测范围，包括文件上下各10像素的间隙
        Rect expandedRect = new Rect(rect.x, rect.y - 10, rect.width, rect.height + 20);
        if (expandedRect.Contains(e.mousePosition))
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
            string json = JsonUtility.ToJson(emptyTree, true).Trim();
            // 统一使用 LF 换行符和 UTF8 without BOM
            json = json.Replace("\r\n", "\n");
            System.Text.UTF8Encoding utf8WithoutBom = new System.Text.UTF8Encoding(false);
            File.WriteAllText(savePath, json, utf8WithoutBom);

            // 创建对应的 .json 运行时文件
            string jsonPath = Path.ChangeExtension(savePath, ".json");
            string runtimeJson = "{\n  \"conversations\": [\n    {\n      \"index\": 0,\n      \"name\": \"\",\n      \"avatarAddr\": \"\",\n      \"isPlayer\": false,\n      \"content\": \"Start dialogue here...\",\n      \"nextIndex\": -1,\n      \"choices\": [],\n      \"eventCalls\": [],\n      \"conditionalBranches\": []\n    }\n  ],\n  \"currentIndex\": 0\n}";
            File.WriteAllText(jsonPath, runtimeJson, utf8WithoutBom);

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

            string newJson = JsonUtility.ToJson(folderData, true).Trim();

            // 强制统一换行符为 LF（Unix格式）
            newJson = newJson.Replace("\r\n", "\n");

            // 只有在内容真正改变时才写入文件
            bool needsSave = true;
            if (File.Exists(savePath))
            {
                string existingJson = File.ReadAllText(savePath);
                // 也统一现有文件的换行符来比较
                existingJson = existingJson.Replace("\r\n", "\n");

                if (existingJson == newJson)
                {
                    needsSave = false;
                }
            }

            if (needsSave)
            {
                string folder = Path.GetDirectoryName(savePath);
                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                // 使用 UTF8 without BOM，并确保换行符一致
                System.Text.UTF8Encoding utf8WithoutBom = new System.Text.UTF8Encoding(false);
                File.WriteAllText(savePath, newJson, utf8WithoutBom);
                Debug.Log("[Manager] Folder structure saved (content changed)");
            }
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

            writer.Write($"\"{EscapeCSV(node.content?.en ?? "")}\"");

            for (int i = 0; i < maxChoices; i++)
            {
                if (node.choices != null && i < node.choices.Count)
                {
                    writer.Write($",\"{EscapeCSV(node.choices[i].text?.en ?? "")}\"");

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
                            writer.Write($"\"{EscapeCSV(choice.text?.en ?? "")}\",");
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



    #region Localization Export/Import

    /// <summary>
    /// 导出本地化文本到CSV
    /// </summary>
    private void ExportLocalizationToCSV()
    {
        string savePath = EditorUtility.SaveFilePanel("Export Localization to CSV",
            Application.dataPath, "Localization_Export.csv", "csv");

        if (string.IsNullOrEmpty(savePath)) return;

        try
        {
            using (StreamWriter writer = new StreamWriter(savePath, false, System.Text.Encoding.UTF8))
            {
                // 写入表头
                writer.WriteLine("ID,中文,English,日本語");

                // 1. 导出所有角色名
                ExportCharacterNames(writer);

                // 2. 按Manager中的顺序导出所有dtree文件的文本
                ExportDialogueTexts(writer);
            }

            EditorUtility.DisplayDialog("Export Complete",
                $"Localization texts exported to:\n{savePath}", "OK");

            Debug.Log($"[Localization Export] Successfully exported to: {savePath}");
        }
        catch (System.Exception e)
        {
            EditorUtility.DisplayDialog("Export Failed",
                $"Failed to export localization:\n{e.Message}", "OK");
            Debug.LogError($"[Localization Export] Error: {e.Message}");
        }
    }

    /// <summary>
    /// 导出角色名
    /// </summary>
    private void ExportCharacterNames(StreamWriter writer)
    {
        if (characterLibrary == null || characterLibrary.characters == null)
            return;

        // 按照Manager中显示的顺序导出角色
        List<CharacterData> orderedCharacters = GetOrderedCharacters();

        foreach (var character in orderedCharacters)
        {
            if (character == null || string.IsNullOrEmpty(character.id))
                continue;

            string id = $"character_{character.id}";
            string zh = EscapeCSV(character.characterName?.zh ?? "");
            string en = EscapeCSV(character.characterName?.en ?? "");
            string ja = EscapeCSV(character.characterName?.ja ?? "");

            writer.WriteLine($"{id},{zh},{en},{ja}");
        }
    }

    /// <summary>
    /// 获取按Manager显示顺序排列的角色列表
    /// </summary>
    private List<CharacterData> GetOrderedCharacters()
    {
        List<CharacterData> orderedList = new List<CharacterData>();

        if (characterLibrary == null || characterLibrary.characters == null)
            return orderedList;

        // 如果有角色文件夹结构
        if (characterFolderData != null)
        {
            // 先添加根级别的角色
            foreach (var charId in characterFolderData.rootCharacterIds)
            {
                var character = System.Array.Find(characterLibrary.characters, c => c.id == charId);
                if (character != null)
                    orderedList.Add(character);
            }

            // 再递归添加文件夹中的角色
            foreach (var folder in characterFolderData.rootFolders)
            {
                AddCharactersFromFolder(folder, orderedList);
            }
        }
        else
        {
            // 如果没有文件夹结构，直接按数组顺序
            orderedList.AddRange(characterLibrary.characters);
        }

        return orderedList;
    }

    /// <summary>
    /// 从角色文件夹递归添加角色
    /// </summary>
    private void AddCharactersFromFolder(CharacterFolder folder, List<CharacterData> list)
    {
        if (folder == null) return;

        // 添加当前文件夹的角色
        foreach (var charId in folder.characterIds)
        {
            var character = System.Array.Find(characterLibrary.characters, c => c.id == charId);
            if (character != null)
                list.Add(character);
        }

        // 递归添加子文件夹的角色
        foreach (var subfolder in folder.subfolders)
        {
            AddCharactersFromFolder(subfolder, list);
        }
    }

    /// <summary>
    /// 导出对话文本
    /// </summary>
    private void ExportDialogueTexts(StreamWriter writer)
    {
        // 获取按Manager显示顺序排列的所有dtree文件
        List<string> orderedFiles = GetOrderedDialogueFiles();

        foreach (var dtreePath in orderedFiles)
        {
            ExportSingleDialogueFile(writer, dtreePath);
        }
    }

    /// <summary>
    /// 获取按Manager显示顺序排列的dtree文件列表
    /// </summary>
    private List<string> GetOrderedDialogueFiles()
    {
        List<string> orderedFiles = new List<string>();

        if (folderData == null) return orderedFiles;

        // 递归获取所有文件
        foreach (var folder in folderData.rootFolders)
        {
            AddFilesFromFolder(folder, orderedFiles);
        }

        // 添加根级别的文件
        foreach (var guid in folderData.rootFileGuids)
        {
            if (guidToPath.ContainsKey(guid))
            {
                string dtreePath = guidToPath[guid];
                if (File.Exists(dtreePath))
                {
                    orderedFiles.Add(dtreePath);
                }
            }
        }

        return orderedFiles;
    }

    /// <summary>
    /// 从文件夹递归添加文件
    /// </summary>
    private void AddFilesFromFolder(VirtualFolder folder, List<string> fileList)
    {
        if (folder == null) return;

        // 先添加子文件夹的文件
        foreach (var subfolder in folder.subfolders)
        {
            AddFilesFromFolder(subfolder, fileList);
        }

        // 再添加当前文件夹的文件
        foreach (var guid in folder.fileGuids)
        {
            if (guidToPath.ContainsKey(guid))
            {
                string dtreePath = guidToPath[guid];
                if (File.Exists(dtreePath))
                {
                    fileList.Add(dtreePath);
                }
            }
        }
    }

    /// <summary>
    /// 导出单个对话文件的所有文本
    /// </summary>
    private void ExportSingleDialogueFile(StreamWriter writer, string dtreePath)
    {
        try
        {
            string json = File.ReadAllText(dtreePath);
            DialogueTreeData treeData = JsonUtility.FromJson<DialogueTreeData>(json);

            if (treeData == null || treeData.nodes == null)
                return;

            string fileName = Path.GetFileNameWithoutExtension(dtreePath);

            // 按节点索引排序
            var sortedNodes = treeData.nodes.OrderBy(n => n.index).ToList();

            foreach (var node in sortedNodes)
            {
                // 导出对话文本
                if (node.content != null && node.content.HasAnyText())
                {
                    string id = $"{fileName}_node_{node.index}_dialogue";
                    string zh = EscapeCSV(node.content.zh ?? "");
                    string en = EscapeCSV(node.content.en ?? "");
                    string ja = EscapeCSV(node.content.ja ?? "");

                    writer.WriteLine($"{id},{zh},{en},{ja}");
                }

                // 导出所有选项文本
                if (node.choices != null)
                {
                    for (int i = 0; i < node.choices.Count; i++)
                    {
                        var choice = node.choices[i];
                        if (choice.text != null && choice.text.HasAnyText())
                        {
                            string id = $"{fileName}_node_{node.index}_choice_{i}";
                            string zh = EscapeCSV(choice.text.zh ?? "");
                            string en = EscapeCSV(choice.text.en ?? "");
                            string ja = EscapeCSV(choice.text.ja ?? "");

                            writer.WriteLine($"{id},{zh},{en},{ja}");
                        }
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Localization Export] Failed to export file {dtreePath}: {e.Message}");
        }
    }

    /// <summary>
    /// CSV转义处理
    /// </summary>
    private string EscapeCSV(string text)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        // 如果包含逗号、引号或换行符，需要用引号包围并转义内部引号
        if (text.Contains(",") || text.Contains("\"") || text.Contains("\n") || text.Contains("\r"))
        {
            text = text.Replace("\"", "\"\"");
            return $"\"{text}\"";
        }

        return text;
    }

    /// <summary>
    /// 从CSV导入本地化文本
    /// </summary>
    private void ImportLocalizationFromCSV()
    {
        string loadPath = EditorUtility.OpenFilePanel("Import Localization from CSV",
            Application.dataPath, "csv");

        if (string.IsNullOrEmpty(loadPath)) return;

        try
        {
            List<string> unmatchedIds = new List<string>();
            int successCount = 0;

            using (StreamReader reader = new StreamReader(loadPath, System.Text.Encoding.UTF8))
            {
                // 跳过表头
                string header = reader.ReadLine();

                while (!reader.EndOfStream)
                {
                    string line = reader.ReadLine();
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    var parts = ParseCSVLine(line);
                    if (parts.Length < 4)
                        continue;

                    string id = parts[0];
                    string zh = parts[1];
                    string en = parts[2];
                    string ja = parts[3];

                    bool matched = ImportSingleText(id, zh, en, ja);
                    if (matched)
                    {
                        successCount++;
                    }
                    else
                    {
                        unmatchedIds.Add(id);
                    }
                }
            }

            // 保存修改
            SaveAllModifications();

            // 显示结果
            if (unmatchedIds.Count > 0)
            {
                string unmatchedList = string.Join("\n", unmatchedIds.Take(10));
                if (unmatchedIds.Count > 10)
                {
                    unmatchedList += $"\n... and {unmatchedIds.Count - 10} more";
                }

                EditorUtility.DisplayDialog("Import Complete with Warnings",
                    $"Imported: {successCount} texts\n" +
                    $"Unmatched IDs: {unmatchedIds.Count}\n\n" +
                    $"Unmatched IDs:\n{unmatchedList}",
                    "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Import Complete",
                    $"Successfully imported {successCount} localization texts!",
                    "OK");
            }

            // 刷新显示
            RefreshAll();

            Debug.Log($"[Localization Import] Imported {successCount} texts, {unmatchedIds.Count} unmatched");
        }
        catch (System.Exception e)
        {
            EditorUtility.DisplayDialog("Import Failed",
                $"Failed to import localization:\n{e.Message}",
                "OK");
            Debug.LogError($"[Localization Import] Error: {e.Message}");
        }
    }

    /// <summary>
    /// 解析CSV行（处理引号和逗号）
    /// </summary>
    private string[] ParseCSVLine(string line)
    {
        List<string> fields = new List<string>();
        bool inQuotes = false;
        string currentField = "";

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    // 双引号转义
                    currentField += '"';
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                fields.Add(currentField);
                currentField = "";
            }
            else
            {
                currentField += c;
            }
        }

        fields.Add(currentField);
        return fields.ToArray();
    }

    /// <summary>
    /// 导入单条文本
    /// </summary>
    private bool ImportSingleText(string id, string zh, string en, string ja)
    {
        // 角色名
        if (id.StartsWith("character_"))
        {
            string characterId = id.Substring("character_".Length);
            return ImportCharacterName(characterId, zh, en, ja);
        }
        // 对话文本和选项
        else
        {
            return ImportDialogueText(id, zh, en, ja);
        }
    }

    /// <summary>
    /// 导入角色名
    /// </summary>
    private bool ImportCharacterName(string characterId, string zh, string en, string ja)
    {
        if (characterLibrary == null || characterLibrary.characters == null)
            return false;

        var character = System.Array.Find(characterLibrary.characters, c => c.id == characterId);
        if (character == null)
            return false;

        if (character.characterName == null)
        {
            character.characterName = new LocalizedText();
        }

        character.characterName.zh = zh;
        character.characterName.en = en;
        character.characterName.ja = ja;

        return true;
    }

    /// <summary>
    /// 导入对话文本
    /// </summary>
    private bool ImportDialogueText(string id, string zh, string en, string ja)
    {
        // 解析ID: {fileName}_node_{nodeIndex}_dialogue 或 {fileName}_node_{nodeIndex}_choice_{choiceIndex}
        var parts = id.Split(new[] { "_node_" }, System.StringSplitOptions.None);
        if (parts.Length != 2)
            return false;

        string fileName = parts[0];
        string remaining = parts[1];

        // 找到对应的dtree文件
        string dtreePath = FindDialogueFile(fileName);
        if (string.IsNullOrEmpty(dtreePath) || !File.Exists(dtreePath))
            return false;

        try
        {
            // 加载文件
            string json = File.ReadAllText(dtreePath);
            DialogueTreeData treeData = JsonUtility.FromJson<DialogueTreeData>(json);

            if (treeData == null || treeData.nodes == null)
                return false;

            // 解析节点索引
            int nodeIndex;
            bool isChoice = remaining.Contains("_choice_");

            if (isChoice)
            {
                var choiceParts = remaining.Split(new[] { "_choice_" }, System.StringSplitOptions.None);
                if (!int.TryParse(choiceParts[0], out nodeIndex))
                    return false;

                int choiceIndex;
                if (!int.TryParse(choiceParts[1], out choiceIndex))
                    return false;

                // 更新选项文本
                var node = treeData.nodes.Find(n => n.index == nodeIndex);
                if (node == null || node.choices == null || choiceIndex >= node.choices.Count)
                    return false;

                if (node.choices[choiceIndex].text == null)
                {
                    node.choices[choiceIndex].text = new LocalizedText();
                }

                node.choices[choiceIndex].text.zh = zh;
                node.choices[choiceIndex].text.en = en;
                node.choices[choiceIndex].text.ja = ja;
            }
            else
            {
                if (!remaining.EndsWith("_dialogue"))
                    return false;

                string nodeIndexStr = remaining.Substring(0, remaining.Length - "_dialogue".Length);
                if (!int.TryParse(nodeIndexStr, out nodeIndex))
                    return false;

                // 更新对话文本
                var node = treeData.nodes.Find(n => n.index == nodeIndex);
                if (node == null)
                    return false;

                if (node.content == null)
                {
                    node.content = new LocalizedText();
                }

                node.content.zh = zh;
                node.content.en = en;
                node.content.ja = ja;
            }

            // 保存文件
            string updatedJson = JsonUtility.ToJson(treeData, true).Trim();
            // 统一使用 LF 换行符和 UTF8 without BOM
            updatedJson = updatedJson.Replace("\r\n", "\n");
            System.Text.UTF8Encoding utf8WithoutBom = new System.Text.UTF8Encoding(false);
            File.WriteAllText(dtreePath, updatedJson, utf8WithoutBom);

            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Localization Import] Failed to import to file {dtreePath}: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 根据文件名查找dtree文件路径
    /// </summary>
    private string FindDialogueFile(string fileName)
    {
        foreach (var kvp in guidToPath)
        {
            string path = kvp.Value;
            string currentFileName = Path.GetFileNameWithoutExtension(path);
            if (currentFileName == fileName)
            {
                return path;
            }
        }
        return null;
    }

    /// <summary>
    /// 保存所有修改
    /// </summary>
    private void SaveAllModifications()
    {
        // 保存角色库
        if (characterLibrary != null)
        {
            SaveCharacterLibrary("");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    #endregion
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

    private string GetLocalizationSettingsPath()
    {
        var script = MonoScript.FromScriptableObject(this);
        string scriptPath = AssetDatabase.GetAssetPath(script);

        if (string.IsNullOrEmpty(scriptPath))
        {
            return "Assets/Editor/Dialogue/LocalizationSettings.json";
        }

        string scriptFolder = Path.GetDirectoryName(scriptPath);
        return Path.Combine(scriptFolder, "LocalizationSettings.json");
    }

    private void LoadLocalizationSettings()
    {
        string path = GetLocalizationSettingsPath();
        if (File.Exists(path))
        {
            try
            {
                string json = File.ReadAllText(path);
                DialogueLocalizationSettings settings = JsonUtility.FromJson<DialogueLocalizationSettings>(json);
                if (settings != null && !string.IsNullOrEmpty(settings.googleSheetsCsvUrl))
                {
                    csvUrlInput = settings.googleSheetsCsvUrl;
                    DialogueLocalization.SetCsvUrl(csvUrlInput);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load localization settings: {e.Message}");
            }
        }
    }

    private void SaveLocalizationSettings()
    {
        try
        {
            DialogueLocalizationSettings settings = new DialogueLocalizationSettings
            {
                googleSheetsCsvUrl = csvUrlInput
            };

            string path = GetLocalizationSettingsPath();
            string json = JsonUtility.ToJson(settings, true).Trim();

            string folder = Path.GetDirectoryName(path);
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            // 统一使用 LF 换行符和 UTF8 without BOM
            json = json.Replace("\r\n", "\n");
            System.Text.UTF8Encoding utf8WithoutBom = new System.Text.UTF8Encoding(false);
            File.WriteAllText(path, json, utf8WithoutBom);
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to save localization settings: {e.Message}");
        }
    }

    private string GetCharacterFolderStructurePath()
    {
        var script = MonoScript.FromScriptableObject(this);
        string scriptPath = AssetDatabase.GetAssetPath(script);

        if (string.IsNullOrEmpty(scriptPath))
        {
            return "Assets/Editor/Dialogue/CharacterFolderStructure.json";
        }

        string scriptFolder = Path.GetDirectoryName(scriptPath);
        return Path.Combine(scriptFolder, "CharacterFolderStructure.json");
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


    private string SerializeLocalizedText(LocalizedText text, int indentLevel = 2)
    {
        if (text == null) text = new LocalizedText();

        string indent = new string(' ', indentLevel * 2);
        string result = "{\n";
        result += $"{indent}  \"en\": \"{EscapeJsonString(text.en)}\",\n";
        result += $"{indent}  \"zh\": \"{EscapeJsonString(text.zh)}\",\n";
        result += $"{indent}  \"ja\": \"{EscapeJsonString(text.ja)}\"\n";
        result += $"{indent}}}";
        return result;
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
                return character.characterName?.en ?? "";
        }

        return "Unknown Character";
    }

    private Texture2D MakeTex(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; i++)
            pix[i] = col;

        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();

        return result;
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

        // 刷新本地化数据
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
                    EditorUtility.DisplayDialog("刷新失败", message, "确定");
                }
            }));
        }

        Debug.Log("========== Refreshing All Data ==========");

        // 重新加载所有数据
        LoadVirtualFolderStructure();
        LoadCharacterLibrary();
        LoadCharacterFolderStructure();
        ScanAllDialogueTrees();

        // 更新文件修改时间
        UpdateFileModificationTimes();

        Debug.Log($"✓ Loaded {guidToPath.Count} dialogue files");
        Debug.Log($"✓ Loaded {characterLibrary?.characters?.Length ?? 0} characters");
        Debug.Log("Refresh complete!");

        Repaint();
    }

    private void OnFocus()
    {
        // 窗口获得焦点时检查文件是否被修改
        CheckFileChangesNow();
    }

    private void CheckFileChanges()
    {
        // 定期检查（每2秒）
        if (EditorApplication.timeSinceStartup > nextCheckTime)
        {
            nextCheckTime = EditorApplication.timeSinceStartup + CHECK_INTERVAL;
            CheckFileChangesNow();
        }
    }

    private void CheckFileChangesNow()
    {
        bool needReload = false;

        string charLibPath = GetCharacterLibraryPath();
        if (File.Exists(charLibPath))
        {
            var currentTime = File.GetLastWriteTime(charLibPath);
            if (currentTime != lastCharacterLibraryTime)
            {
                Debug.Log($"[Manager] Detected CharacterLibrary.json change, auto-reloading...");
                needReload = true;
            }
        }

        string folderStructPath = GetFolderStructurePath();
        if (File.Exists(folderStructPath))
        {
            var currentTime = File.GetLastWriteTime(folderStructPath);
            if (currentTime != lastFolderStructureTime)
            {
                Debug.Log($"[Manager] Detected FolderStructure.json change, auto-reloading...");
                needReload = true;
            }
        }

        if (needReload)
        {
            LoadVirtualFolderStructure();
            LoadCharacterLibrary();
            ScanAllDialogueTrees();
            UpdateFileModificationTimes();
            Repaint();
        }
    }

    private void UpdateFileModificationTimes()
    {
        string charLibPath = GetCharacterLibraryPath();
        if (File.Exists(charLibPath))
        {
            lastCharacterLibraryTime = File.GetLastWriteTime(charLibPath);
        }

        string folderStructPath = GetFolderStructurePath();
        if (File.Exists(folderStructPath))
        {
            lastFolderStructureTime = File.GetLastWriteTime(folderStructPath);
        }
    }

    private void CleanupCharacterFromFolders(string characterId)
    {
        characterFolderData.rootCharacterIds.Remove(characterId);
        CleanupCharacterFromFoldersRecursive(characterFolderData.rootFolders, characterId);
        SaveCharacterFolderStructure();
    }

    private void CleanupCharacterFromFoldersRecursive(List<CharacterFolder> folders, string characterId)
    {
        foreach (var folder in folders)
        {
            folder.characterIds.Remove(characterId);
            CleanupCharacterFromFoldersRecursive(folder.subfolders, characterId);
        }
    }
    private void HandleCharacterFolderDragAndDrop(Rect rect, CharacterFolder folder)
    {
        Event e = Event.current;

        if (rect.Contains(e.mousePosition))
        {
            if (e.type == EventType.DragUpdated)
            {
                // 只处理角色拖拽到文件夹
                if (draggedCharacterForReorder != null)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Move;
                    e.Use();
                }
            }
            else if (e.type == EventType.DragPerform)
            {
                if (draggedCharacterForReorder != null)
                {
                    MoveCharacterToFolder(draggedCharacterForReorder.id, draggedCharacterFromFolder, folder);
                    DragAndDrop.AcceptDrag();
                    draggedCharacterForReorder = null;
                    isDraggingCharacterForReorder = false;
                    insertBeforeCharacterId = null;
                    insertAfter = false;
                    e.Use();
                }
            }
        }
    }

    private void HandleCharacterFolderContextMenu(Rect rect, CharacterFolder folder)
    {
        Event e = Event.current;

        if (e.type == EventType.ContextClick && rect.Contains(e.mousePosition))
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("New Folder"), false, () => CreateCharacterFolder(folder));
            menu.ShowAsContext();
            e.Use();
        }
    }

    private void RenameCharacterFolder(CharacterFolder folder)
    {
        EditorApplication.delayCall += () =>
        {
            EditorInputDialog.ShowAsync("Rename Folder", "Enter new folder name:", folder.name, (newName) =>
            {
                if (!string.IsNullOrWhiteSpace(newName))
                {
                    folder.name = newName.Trim();
                    SaveCharacterFolderStructure();
                }
            });
        };
    }

    private void CreateCharacterFolder(CharacterFolder parent)
    {
        string folderName = "New Folder";
        int counter = 1;

        var existingNames = parent == null
            ? characterFolderData.rootFolders.Select(f => f.name).ToList()
            : parent.subfolders.Select(f => f.name).ToList();

        string finalName = folderName;
        while (existingNames.Contains(finalName))
        {
            finalName = $"{folderName} {counter}";
            counter++;
        }

        var newFolder = new CharacterFolder
        {
            name = finalName,
            id = System.Guid.NewGuid().ToString()
        };

        if (parent == null)
        {
            characterFolderData.rootFolders.Add(newFolder);
        }
        else
        {
            parent.subfolders.Add(newFolder);
        }

        SaveCharacterFolderStructure();
    }

    private void DeleteCharacterFolder(CharacterFolder folder, CharacterFolder parent)
    {
        List<string> allCharacterIds = new List<string>();
        CollectAllCharactersRecursive(folder, allCharacterIds);

        if (parent == null)
        {
            characterFolderData.rootFolders.Remove(folder);
        }
        else
        {
            parent.subfolders.Remove(folder);
        }

        var defaultFolder = characterFolderData.rootFolders.FirstOrDefault(f => f.id == "default_character_folder");
        if (defaultFolder != null)
        {
            foreach (var characterId in allCharacterIds)
            {
                if (!defaultFolder.characterIds.Contains(characterId))
                {
                    defaultFolder.characterIds.Add(characterId);
                }
            }
        }

        SaveCharacterFolderStructure();
    }

    private void CollectAllCharactersRecursive(CharacterFolder folder, List<string> characterList)
    {
        characterList.AddRange(folder.characterIds);

        foreach (var subfolder in folder.subfolders)
        {
            CollectAllCharactersRecursive(subfolder, characterList);
        }
    }

    private CharacterFolder FindCharacterParentFolder(string characterId)
    {
        if (characterFolderData.rootCharacterIds.Contains(characterId))
            return null;

        return FindCharacterParentFolderRecursive(characterFolderData.rootFolders, characterId);
    }

    private CharacterFolder FindCharacterParentFolderRecursive(List<CharacterFolder> folders, string characterId)
    {
        foreach (var folder in folders)
        {
            if (folder.characterIds.Contains(characterId))
                return folder;

            var result = FindCharacterParentFolderRecursive(folder.subfolders, characterId);
            if (result != null)
                return result;
        }
        return null;
    }

    private void MoveCharacterToFolder(string characterId, CharacterFolder fromFolder, CharacterFolder toFolder)
    {
        if (fromFolder == null)
        {
            characterFolderData.rootCharacterIds.Remove(characterId);
        }
        else
        {
            fromFolder.characterIds.Remove(characterId);
        }

        if (!toFolder.characterIds.Contains(characterId))
        {
            toFolder.characterIds.Add(characterId);
        }

        SaveCharacterFolderStructure();
    }

    private int CalculateCharacterFolderIndent(CharacterFolder folder)
    {
        int level = 0;
        CharacterFolder current = folder;

        while (current != null && current.id != "default_character_folder")
        {
            level++;
            current = FindCharacterFolderParent(current);
        }

        return level;
    }

    private CharacterFolder FindCharacterFolderParent(CharacterFolder targetFolder)
    {
        return FindCharacterFolderParentRecursive(characterFolderData.rootFolders, targetFolder);
    }

    private CharacterFolder FindCharacterFolderParentRecursive(List<CharacterFolder> folders, CharacterFolder targetFolder)
    {
        foreach (var folder in folders)
        {
            if (folder.subfolders.Contains(targetFolder))
                return folder;

            var result = FindCharacterFolderParentRecursive(folder.subfolders, targetFolder);
            if (result != null)
                return result;
        }
        return null;
    }

    private void EditCharacterDescription(CharacterFolder folder)
    {
        EditorApplication.delayCall += () =>
        {
            EditorInputDialog.ShowAsync("Edit Description", "Enter folder description:", folder.description, (newDesc) =>
            {
                if (newDesc != null)
                {
                    folder.description = newDesc.Trim();
                    SaveCharacterFolderStructure();
                }
            });
        };
    }

    #region Save All

    private void SaveAllDialogueTrees()
    {
        if (guidToPath == null || guidToPath.Count == 0)
        {
            EditorUtility.DisplayDialog("Save All", "No files found.", "OK");
            return;
        }

        if (!EditorUtility.DisplayDialog("Save All",
            $"Save {guidToPath.Count} file(s)?\n\n这将强制重新保存所有文件", "Yes", "Cancel"))
            return;

        int saved = 0, failed = 0;
        List<string> errors = new List<string>();

        try
        {
            int idx = 0;
            foreach (var kvp in guidToPath)
            {
                idx++;
                string dtreePath = kvp.Value;
                string name = Path.GetFileName(dtreePath);
                EditorUtility.DisplayProgressBar("Save All", $"{idx}/{guidToPath.Count}: {name}", (float)idx / guidToPath.Count);

                try
                {
                    if (!File.Exists(dtreePath)) { failed++; errors.Add(name + " (not found)"); continue; }

                    string json = File.ReadAllText(dtreePath);
                    DialogueTreeData data = JsonUtility.FromJson<DialogueTreeData>(json);
                    if (data == null || data.nodes == null) { failed++; errors.Add(name + " (invalid)"); continue; }

                    // 从DialogueLocalization更新所有LocalizedText（只更新Use ID模式的内容）
                    if (DialogueLocalization.IsLoaded)
                    {
                        foreach (var node in data.nodes)
                        {
                            // 只在Use ID模式时更新节点内容
                            if (node.useContentId && !string.IsNullOrEmpty(node.contentId) && DialogueLocalization.HasId(node.contentId))
                            {
                                var locData = DialogueLocalization.GetAllLanguages(node.contentId);
                                if (locData != null)
                                {
                                    node.content.en = locData.ContainsKey(Language.English) ? locData[Language.English] : "";
                                    node.content.zh = locData.ContainsKey(Language.ChineseSimplified) ? locData[Language.ChineseSimplified] : "";
                                    node.content.ja = locData.ContainsKey(Language.Japanese) ? locData[Language.Japanese] : "";
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
                                            choice.text.en = locData.ContainsKey(Language.English) ? locData[Language.English] : "";
                                            choice.text.zh = locData.ContainsKey(Language.ChineseSimplified) ? locData[Language.ChineseSimplified] : "";
                                            choice.text.ja = locData.ContainsKey(Language.Japanese) ? locData[Language.Japanese] : "";
                                        }
                                    }
                                }
                            }
                        }
                    }

                    // 强制保存.dtree和.json
                    SaveEditorFormat(dtreePath, data);
                    SaveRuntimeJson(Path.ChangeExtension(dtreePath, ".json"), data);
                    saved++;
                }
                catch (System.Exception e) { failed++; errors.Add(name + $" ({e.Message})"); }
            }
        }
        finally { EditorUtility.ClearProgressBar(); }

        AssetDatabase.Refresh();

        string msg = $"Saved: {saved} files (.dtree + .json)";
        if (failed > 0) { msg += $"\nFailed: {failed}\n"; foreach (var e in errors) msg += $"• {e}\n"; }
        EditorUtility.DisplayDialog("Done", msg, "OK");

        // 刷新所有打开的编辑器窗口
        RefreshAllOpenEditorWindows();
    }

    private void SaveRuntimeJson(string path, DialogueTreeData data)
    {
        var runtime = ConvertRuntime(data);
        var idxMap = new Dictionary<string, int>();
        foreach (var n in data.nodes) idxMap[n.id] = n.index;

        var sb = new System.Text.StringBuilder();
        sb.Append("{\n  \"conversations\": [\n");

        for (int i = 0; i < runtime.Count; i++)
        {
            var item = runtime[i];
            sb.Append("    {\n");
            sb.Append($"      \"index\": {item.index},\n");
            sb.Append($"      \"name\": {SerializeLocalizedText(item.name, 3)},\n");
            sb.Append($"      \"avatarAddr\": \"{EscapeJsonString(item.avatarAddr)}\",\n");
            sb.Append($"      \"isPlayer\": {item.isPlayer.ToString().ToLower()},\n");
            sb.Append($"      \"content\": {SerializeLocalizedText(item.content, 3)}");

            if (item.conditionalBranches?.Count > 0)
            {
                sb.Append(",\n      \"conditionalBranches\": [");
                for (int j = 0; j < item.conditionalBranches.Count; j++)
                {
                    var br = item.conditionalBranches[j];
                    sb.Append($"\n        {{\"targetIndex\": {br.targetIndex}, \"priority\": {br.priority}");
                    if (br.priority > 0 && br.conditions?.Count > 0)
                    {
                        sb.Append(", \"conditions\": [\n");
                        for (int k = 0; k < br.conditions.Count; k++)
                        {
                            var c = br.conditions[k];
                            sb.Append("            {\n");
                            sb.Append($"              \"targetObjectName\": \"{EscapeJsonString(c.targetObjectName)}\",\n");
                            sb.Append($"              \"componentTypeName\": \"{EscapeJsonString(c.componentTypeName)}\",\n");
                            sb.Append($"              \"variableName\": \"{EscapeJsonString(c.variableName)}\",\n");
                            sb.Append($"              \"comparison\": \"{c.comparison}\",\n");
                            sb.Append($"              \"compareValue\": \"{EscapeJsonString(c.compareValue)}\"\n");
                            sb.Append("            }");
                            if (k < br.conditions.Count - 1) sb.Append(",");
                            sb.Append("\n");
                        }
                        sb.Append("          ], \"conditionLogic\": \"");
                        sb.Append(br.conditionLogic);
                        sb.Append("\"");
                    }
                    sb.Append("}");
                    if (j < item.conditionalBranches.Count - 1) sb.Append(",");
                }
                sb.Append("\n      ]");
            }
            else
            {
                int next = -1;
                if (!string.IsNullOrEmpty(item.nextNodeId) && idxMap.ContainsKey(item.nextNodeId))
                    next = idxMap[item.nextNodeId];
                sb.Append($",\n      \"nextIndex\": {next}");
            }

            if (item.choices?.Count > 0)
            {
                sb.Append(",\n      \"choices\": [");
                for (int j = 0; j < item.choices.Count; j++)
                {
                    var ch = item.choices[j];
                    int tgt = -1;
                    if (!string.IsNullOrEmpty(ch.nextNodeId) && idxMap.ContainsKey(ch.nextNodeId))
                        tgt = idxMap[ch.nextNodeId];

                    sb.Append($"\n        {{\"text\": {SerializeLocalizedText(ch.text, 5)}, \"targetIndex\": {tgt}");
                    if (ch.conditions?.Count > 0)
                    {
                        sb.Append(", \"conditions\": [\n");
                        for (int k = 0; k < ch.conditions.Count; k++)
                        {
                            var c = ch.conditions[k];
                            sb.Append("            {\n");
                            sb.Append($"              \"targetObjectName\": \"{EscapeJsonString(c.targetObjectName)}\",\n");
                            sb.Append($"              \"componentTypeName\": \"{EscapeJsonString(c.componentTypeName)}\",\n");
                            sb.Append($"              \"variableName\": \"{EscapeJsonString(c.variableName)}\",\n");
                            sb.Append($"              \"comparison\": \"{c.comparison}\",\n");
                            sb.Append($"              \"compareValue\": \"{EscapeJsonString(c.compareValue)}\"\n");
                            sb.Append("            }");
                            if (k < ch.conditions.Count - 1) sb.Append(",");
                            sb.Append("\n");
                        }
                        sb.Append("          ], \"conditionLogic\": \"");
                        sb.Append(ch.conditionLogic);
                        sb.Append("\"");
                    }
                    sb.Append("}");
                    if (j < item.choices.Count - 1) sb.Append(",");
                }
                sb.Append("\n      ]");
            }

            if (item.eventCalls?.Count > 0)
            {
                sb.Append(",\n      \"eventCalls\": [\n");
                for (int j = 0; j < item.eventCalls.Count; j++)
                {
                    var ev = item.eventCalls[j];
                    sb.Append("        {\n");
                    sb.Append($"          \"targetObjectID\": \"{EscapeJsonString(ev.targetObjectID)}\",\n");
                    sb.Append($"          \"targetObjectName\": \"{EscapeJsonString(ev.targetObjectName)}\",\n");
                    sb.Append($"          \"componentTypeName\": \"{EscapeJsonString(ev.componentTypeName)}\",\n");
                    sb.Append($"          \"methodName\": \"{EscapeJsonString(ev.methodName)}\",\n");
                    sb.Append($"          \"parameterType\": \"{ev.parameterType}\",\n");
                    sb.Append($"          \"stringParameter\": \"{EscapeJsonString(ev.stringParameter)}\",\n");
                    sb.Append($"          \"intParameter\": {ev.intParameter},\n");
                    sb.Append($"          \"floatParameter\": {ev.floatParameter},\n");
                    sb.Append($"          \"boolParameter\": {ev.boolParameter.ToString().ToLower()},\n");
                    sb.Append($"          \"triggerTiming\": {(int)ev.triggerTiming}\n");
                    sb.Append("        }");
                    if (j < item.eventCalls.Count - 1) sb.Append(",");
                    sb.Append("\n");
                }
                sb.Append("      ]");
            }

            sb.Append("\n    }");
            if (i < runtime.Count - 1) sb.Append(",");
            sb.Append("\n");
        }
        sb.Append("  ],\n  \"currentIndex\": 0\n}");
        File.WriteAllText(path, sb.ToString());
    }

    private void SaveEditorFormat(string path, DialogueTreeData data)
    {
        File.WriteAllText(path, JsonUtility.ToJson(data, true).Trim());
    }

    private List<RuntimeDialogueData> ConvertRuntime(DialogueTreeData data)
    {
        var result = new List<RuntimeDialogueData>();
        var idxMap = new Dictionary<string, int>();
        var nodes = data.nodes.OrderBy(n => n.index).ToList();
        foreach (var n in nodes) idxMap[n.id] = n.index;

        foreach (var node in nodes)
        {
            var rt = new RuntimeDialogueData
            {
                index = node.index,
                content = node.content ?? new LocalizedText(),
                eventCalls = new List<DialogueEventCall>(node.eventCalls ?? new List<DialogueEventCall>())
            };

            if (!string.IsNullOrEmpty(node.characterId) && characterLibrary?.characters != null)
            {
                var ch = System.Array.Find(characterLibrary.characters, c => c.id == node.characterId);
                if (ch != null)
                {
                    rt.name = ch.characterName ?? new LocalizedText();
                    rt.avatarAddr = ConvertPath(ch.avatarAssetPath ?? "");
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

    private string ConvertPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return "";
        int idx = path.IndexOf("Resources/");
        if (idx >= 0)
        {
            string sub = path.Substring(idx + 10);
            return System.IO.Path.ChangeExtension(sub, null);
        }
        return System.IO.Path.GetFileNameWithoutExtension(path);
    }

    #endregion
    #endregion

    #region Localization Methods

    private void DrawLocalizationSettings()
    {
        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.BeginHorizontal();

        string statusIcon = DialogueLocalization.IsLoaded ? "✓" : "⚠";
        string statusText = DialogueLocalization.IsLoaded
            ? $"{statusIcon} 本地化已加载 ({DialogueLocalization.GetAllIds().Count} 条)"
            : $"{statusIcon} 本地化未加载";

        GUIStyle foldoutStyle = new GUIStyle(EditorStyles.foldout);
        foldoutStyle.fontStyle = FontStyle.Bold;

        localizationSettingsExpanded = EditorGUILayout.Foldout(
            localizationSettingsExpanded,
            statusText,
            true,
            foldoutStyle);

        EditorGUILayout.EndHorizontal();

        if (localizationSettingsExpanded)
        {
            EditorGUILayout.Space(5);

            EditorGUILayout.HelpBox(
                "Google Sheets 本地化配置\n" +
                "1. 在Google Sheets: 文件 > 共享 > 发布到网络\n" +
                "2. 选择工作表，格式选 CSV，点击发布\n" +
                "3. 复制链接粘贴到下方\n" +
                "格式: ID, 中文, English, 日语",
                MessageType.Info);

            EditorGUILayout.Space(5);

            EditorGUILayout.LabelField("CSV URL:", EditorStyles.boldLabel);
            csvUrlScrollPos = EditorGUILayout.BeginScrollView(csvUrlScrollPos, GUILayout.Height(50));
            csvUrlInput = EditorGUILayout.TextArea(csvUrlInput, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();

            GUI.enabled = !string.IsNullOrEmpty(csvUrlInput);
            if (GUILayout.Button("保存并加载", GUILayout.Height(25)))
            {
                SaveAndLoadLocalization();
            }
            GUI.enabled = true;

            if (GUILayout.Button("清空", GUILayout.Height(25), GUILayout.Width(60)))
            {
                csvUrlInput = "";
                DialogueLocalization.SetCsvUrl("");
                SaveLocalizationSettings();
                DialogueLocalization.Clear();
                Repaint();
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(5);
    }

    private void SaveAndLoadLocalization()
    {
        csvUrlInput = csvUrlInput.Trim();

        if (string.IsNullOrEmpty(csvUrlInput))
        {
            EditorUtility.DisplayDialog("错误", "请输入CSV URL", "确定");
            return;
        }

        if (!csvUrlInput.StartsWith("http://") && !csvUrlInput.StartsWith("https://"))
        {
            EditorUtility.DisplayDialog("错误", "URL必须以 http:// 或 https:// 开头", "确定");
            return;
        }

        DialogueLocalization.SetCsvUrl(csvUrlInput);
        SaveLocalizationSettings();

        EditorCoroutineRunner.StartCoroutine(DialogueLocalization.LoadFromGoogleSheets((success, message) =>
        {
            if (success)
            {
                EditorUtility.DisplayDialog("成功", message, "确定");
                Repaint();
            }
            else
            {
                EditorUtility.DisplayDialog("失败", message, "确定");
            }
        }));
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

        var pureIds = changedIds.Select(id =>
        {
            if (id.EndsWith(" (新增)")) return id.Substring(0, id.Length - 5);
            if (id.EndsWith(" (已删除)")) return id.Substring(0, id.Length - 6);
            return id;
        }).ToList();

        foreach (var kvp in guidToPath)
        {
            string path = kvp.Value;
            if (!path.EndsWith(".dtree")) continue;

            try
            {
                string jsonContent = File.ReadAllText(path);

                bool hasAffectedId = false;
                foreach (var id in pureIds)
                {
                    if (jsonContent.Contains($"\"{id}\""))
                    {
                        hasAffectedId = true;
                        break;
                    }
                }

                if (hasAffectedId)
                {
                    affectedFiles.Add(Path.GetFileName(path));
                }
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
            var kvp = guidToPath.FirstOrDefault(x => Path.GetFileName(x.Value) == fileName);
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
                    File.WriteAllText(dtreePath, JsonUtility.ToJson(data, true).Trim());

                    // 保存对应的运行时.json文件
                    string runtimePath = Path.ChangeExtension(dtreePath, ".json");
                    SaveRuntimeJson(runtimePath, data);

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

        EditorUtility.DisplayDialog("保存完成", $"成功更新 {savedCount} 个对话树文件 (.dtree + .json)", "确定");
        AssetDatabase.Refresh();

        RefreshAllOpenEditorWindows();
    }

    #endregion

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

}