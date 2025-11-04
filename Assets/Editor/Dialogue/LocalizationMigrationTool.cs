using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using DialogueSystem;

/// <summary>
/// 本地化数据迁移工具 - 将旧格式的string转换为LocalizedText
/// </summary>
public class LocalizationMigrationTool : EditorWindow
{
    private string folderPath = "Assets/StreamingAssets";
    private Vector2 scrollPos;
    private List<string> logMessages = new List<string>();
    private bool includeCharacters = true;

    [MenuItem("Tools/Dialogue Tree Editor/Localization Migration Tool")]
    public static void ShowWindow()
    {
        var window = GetWindow<LocalizationMigrationTool>("Localization Migration");
        window.minSize = new Vector2(500, 400);
        window.Show();
    }

    private void OnGUI()
    {
        GUILayout.Label("数据迁移工具 - 将旧数据转换为本地化格式", EditorStyles.boldLabel);
        GUILayout.Space(10);

        EditorGUILayout.HelpBox(
            "此工具会将旧格式的对话树数据（string）转换为新的本地化格式（LocalizedText）。\n" +
            "原有的英文内容会被保留在English字段中。\n\n" +
            "⚠️ 建议先备份数据！",
            MessageType.Warning);

        GUILayout.Space(10);

        // 文件夹选择
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("对话树文件夹:", GUILayout.Width(100));
        folderPath = EditorGUILayout.TextField(folderPath);
        if (GUILayout.Button("Browse", GUILayout.Width(80)))
        {
            string path = EditorUtility.OpenFolderPanel("Select Dialogue Folder", folderPath, "");
            if (!string.IsNullOrEmpty(path))
            {
                folderPath = path;
            }
        }
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(5);

        // 选项
        includeCharacters = EditorGUILayout.ToggleLeft("同时迁移角色库数据", includeCharacters);

        GUILayout.Space(10);

        // 迁移按钮
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("开始迁移数据", GUILayout.Height(40)))
        {
            MigrateAllData();
        }
        GUI.backgroundColor = Color.white;

        GUILayout.Space(10);

        // 日志显示
        EditorGUILayout.LabelField("迁移日志:", EditorStyles.boldLabel);
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(200));
        foreach (var msg in logMessages)
        {
            EditorGUILayout.LabelField(msg, EditorStyles.wordWrappedLabel);
        }
        EditorGUILayout.EndScrollView();

        GUILayout.Space(10);
        if (GUILayout.Button("清除日志"))
        {
            logMessages.Clear();
        }
    }

    private void MigrateAllData()
    {
        logMessages.Clear();
        Log("========== 开始迁移 ==========");

        if (!Directory.Exists(folderPath))
        {
            Log($"❌ 文件夹不存在: {folderPath}");
            return;
        }

        int successCount = 0;
        int failCount = 0;

        // 迁移所有 .dtree 文件
        string[] dtreeFiles = Directory.GetFiles(folderPath, "*.dtree", SearchOption.AllDirectories);
        Log($"找到 {dtreeFiles.Length} 个 .dtree 文件");

        foreach (string dtreePath in dtreeFiles)
        {
            if (MigrateDialogueTreeFile(dtreePath))
                successCount++;
            else
                failCount++;
        }

        // 迁移角色库
        if (includeCharacters)
        {
            if (MigrateCharacterLibrary())
            {
                Log("✅ 角色库迁移成功");
            }
            else
            {
                Log("⚠️ 未找到角色库或迁移失败");
            }
        }

        Log("========== 迁移完成 ==========");
        Log($"成功: {successCount} 个文件");
        Log($"失败: {failCount} 个文件");

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("迁移完成",
            $"成功迁移 {successCount} 个对话树文件\n失败 {failCount} 个",
            "OK");
    }

    private bool MigrateDialogueTreeFile(string filePath)
    {
        try
        {
            Log($"\n处理文件: {Path.GetFileName(filePath)}");

            string jsonContent = File.ReadAllText(filePath);

            // 使用旧格式反序列化
            var oldData = JsonUtility.FromJson<OldDialogueTreeData>(jsonContent);

            if (oldData == null || oldData.nodes == null)
            {
                Log($"  ❌ 无法读取文件数据");
                return false;
            }

            // 转换为新格式
            var newData = new DialogueTreeData
            {
                nodes = new List<DialogueNodeData>(),
                connections = oldData.connections
            };

            foreach (var oldNode in oldData.nodes)
            {
                var newNode = new DialogueNodeData
                {
                    id = oldNode.id,
                    index = oldNode.index,
                    characterId = oldNode.characterId,
                    content = new LocalizedText { en = oldNode.content ?? "" },
                    positionX = oldNode.positionX,
                    positionY = oldNode.positionY,
                    choices = new List<ChoiceData>(),
                    eventCalls = oldNode.eventCalls,
                    conditionalBranches = oldNode.conditionalBranches
                };

                // 转换选项
                if (oldNode.choices != null)
                {
                    foreach (var oldChoice in oldNode.choices)
                    {
                        var newChoice = new ChoiceData
                        {
                            text = new LocalizedText { en = oldChoice.text ?? "" },
                            conditions = oldChoice.conditions,
                            conditionLogic = oldChoice.conditionLogic
                        };
                        newNode.choices.Add(newChoice);
                    }
                }

                newData.nodes.Add(newNode);
            }

            // 保存新格式
            string newJson = JsonUtility.ToJson(newData, true);
            File.WriteAllText(filePath, newJson);

            Log($"  ✅ 成功迁移 {oldData.nodes.Count} 个节点");
            return true;
        }
        catch (System.Exception e)
        {
            Log($"  ❌ 错误: {e.Message}");
            return false;
        }
    }

    private bool MigrateCharacterLibrary()
    {
        string libraryPath = "Assets/Resources/CharacterLibrary.json";

        if (!File.Exists(libraryPath))
        {
            // 尝试其他可能的位置
            string[] possiblePaths = {
                "Assets/StreamingAssets/CharacterLibrary.json",
                "Assets/Dialogues/CharacterLibrary.json"
            };

            foreach (string path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    libraryPath = path;
                    break;
                }
            }
        }

        if (!File.Exists(libraryPath))
        {
            return false;
        }

        try
        {
            Log($"\n处理角色库: {libraryPath}");

            string jsonContent = File.ReadAllText(libraryPath);
            var oldLibrary = JsonUtility.FromJson<OldCharacterLibraryData>(jsonContent);

            if (oldLibrary == null || oldLibrary.characters == null)
            {
                return false;
            }

            var newLibrary = new CharacterLibraryData
            {
                characters = new CharacterData[oldLibrary.characters.Length]
            };

            for (int i = 0; i < oldLibrary.characters.Length; i++)
            {
                var oldChar = oldLibrary.characters[i];
                newLibrary.characters[i] = new CharacterData
                {
                    id = oldChar.id,
                    character = oldChar.character,
                    characterName = new LocalizedText { en = oldChar.characterName ?? "" },
                    avatarAssetPath = oldChar.avatarAssetPath
                };
            }

            string newJson = JsonUtility.ToJson(newLibrary, true);
            File.WriteAllText(libraryPath, newJson);

            Log($"  ✅ 成功迁移 {oldLibrary.characters.Length} 个角色");
            return true;
        }
        catch (System.Exception e)
        {
            Log($"  ❌ 角色库迁移错误: {e.Message}");
            return false;
        }
    }

    private void Log(string message)
    {
        logMessages.Add(message);
        Debug.Log($"[Migration] {message}");
        Repaint();
    }

    // ==================== 旧格式数据结构 ====================

    [System.Serializable]
    private class OldDialogueTreeData
    {
        public List<OldDialogueNodeData> nodes;
        public List<DialogueConnectionData> connections;
    }

    [System.Serializable]
    private class OldDialogueNodeData
    {
        public string id;
        public int index;
        public string characterId;
        public string content;  // 旧格式：string
        public float positionX;
        public float positionY;
        public List<OldChoiceData> choices;
        public List<DialogueEventCall> eventCalls;
        public List<ConditionalBranchData> conditionalBranches;
    }

    [System.Serializable]
    private class OldChoiceData
    {
        public string text;  // 旧格式：string
        public List<ChoiceCondition> conditions;
        public ConditionLogic conditionLogic;
    }

    [System.Serializable]
    private class OldCharacterLibraryData
    {
        public OldCharacterData[] characters;
    }

    [System.Serializable]
    private class OldCharacterData
    {
        public string id;
        public string character;
        public string characterName;  // 旧格式：string
        public string avatarAssetPath;
    }
}