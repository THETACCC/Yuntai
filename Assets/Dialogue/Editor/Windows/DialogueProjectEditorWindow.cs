using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using DialogueSystem;

/// <summary>
/// 对话项目编辑器主窗口 - 管理对话树、角色库和本地化
/// </summary>
public class DialogueProjectEditorWindow : EditorWindow
{
    // Managers - 业务逻辑层
    private CharacterLibraryManager characterManager;
    private VirtualFolderManager folderManager;
    private LocalizationEditorManager localizationManager;

    // Drawers - UI绘制层
    private CharacterListDrawer characterDrawer;
    private FolderTreeDrawer folderDrawer;
    private LocalizationSettingsDrawer localizationDrawer;

    // UI State
    private Vector2 scrollPos;

    [MenuItem("Tools/Dialogue Tree Manager")]
    public static void ShowWindow()
    {
        var window = GetWindow<DialogueProjectEditorWindow>();
        window.titleContent = new GUIContent("Dialogue Manager");
        window.minSize = new Vector2(400, 500);
        window.Show();
    }

    private void OnEnable()
    {
        // 1. 初始化Managers
        characterManager = new CharacterLibraryManager();
        folderManager = new VirtualFolderManager();
        localizationManager = new LocalizationEditorManager();

        // 2. 加载数据 (必须在初始化Drawers之前！)
        folderManager.LoadVirtualFolderStructure();
        characterManager.LoadCharacterLibrary();
        characterManager.LoadCharacterFolderStructure();
        localizationManager.LoadLocalizationSettings();
        folderManager.ScanAllDialogueTrees();

        // 3. 初始化Drawers (现在数据已经加载好了)
        characterDrawer = new CharacterListDrawer(characterManager);
        folderDrawer = new FolderTreeDrawer(folderManager);
        localizationDrawer = new LocalizationSettingsDrawer(localizationManager);

        // 4. 初始化本地化
        if (!DialogueLocalization.IsLoaded && !string.IsNullOrEmpty(localizationManager.CsvUrlInput))
        {
            DialogueLocalization.LoadInEditorSync();
        }
    }

    private void OnGUI()
    {
        DrawToolbar();

        localizationDrawer.DrawLocalizationSettings();

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        characterDrawer.DrawCharactersSection();

        EditorGUILayout.Space(5);

        folderDrawer.DrawFolderTree();

        EditorGUILayout.EndScrollView();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("Scan Dialogue Trees", EditorStyles.toolbarButton, GUILayout.Width(150)))
        {
            folderManager.ScanAllDialogueTrees();
            Repaint();
        }

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
        {
            folderManager.LoadVirtualFolderStructure();
            folderManager.ScanAllDialogueTrees();
            characterManager.LoadCharacterLibrary();
            characterManager.LoadCharacterFolderStructure();
            Repaint();
        }

        EditorGUILayout.EndHorizontal();
    }
}