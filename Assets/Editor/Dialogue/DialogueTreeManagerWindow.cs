using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// 简单的输入对话框辅助类
public class EditorInputDialog : EditorWindow
{
    private string inputText = "";
    private string dialogTitle = "";
    private string message = "";

    public static string Show(string title, string message, string defaultValue = "")
    {
        var window = CreateInstance<EditorInputDialog>();
        window.titleContent = new GUIContent(title);
        window.dialogTitle = title;
        window.message = message;
        window.inputText = defaultValue;
        window.minSize = new Vector2(300, 100);
        window.maxSize = new Vector2(300, 100);

        window.ShowModal();

        return window.inputText;
    }

    private void OnGUI()
    {
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
        public string description = ""; // 文件夹描述
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
    private Dictionary<string, string> guidToPath = new Dictionary<string, string>();
    private Vector2 scrollPos;
    private VirtualFolder draggedFromFolder;
    private string draggedFileGuid;

    private string GetFolderStructurePath()
    {
        // 找到当前脚本文件的路径
        var script = MonoScript.FromScriptableObject(this);
        string scriptPath = AssetDatabase.GetAssetPath(script);

        if (string.IsNullOrEmpty(scriptPath))
        {
            // 如果找不到，使用默认路径
            return "Assets/Editor/Dialogue/DialogueTreeFolderStructure.json";
        }

        // 获取脚本所在的文件夹
        string scriptFolder = Path.GetDirectoryName(scriptPath);

        // 在同一文件夹下保存
        return Path.Combine(scriptFolder, "DialogueTreeFolderStructure.json");
    }

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
        ScanAllDialogueTrees();
    }

    private void OnDisable()
    {
        SaveVirtualFolderStructure();
    }

    private void OnGUI()
    {
        DrawToolbar();

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

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
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
        {
            ScanAllDialogueTrees();
        }

        if (GUILayout.Button("New Folder", EditorStyles.toolbarButton, GUILayout.Width(80)))
        {
            CreateVirtualFolder(null);
        }

        if (GUILayout.Button("Export", EditorStyles.toolbarButton, GUILayout.Width(60)))
        {
            ExportAllToCSV();
        }

        // DISABLED: Import feature temporarily disabled due to data loss risk
        /*
        GUI.backgroundColor = new Color(0.7f, 1f, 0.7f);
        if (GUILayout.Button("Import", EditorStyles.toolbarButton, GUILayout.Width(60)))
        {
            ImportAllFromCSV();
        }
        GUI.backgroundColor = Color.white;
        */

        GUILayout.FlexibleSpace();

        int fileCount = guidToPath.Count;
        EditorGUILayout.LabelField($"Files: {fileCount}", EditorStyles.miniLabel);

        EditorGUILayout.EndHorizontal();
    }

    private void DrawVirtualFolder(VirtualFolder folder, int indentLevel, VirtualFolder parentFolder)
    {
        EditorGUILayout.BeginVertical();

        // 第一行：文件夹名称和按钮
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
        GUI.Label(labelRect, $"📁 {folder.name}", EditorStyles.boldLabel);

        if (folder.id != "default_folder")
        {
            Rect renameRect = new Rect(rect.xMax - 195, rect.y + 2, 60, 18);
            if (GUI.Button(renameRect, "Rename", EditorStyles.miniButton))
            {
                RenameFolder(folder);
            }

            Rect newFolderRect = new Rect(rect.xMax - 130, rect.y + 2, 60, 18);
            if (GUI.Button(newFolderRect, "New", EditorStyles.miniButton))
            {
                CreateVirtualFolder(folder);
            }

            Rect deleteRect = new Rect(rect.xMax - 65, rect.y + 2, 60, 18);
            GUI.backgroundColor = new Color(1f, 0.7f, 0.7f);
            if (GUI.Button(deleteRect, "Del", EditorStyles.miniButton))
            {
                if (EditorUtility.DisplayDialog("Delete Folder",
                    $"Delete folder '{folder.name}'? All files will move to 'All Dialogues' folder.",
                    "Delete", "Cancel"))
                {
                    DeleteVirtualFolder(folder, parentFolder);
                }
            }
            GUI.backgroundColor = Color.white;
        }
        else
        {
            Rect newFolderRect = new Rect(rect.xMax - 65, rect.y + 2, 60, 18);
            if (GUI.Button(newFolderRect, "New", EditorStyles.miniButton))
            {
                CreateVirtualFolder(folder);
            }
        }

        EditorGUILayout.EndHorizontal();

        // 第二行：Description（如果有）
        if (!string.IsNullOrEmpty(folder.description))
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(indentLevel * 20 + 25); // 与文件夹名对齐

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

            // 检测双击编辑描述
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
            // 没有描述时，显示"Add description..."提示
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
    }

    private void EditDescription(VirtualFolder folder)
    {
        string newDesc = EditorInputDialog.Show("Edit Description", "Enter folder description:", folder.description);

        if (newDesc != null) // null 表示取消，空字符串表示清空
        {
            folder.description = newDesc.Trim();
            SaveVirtualFolderStructure();
        }
    }

    private void DrawFile(string guid, int indentLevel, VirtualFolder parentFolder)
    {
        if (!guidToPath.ContainsKey(guid)) return;

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
        GUI.Label(labelRect, $"📄 {fileName}");

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
    }

    private void RenameFolder(VirtualFolder folder)
    {
        string newName = EditorInputDialog.Show("Rename Folder", "Enter new folder name:", folder.name);

        if (!string.IsNullOrWhiteSpace(newName))
        {
            folder.name = newName.Trim();
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

    private void HandleFolderDragAndDrop(Rect rect, VirtualFolder folder)
    {
        Event e = Event.current;

        if (rect.Contains(e.mousePosition))
        {
            if (e.type == EventType.DragUpdated)
            {
                if (!string.IsNullOrEmpty(draggedFileGuid))
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Move;
                    e.Use();
                }
            }
            else if (e.type == EventType.DragPerform)
            {
                if (!string.IsNullOrEmpty(draggedFileGuid))
                {
                    MoveFileToFolder(draggedFileGuid, draggedFromFolder, folder);
                    DragAndDrop.AcceptDrag();
                    draggedFileGuid = null;
                    draggedFromFolder = null;
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

    private void CollectAllFilesRecursive(VirtualFolder folder, List<string> fileList)
    {
        fileList.AddRange(folder.fileGuids);

        foreach (var subfolder in folder.subfolders)
        {
            CollectAllFilesRecursive(subfolder, fileList);
        }
    }

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

            // 确保文件夹存在
            string folder = Path.GetDirectoryName(savePath);
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            File.WriteAllText(savePath, json);
            AssetDatabase.Refresh();

            Debug.Log($"Saved folder structure to: {savePath}");
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
                Debug.Log($"Loaded folder structure from: {loadPath}");
            }
            else
            {
                folderData = new VirtualFolderData();
                Debug.Log("No existing folder structure found, created new one");
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
                    var method = typeof(DialogueTreeEditor).GetMethod("LoadFromFile",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                    if (method != null)
                    {
                        method.Invoke(window, new object[] { dtreePath });
                        Debug.Log($"Opened dialogue tree: {Path.GetFileName(dtreePath)}");
                    }
                };
            }
        };
    }

    // ========== Export/Import Functions ==========

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

                if (folderData.rootFileGuids.Count > 0)
                {
                    if (!isFirst)
                    {
                        writer.WriteLine("");
                        writer.WriteLine("---");
                        writer.WriteLine("");
                    }

                    writer.WriteLine("FOLDER: Root");
                    foreach (var guid in folderData.rootFileGuids)
                    {
                        if (guidToPath.ContainsKey(guid))
                        {
                            ExportSingleFileToWriter(writer, guidToPath[guid], 1);
                        }
                    }
                }
            }

            EditorUtility.DisplayDialog("Export Successful",
                $"Exported all dialogues to:\n{savePath}", "OK");
            Debug.Log($"Exported all to: {savePath}");
        }
        catch (System.Exception e)
        {
            EditorUtility.DisplayDialog("Export Failed", $"Error: {e.Message}", "OK");
            Debug.LogError($"Export all failed: {e.Message}");
        }
    }

    private void ExportFolderRecursive(StreamWriter writer, VirtualFolder folder, string parentPath, int indentLevel)
    {
        string folderPath = string.IsNullOrEmpty(parentPath) ? folder.name : $"{parentPath}/{folder.name}";

        string indent = new string(',', indentLevel);

        // 写入文件夹名
        writer.WriteLine($"{indent}FOLDER: {folderPath}");

        // 如果有描述，写入描述（下一行）
        if (!string.IsNullOrEmpty(folder.description))
        {
            writer.WriteLine($"{indent}Description: {EscapeCSV(folder.description)}");
        }

        // 空一行
        writer.WriteLine();

        // 写入文件
        foreach (var guid in folder.fileGuids)
        {
            if (guidToPath.ContainsKey(guid))
            {
                ExportSingleFileToWriter(writer, guidToPath[guid], indentLevel + 1);
            }
        }

        // 递归导出子文件夹
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
        // 修改：使用 ChoiceData 的 text 属性
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
            writer.Write($"\"{EscapeCSV(node.name)}\",");
            writer.Write($"\"{EscapeCSV(node.content)}\"");

            for (int i = 0; i < maxChoices; i++)
            {
                if (node.choices != null && i < node.choices.Count)
                {
                    // 修改：从 ChoiceData 获取 text
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
    }

    private void ImportAllFromCSV()
    {
        string csvPath = EditorUtility.OpenFilePanel("Import All Dialogues from CSV",
            Application.dataPath, "csv");

        if (string.IsNullOrEmpty(csvPath)) return;

        if (!EditorUtility.DisplayDialog("Import CSV",
            "This will update dialogue content from CSV for ALL files.\nNode positions and connections will be preserved.\nContinue?",
            "Import", "Cancel"))
        {
            return;
        }

        try
        {
            var lines = File.ReadAllLines(csvPath, System.Text.Encoding.UTF8);

            int updatedCount = 0;
            string currentFileName = "";
            var currentFileData = new List<string>();

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();

                if (string.IsNullOrEmpty(line) || line.StartsWith("===") || line.StartsWith("---"))
                    continue;

                if (line.Contains("FOLDER:"))
                    continue;

                if (line.Contains("FILE:"))
                {
                    if (!string.IsNullOrEmpty(currentFileName) && currentFileData.Count > 0)
                    {
                        if (ImportFileData(currentFileName, currentFileData))
                        {
                            updatedCount++;
                        }
                    }

                    currentFileName = ExtractFileName(line);
                    currentFileData.Clear();
                    continue;
                }

                if (!string.IsNullOrEmpty(currentFileName))
                {
                    currentFileData.Add(line);
                }
            }

            if (!string.IsNullOrEmpty(currentFileName) && currentFileData.Count > 0)
            {
                if (ImportFileData(currentFileName, currentFileData))
                {
                    updatedCount++;
                }
            }

            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Import Successful",
                $"Updated {updatedCount} dialogue files from CSV.", "OK");
            Debug.Log($"Import completed: {updatedCount} files updated");
        }
        catch (System.Exception e)
        {
            EditorUtility.DisplayDialog("Import Failed", $"Error: {e.Message}", "OK");
            Debug.LogError($"Import all failed: {e.Message}");
        }
    }

    private string ExtractFileName(string line)
    {
        string cleaned = line.Replace(",", "").Replace("FILE:", "").Trim();
        return cleaned;
    }

    private bool ImportFileData(string fileName, List<string> dataLines)
    {
        string targetFilePath = null;
        foreach (var kvp in guidToPath)
        {
            string name = Path.GetFileNameWithoutExtension(kvp.Value);
            if (name == fileName)
            {
                targetFilePath = kvp.Value;
                break;
            }
        }

        if (string.IsNullOrEmpty(targetFilePath))
        {
            Debug.LogWarning($"File not found: {fileName}");
            return false;
        }

        // 保存原始JSON以备份
        string originalJson = File.ReadAllText(targetFilePath);

        var treeData = LoadDialogueTreeData(targetFilePath);
        if (treeData == null)
        {
            Debug.LogWarning($"Failed to load tree data: {fileName}");
            return false;
        }

        // 创建节点索引映射
        var nodeMap = new Dictionary<int, DialogueNodeData>();
        foreach (var node in treeData.nodes)
        {
            nodeMap[node.index] = node;
        }

        bool isHeader = true;
        bool hasChanges = false;

        foreach (var line in dataLines)
        {
            if (isHeader && line.Contains("NodeIndex"))
            {
                isHeader = false;
                continue;
            }

            if (string.IsNullOrWhiteSpace(line)) continue;

            var fields = ParseCSVLine(line);
            if (fields.Count < 3) continue;

            while (fields.Count > 0 && string.IsNullOrWhiteSpace(fields[0]))
            {
                fields.RemoveAt(0);
            }

            if (fields.Count < 3) continue;

            int nodeIndex;
            if (!int.TryParse(fields[0], out nodeIndex)) continue;

            if (nodeMap.ContainsKey(nodeIndex))
            {
                var node = nodeMap[nodeIndex];

                // 只更新这三个字段，其他字段（如avatarAssetPath）保持不变
                node.name = fields[1];
                node.content = fields[2];

                // 修改：更新选项时创建 ChoiceData 对象
                node.choices.Clear();
                for (int j = 3; j < fields.Count; j += 2)
                {
                    if (!string.IsNullOrWhiteSpace(fields[j]))
                    {
                        // 创建新的 ChoiceData，保留原有条件（如果存在）
                        var choiceData = new ChoiceData
                        {
                            text = fields[j],
                            conditions = new List<ChoiceCondition>(), // 导入时清空条件
                            conditionLogic = ConditionLogic.AND
                        };
                        node.choices.Add(choiceData);
                    }
                }

                hasChanges = true;
            }
        }

        if (!hasChanges)
        {
            Debug.LogWarning($"No changes detected for: {fileName}");
            return false;
        }

        // 保存更新后的数据
        try
        {
            string json = JsonUtility.ToJson(treeData, true);
            File.WriteAllText(targetFilePath, json);
            Debug.Log($"Updated: {fileName}");
            return true;
        }
        catch (System.Exception e)
        {
            // 如果保存失败，恢复原始文件
            Debug.LogError($"Failed to save {fileName}: {e.Message}");
            File.WriteAllText(targetFilePath, originalJson);
            return false;
        }
    }

    private List<string> ParseCSVLine(string line)
    {
        var fields = new List<string>();
        bool inQuotes = false;
        string currentField = "";

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
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
        return fields;
    }

    private string EscapeCSV(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text.Replace("\"", "\"\"");
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
}