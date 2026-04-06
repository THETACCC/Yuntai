using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using DialogueSystem;
using System;

/// <summary>
/// 角色列表UI绘制器 - 负责角色列表的完整可视化和交互
/// </summary>
public class CharacterListDrawer
{
    private CharacterLibraryManager manager;

    // UI状态
    private bool charactersExpanded = true;
    private string editingCharacterId = "";
    private Dictionary<string, Sprite> tempSelectedSprites = new Dictionary<string, Sprite>();
    private Dictionary<string, bool> characterFolderExpandedState = new Dictionary<string, bool>();

    // 拖拽状态
    private CharacterData draggedCharacterForReorder = null;
    private CharacterFolder draggedCharacterFromFolder = null;
    private bool isDraggingCharacterForReorder = false;
    private string insertBeforeCharacterId = null;
    private CharacterFolder insertBeforeCharacterFolder = null;
    private CharacterFolder insertCharacterParentFolder = null;
    private bool insertAfter = false;

    // 缓存的背景texture
    private Texture2D cachedNormalBg;
    private Texture2D cachedFocusedBg;

    public bool CharactersExpanded
    {
        get => charactersExpanded;
        set => charactersExpanded = value;
    }

    public CharacterListDrawer(CharacterLibraryManager manager)
    {
        this.manager = manager;

        // 确保default文件夹展开
        if (!characterFolderExpandedState.ContainsKey("default_character_folder"))
            characterFolderExpandedState["default_character_folder"] = true;

        cachedNormalBg = MakeTex(2, 2, new Color(0.2f, 0.2f, 0.2f, 1f));
        cachedFocusedBg = MakeTex(2, 2, new Color(0.3f, 0.3f, 0.3f, 1f));
    }

    public void DrawCharactersSection()
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

        Rect labelRect = new Rect(rect.x + 25, rect.y + 3, rect.width - 30, rect.height);
        GUI.Label(labelRect, "All Characters", EditorStyles.boldLabel);

        EditorGUILayout.EndVertical();

        HandleCharacterSectionContextMenu(rect);

        if (charactersExpanded && manager.CharacterLibrary != null && manager.CharacterFolderData != null)
        {
            var defaultFolder = manager.CharacterFolderData.rootFolders?.FirstOrDefault(f => f.id == "default_character_folder");
            if (defaultFolder != null)
            {
                // 绘制其他自定义文件夹
                foreach (var folder in manager.CharacterFolderData.rootFolders)
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
                    var character = manager.GetCharacterById(characterId);
                    if (character != null)
                    {
                        int index = System.Array.IndexOf(manager.CharacterLibrary.characters, character);
                        DrawCharacter(character, index, defaultFolder);
                    }
                }
            }

            // 绘制根级别的角色（如果有的话）
            foreach (var characterId in manager.CharacterFolderData.rootCharacterIds.ToList())
            {
                var character = manager.GetCharacterById(characterId);
                if (character != null)
                {
                    int index = System.Array.IndexOf(manager.CharacterLibrary.characters, character);
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
            var defaultFolder = manager.CharacterFolderData?.rootFolders?.FirstOrDefault(f => f.id == "default_character_folder");
            if (defaultFolder != null)
            {
                GenericMenu menu = new GenericMenu();
                menu.AddItem(new GUIContent("+ New Character"), false, () =>
                {
                    CreateNewCharacter();
                });
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
            EditorGUI.DrawRect(insertLineRect, new Color(0.3f, 0.6f, 1f, 0.8f));
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

        Rect labelRect = new Rect(rect.x + 25, rect.y + 3, rect.width - 30, rect.height);
        GUI.Label(labelRect, folder.name, EditorStyles.boldLabel);

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
        HandleCharacterFolderContextMenu(rect, folder, parentFolder);

        bool isExpandedForChildren = characterFolderExpandedState.ContainsKey(folder.id) && characterFolderExpandedState[folder.id];
        if (isExpandedForChildren)
        {
            foreach (var subfolder in folder.subfolders.ToList())
            {
                DrawCharacterFolder(subfolder, indentLevel + 1, folder);
            }

            foreach (var characterId in folder.characterIds.ToList())
            {
                var character = manager.GetCharacterById(characterId);
                if (character != null)
                {
                    int index = System.Array.IndexOf(manager.CharacterLibrary.characters, character);
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
            EditorGUI.DrawRect(insertLineRect, new Color(0.3f, 0.6f, 1f, 0.8f));
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
            EditorGUI.DrawRect(insertLineRect, new Color(0.3f, 0.6f, 1f, 0.8f));
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.BeginVertical();
        GUILayout.Space(2);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(indentLevel * 20 + 25);

        var boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.padding = new RectOffset(8, 8, 8, 8);

        EditorGUILayout.BeginVertical(boxStyle, GUILayout.MinHeight(isEditing ? 230 : 70));

        // 第一行：显示名称和按钮
        EditorGUILayout.BeginHorizontal();

        if (isEditing)
        {
            // 编辑模式：显示 character 字段
            EditorGUILayout.LabelField("Character:", GUILayout.Width(70));

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
            DrawEditingMode(character);
        }
        else
        {
            DrawNonEditingMode(character);
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
            EditorGUI.DrawRect(insertLineRect, new Color(0.3f, 0.6f, 1f, 0.8f));
            EditorGUILayout.EndHorizontal();
        }
    }

    private void DrawEditingMode(CharacterData character)
    {
        EditorGUILayout.Space(3);

        // Name 标题
        EditorGUILayout.LabelField("Name:", EditorStyles.boldLabel);

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
            DrawUseIDMode(character, nameFieldStyle);
        }
        else
        {
            DrawDirectInputMode(character, nameFieldStyle);
        }

        // Avatar选择
        DrawAvatarSelection(character);

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

        // Done 和 Cancel 按钮
        DrawEditingButtons(character);
    }

    private void DrawUseIDMode(CharacterData character, GUIStyle nameFieldStyle)
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

        // Name 预览
        if (!string.IsNullOrEmpty(character.nameId))
        {
            if (DialogueLocalization.IsLoaded && DialogueLocalization.HasId(character.nameId))
            {
                var locData = DialogueLocalization.GetAllLanguages(character.nameId);
                if (locData != null)
                {
                    var previewStyle = new GUIStyle(EditorStyles.label);
                    previewStyle.normal.textColor = new Color(0.7f, 0.9f, 0.7f);
                    previewStyle.wordWrap = true;

                    if (locData.ContainsKey(Language.ChineseSimplified))
                    {
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Space(15);
                        EditorGUILayout.LabelField($"中文: {locData[Language.ChineseSimplified]}", previewStyle);
                        EditorGUILayout.EndHorizontal();
                    }

                    if (locData.ContainsKey(Language.English))
                    {
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Space(15);
                        EditorGUILayout.LabelField($"EN: {locData[Language.English]}", previewStyle);
                        EditorGUILayout.EndHorizontal();
                    }

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
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(15);
                var errorStyle = new GUIStyle(EditorStyles.label);
                errorStyle.normal.textColor = new Color(1f, 0.5f, 0.5f);
                EditorGUILayout.LabelField($"[错误: ID '{character.nameId}' 不存在]", errorStyle);
                EditorGUILayout.EndHorizontal();
            }
        }
    }

    private void DrawDirectInputMode(CharacterData character, GUIStyle nameFieldStyle)
    {
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

    private void DrawAvatarSelection(CharacterData character)
    {
        EditorGUILayout.Space(5);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Avatar:", GUILayout.Width(70));

        // 获取当前显示的sprite
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
            if (newSprite != null)
            {
                tempSelectedSprites[character.id] = newSprite;

                string assetPath = AssetDatabase.GetAssetPath(newSprite);

                if (assetPath.Contains("/Resources/"))
                {
                    character.avatarAssetPath = assetPath;
                }
            }
            else
            {
                tempSelectedSprites.Remove(character.id);
                character.avatarAssetPath = "";
            }
        }

        EditorGUILayout.EndHorizontal();

        // 显示状态提示
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(70);

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
            var errorStyle = new GUIStyle(EditorStyles.miniLabel);
            errorStyle.normal.textColor = new Color(1f, 0.3f, 0.3f);
            EditorGUILayout.LabelField("ERROR: Not in a Resources folder!", errorStyle);
        }
        else
        {
            var successStyle = new GUIStyle(EditorStyles.miniLabel);
            successStyle.normal.textColor = new Color(0.3f, 1f, 0.3f);
            EditorGUILayout.LabelField($"OK: {System.IO.Path.GetFileName(currentPath)}", successStyle);
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawEditingButtons(CharacterData character)
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Cancel", EditorStyles.miniButton, GUILayout.Width(70)))
        {
            editingCharacterId = "";
            tempSelectedSprites.Remove(character.id);
            manager.LoadCharacterLibrary();
        }

        GUI.backgroundColor = new Color(0.7f, 1f, 0.7f);
        if (GUILayout.Button("Done", EditorStyles.miniButton, GUILayout.Width(70)))
        {
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
                string savedCharacterId = character.id;
                manager.SaveCharacterLibraryInternal();
                Debug.Log($"✓ Saved character: {character.character}");
                editingCharacterId = "";
                tempSelectedSprites.Remove(character.id);

                // 刷新打开的编辑器窗口，并重新生成受影响的运行时 JSON
                EditorApplication.delayCall += () =>
                {
                    DialogueTreeEditor.RefreshAllOpenEditors();
                    DialogueProjectEditorWindow.OnCharacterSaved(savedCharacterId);
                };
            }
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndHorizontal();
    }

    private void DrawNonEditingMode(CharacterData character)
    {
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

        // 显示 Name 字段
        var nameTitleStyle = new GUIStyle(EditorStyles.miniLabel);
        nameTitleStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f);
        nameTitleStyle.fontStyle = FontStyle.Bold;

        var nameStyle = new GUIStyle(EditorStyles.miniLabel);
        nameStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f);

        // 如果是 useNameId 模式，从本地化表实时读取显示
        LocalizedText displayName = character.characterName;
        if (character.useNameId && !string.IsNullOrEmpty(character.nameId) &&
            DialogueLocalization.IsLoaded && DialogueLocalization.HasId(character.nameId))
        {
            var locData = DialogueLocalization.GetAllLanguages(character.nameId);
            if (locData != null)
            {
                displayName = new LocalizedText
                {
                    en = locData.ContainsKey(Language.English) ? locData[Language.English] : "",
                    zh = locData.ContainsKey(Language.ChineseSimplified) ? locData[Language.ChineseSimplified] : "",
                    ja = locData.ContainsKey(Language.Japanese) ? locData[Language.Japanese] : ""
                };
            }
        }

        bool hasAnyName = !string.IsNullOrEmpty(displayName?.en) ||
                         !string.IsNullOrEmpty(displayName?.zh) ||
                         !string.IsNullOrEmpty(displayName?.ja);

        if (hasAnyName)
        {
            EditorGUILayout.LabelField("Name:", nameTitleStyle);

            if (!string.IsNullOrEmpty(displayName?.en))
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(10);
                EditorGUILayout.LabelField($"EN: {displayName.en}", nameStyle);
                EditorGUILayout.EndHorizontal();
            }

            if (!string.IsNullOrEmpty(displayName?.zh))
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(10);
                EditorGUILayout.LabelField($"中文: {displayName.zh}", nameStyle);
                EditorGUILayout.EndHorizontal();
            }

            if (!string.IsNullOrEmpty(displayName?.ja))
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(10);
                EditorGUILayout.LabelField($"日本語: {displayName.ja}", nameStyle);
                EditorGUILayout.EndHorizontal();
            }
        }

        var pathStyle = new GUIStyle(EditorStyles.miniLabel);
        pathStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
        string displayPath = string.IsNullOrEmpty(character.avatarAssetPath) ?
            "(No avatar)" : System.IO.Path.GetFileName(character.avatarAssetPath);
        EditorGUILayout.LabelField(displayPath, pathStyle);

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();
    }

    #region 拖拽和排序

    private void HandleCharacterDrag(Rect rect, CharacterData character, CharacterFolder parentFolder)
    {
        Event e = Event.current;

        if (e.type == EventType.MouseDown && rect.Contains(e.mousePosition) && e.button == 0)
        {
            float buttonAreaWidth = 130;
            Rect buttonArea = new Rect(rect.xMax - buttonAreaWidth, rect.y, buttonAreaWidth, rect.height);
            if (!buttonArea.Contains(e.mousePosition))
            {
                draggedCharacterForReorder = character;
                draggedCharacterFromFolder = parentFolder;
                isDraggingCharacterForReorder = false;
            }
        }

        if (e.type == EventType.MouseDrag && draggedCharacterForReorder == character && !isDraggingCharacterForReorder)
        {
            DragAndDrop.PrepareStartDrag();
            DragAndDrop.SetGenericData("ReorderCharacter", character);
            DragAndDrop.SetGenericData("CharacterData", character);
            DragAndDrop.StartDrag("Dragging Character");
            isDraggingCharacterForReorder = true;
            e.Use();
        }

        if (e.type == EventType.DragExited || e.type == EventType.MouseUp)
        {
            if (draggedCharacterForReorder != null)
            {
                draggedCharacterForReorder = null;
                draggedCharacterFromFolder = null;
                isDraggingCharacterForReorder = false;
                insertBeforeCharacterId = null;
                insertAfter = false;
            }
        }
    }

    private void HandleCharacterDropForReorder(Rect rect, CharacterData character)
    {
        if (draggedCharacterForReorder == null || draggedCharacterForReorder == character) return;

        Event e = Event.current;
        Rect expandedRect = new Rect(rect.x, rect.y - 10, rect.width, rect.height + 20);

        if (expandedRect.Contains(e.mousePosition))
        {
            if (e.type == EventType.DragUpdated)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Move;

                float mouseY = e.mousePosition.y;
                float rectMiddle = rect.y + rect.height / 2;
                bool shouldInsertAfter = mouseY > rectMiddle;

                insertBeforeCharacterId = character.id;
                insertAfter = shouldInsertAfter;

                e.Use();
            }
            else if (e.type == EventType.DragPerform)
            {
                float mouseY = e.mousePosition.y;
                float rectMiddle = rect.y + rect.height / 2;
                bool shouldInsertAfter = mouseY > rectMiddle;

                ReorderCharacter(draggedCharacterForReorder, character, shouldInsertAfter);
                DragAndDrop.AcceptDrag();
                draggedCharacterForReorder = null;
                isDraggingCharacterForReorder = false;

                insertBeforeCharacterId = null;
                insertAfter = false;

                e.Use();
            }
        }
    }

    private void ReorderCharacter(CharacterData sourceCharacter, CharacterData targetCharacter, bool insertAfter)
    {
        var sourceFolder = FindCharacterParentFolder(sourceCharacter.id);
        var targetFolder = FindCharacterParentFolder(targetCharacter.id);

        if (sourceFolder != targetFolder)
            return;

        List<string> list = sourceFolder == null ? manager.CharacterFolderData.rootCharacterIds : sourceFolder.characterIds;

        int sourceIndex = list.IndexOf(sourceCharacter.id);
        int targetIndex = list.IndexOf(targetCharacter.id);

        if (sourceIndex != -1 && targetIndex != -1 && sourceIndex != targetIndex)
        {
            list.RemoveAt(sourceIndex);
            targetIndex = list.IndexOf(targetCharacter.id);

            if (insertAfter)
            {
                targetIndex++;
            }

            list.Insert(targetIndex, sourceCharacter.id);
            manager.SaveCharacterFolderStructure();
        }
    }

    private CharacterFolder FindCharacterParentFolder(string characterId)
    {
        if (manager.CharacterFolderData.rootCharacterIds.Contains(characterId))
            return null;

        return FindCharacterParentFolderRecursive(manager.CharacterFolderData.rootFolders, characterId);
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

    #endregion

    #region 文件夹操作

    private void HandleCharacterFolderDragAndDrop(Rect rect, CharacterFolder folder)
    {
        Event e = Event.current;

        if (rect.Contains(e.mousePosition))
        {
            if (e.type == EventType.DragUpdated)
            {
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

    private void HandleCharacterFolderContextMenu(Rect rect, CharacterFolder folder, CharacterFolder parentFolder)
    {
        Event e = Event.current;

        if (e.type == EventType.ContextClick && rect.Contains(e.mousePosition))
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("New Folder"), false, () => CreateCharacterFolder(folder));
            
            if (folder.id != "default_character_folder")
            {
                menu.AddItem(new GUIContent("Rename"), false, () => RenameCharacterFolder(folder));
                menu.AddItem(new GUIContent("Delete"), false, () =>
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
                });
            }
            
            menu.ShowAsContext();
            e.Use();
        }
    }

    private void MoveCharacterToFolder(string characterId, CharacterFolder fromFolder, CharacterFolder toFolder)
    {
        if (fromFolder == null)
        {
            manager.CharacterFolderData.rootCharacterIds.Remove(characterId);
        }
        else
        {
            fromFolder.characterIds.Remove(characterId);
        }

        if (!toFolder.characterIds.Contains(characterId))
        {
            toFolder.characterIds.Add(characterId);
        }

        manager.SaveCharacterFolderStructure();
    }

    private void CreateCharacterFolder(CharacterFolder parent)
    {
        string folderName = "New Folder";
        int counter = 1;

        var existingNames = parent == null
            ? manager.CharacterFolderData.rootFolders.Select(f => f.name).ToList()
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
            id = Guid.NewGuid().ToString()
        };

        if (parent == null)
        {
            manager.CharacterFolderData.rootFolders.Add(newFolder);
        }
        else
        {
            parent.subfolders.Add(newFolder);
        }

        manager.SaveCharacterFolderStructure();
    }

    private void DeleteCharacterFolder(CharacterFolder folder, CharacterFolder parent)
    {
        List<string> allCharacterIds = new List<string>();
        CollectAllCharactersRecursive(folder, allCharacterIds);

        if (parent == null)
        {
            manager.CharacterFolderData.rootFolders.Remove(folder);
        }
        else
        {
            parent.subfolders.Remove(folder);
        }

        var defaultFolder = manager.CharacterFolderData.rootFolders.FirstOrDefault(f => f.id == "default_character_folder");
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

        manager.SaveCharacterFolderStructure();
    }

    private void CollectAllCharactersRecursive(CharacterFolder folder, List<string> characterList)
    {
        characterList.AddRange(folder.characterIds);

        foreach (var subfolder in folder.subfolders)
        {
            CollectAllCharactersRecursive(subfolder, characterList);
        }
    }

    private void RenameCharacterFolder(CharacterFolder folder)
    {
        EditorApplication.delayCall += () =>
        {
            EditorInputDialogue.ShowAsync("Rename Folder", "Enter new folder name:", folder.name, (newName) =>
            {
                if (!string.IsNullOrWhiteSpace(newName))
                {
                    folder.name = newName.Trim();
                    manager.SaveCharacterFolderStructure();
                }
            });
        };
    }

    private void EditCharacterDescription(CharacterFolder folder)
    {
        EditorApplication.delayCall += () =>
        {
            EditorInputDialogue.ShowAsync("Edit Description", "Enter folder description:", folder.description, (newDesc) =>
            {
                if (newDesc != null)
                {
                    folder.description = newDesc.Trim();
                    manager.SaveCharacterFolderStructure();
                }
            });
        };
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
        return FindCharacterFolderParentRecursive(manager.CharacterFolderData.rootFolders, targetFolder);
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

    #endregion

    #region 角色CRUD

    private void CreateNewCharacter()
    {
        manager.CreateNewCharacter();

        // 自动进入编辑模式
        if (manager.CharacterLibrary.characters != null && manager.CharacterLibrary.characters.Length > 0)
        {
            var newChar = manager.CharacterLibrary.characters[manager.CharacterLibrary.characters.Length - 1];
            editingCharacterId = newChar.id;
        }
    }

    private void DeleteCharacter(int index)
    {
        if (EditorUtility.DisplayDialog("Delete Character",
            $"Delete character '{manager.CharacterLibrary.characters[index].characterName?.en ?? "Unknown"}'?\n\nNote: Dialogue nodes using this character will show 'Unknown Character'.",
            "Delete", "Cancel"))
        {
            string deletedCharacterId = manager.CharacterLibrary.characters[index].id;

            var list = new List<CharacterData>(manager.CharacterLibrary.characters);
            list.RemoveAt(index);
            manager.CharacterLibrary.characters = list.ToArray();

            tempSelectedSprites.Remove(deletedCharacterId);

            manager.SaveCharacterLibraryInternal();

            CleanupCharacterFromFolders(deletedCharacterId);

            EditorApplication.delayCall += () =>
            {
                DialogueTreeEditor.RefreshAllOpenEditors();
            };
        }
    }

    private void CleanupCharacterFromFolders(string characterId)
    {
        manager.CharacterFolderData.rootCharacterIds.Remove(characterId);
        CleanupCharacterFromFoldersRecursive(manager.CharacterFolderData.rootFolders, characterId);
        manager.SaveCharacterFolderStructure();
    }

    private void CleanupCharacterFromFoldersRecursive(List<CharacterFolder> folders, string characterId)
    {
        foreach (var folder in folders)
        {
            folder.characterIds.Remove(characterId);
            CleanupCharacterFromFoldersRecursive(folder.subfolders, characterId);
        }
    }

    #endregion

    #region 辅助方法

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

    #endregion
}