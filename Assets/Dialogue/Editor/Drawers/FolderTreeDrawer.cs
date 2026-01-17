using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DialogueSystem;

/// <summary>
/// 文件夹树UI绘制器 - 负责对话树文件夹的完整可视化和交互
/// </summary>
public class FolderTreeDrawer
{
    private VirtualFolderManager folderManager;
    private Dictionary<string, bool> folderExpandedState = new Dictionary<string, bool>();

    // 拖拽状态 - 文件拖拽
    private string draggedFileGuid;
    private VirtualFolder draggedFromFolder;

    // 拖拽状态 - 文件夹拖拽
    private VirtualFolder draggedFolder;
    private VirtualFolder draggedFolderParent;
    private bool isDraggingForReorder = false;

    // 插入位置提示
    private VirtualFolder insertBeforeFolder = null;
    private string insertBeforeFileGuid = null;
    private VirtualFolder insertParentFolder = null;
    private bool insertAfter = false;

    public FolderTreeDrawer(VirtualFolderManager manager)
    {
        this.folderManager = manager;

        // 确保default文件夹展开
        if (!folderExpandedState.ContainsKey("default_folder"))
            folderExpandedState["default_folder"] = true;
    }

    public void DrawFolderTree()
    {
        if (folderManager.FolderData == null) return;

        foreach (var folder in folderManager.FolderData.rootFolders)
        {
            DrawVirtualFolder(folder, 0, null);
        }

        foreach (var guid in folderManager.FolderData.rootFileGuids.ToList())
        {
            if (folderManager.GuidToPath.ContainsKey(guid))
            {
                DrawFile(guid, 0, null);
            }
        }
    }

    private void DrawVirtualFolder(VirtualFolder folder, int indentLevel, VirtualFolder parentFolder)
    {
        // 绘制插入线提示（在文件夹之前）
        if (insertBeforeFolder == folder && insertParentFolder == parentFolder && !insertAfter)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(indentLevel * 20);
            Rect insertLineRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.Height(3));
            EditorGUI.DrawRect(insertLineRect, new Color(0.3f, 0.6f, 1f, 0.8f));
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

        Rect labelRect = new Rect(rect.x + 25, rect.y + 3, rect.width - 30, rect.height);
        GUI.Label(labelRect, folder.name, EditorStyles.boldLabel);

        EditorGUILayout.EndHorizontal();

        // 显示description
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
        HandleFolderContextMenu(rect, folder, parentFolder);

        bool isExpandedForChildren = folderExpandedState.ContainsKey(folder.id) && folderExpandedState[folder.id];
        if (isExpandedForChildren)
        {
            foreach (var subfolder in folder.subfolders.ToList())
            {
                DrawVirtualFolder(subfolder, indentLevel + 1, folder);
            }

            foreach (var guid in folder.fileGuids.ToList())
            {
                if (folderManager.GuidToPath.ContainsKey(guid))
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
            EditorGUI.DrawRect(insertLineRect, new Color(0.3f, 0.6f, 1f, 0.8f));
            EditorGUILayout.EndHorizontal();
        }
    }

    private void DrawFile(string guid, int indentLevel, VirtualFolder parentFolder)
    {
        if (!folderManager.GuidToPath.ContainsKey(guid)) return;

        // 绘制插入线提示（在文件之前）
        if (insertBeforeFileGuid == guid && insertParentFolder == parentFolder && !insertAfter)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(indentLevel * 20);
            Rect insertLineRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.Height(3));
            EditorGUI.DrawRect(insertLineRect, new Color(0.3f, 0.6f, 1f, 0.8f));
            EditorGUILayout.EndHorizontal();
        }

        string filePath = folderManager.GuidToPath[guid];
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
            EditorGUI.DrawRect(insertLineRect, new Color(0.3f, 0.6f, 1f, 0.8f));
            EditorGUILayout.EndHorizontal();
        }
    }

    #region 文件拖拽和排序

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

        Rect expandedRect = new Rect(rect.x, rect.y - 10, rect.width, rect.height + 20);
        if (expandedRect.Contains(e.mousePosition))
        {
            if (e.type == EventType.DragUpdated)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Move;

                float mouseY = e.mousePosition.y;
                float rectMiddle = rect.y + rect.height / 2;
                bool shouldInsertAfter = mouseY > rectMiddle;

                insertBeforeFileGuid = guid;
                insertBeforeFolder = null;
                insertParentFolder = parentFolder;
                insertAfter = shouldInsertAfter;

                e.Use();
            }
            else if (e.type == EventType.DragPerform)
            {
                float mouseY = e.mousePosition.y;
                float rectMiddle = rect.y + rect.height / 2;
                bool shouldInsertAfter = mouseY > rectMiddle;

                ReorderFile(draggedFileGuid, guid, parentFolder, shouldInsertAfter);
                DragAndDrop.AcceptDrag();
                draggedFileGuid = null;
                draggedFromFolder = null;

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
        List<string> list = parentFolder == null ? folderManager.FolderData.rootFileGuids : parentFolder.fileGuids;

        int sourceIndex = list.IndexOf(sourceGuid);
        int targetIndex = list.IndexOf(targetGuid);

        if (sourceIndex != -1 && targetIndex != -1 && sourceIndex != targetIndex)
        {
            list.RemoveAt(sourceIndex);
            targetIndex = list.IndexOf(targetGuid);

            if (insertAfter)
            {
                targetIndex++;
            }

            list.Insert(targetIndex, sourceGuid);
            folderManager.SaveVirtualFolderStructure();
        }
    }

    #endregion

    #region 文件夹拖拽和排序

    private void HandleFolderDragForReorder(Rect rect, VirtualFolder folder, VirtualFolder parentFolder)
    {
        Event e = Event.current;

        // default_folder不能作为拖拽源
        if (folder.id != "default_folder" && e.type == EventType.MouseDown && rect.Contains(e.mousePosition) && e.button == 0)
        {
            float buttonAreaWidth = 130;
            Rect buttonArea = new Rect(rect.xMax - buttonAreaWidth, rect.y, buttonAreaWidth, rect.height);
            if (!buttonArea.Contains(e.mousePosition))
            {
                draggedFolder = folder;
                draggedFolderParent = parentFolder;
                isDraggingForReorder = false;
            }
        }

        if (folder.id != "default_folder" && e.type == EventType.MouseDrag && draggedFolder == folder && !isDraggingForReorder)
        {
            DragAndDrop.PrepareStartDrag();
            DragAndDrop.SetGenericData("ReorderFolder", folder);
            DragAndDrop.StartDrag("Reordering Folder");
            isDraggingForReorder = true;
            e.Use();
        }

        // default_folder可以作为拖拽目标
        if (isDraggingForReorder && draggedFolder != null && draggedFolder != folder)
        {
            // 只有在同一个父级下才能排序
            if (draggedFolderParent == parentFolder)
            {
                Rect expandedRect = new Rect(rect.x, rect.y - 10, rect.width, rect.height + 20);
                if (e.type == EventType.DragUpdated && expandedRect.Contains(e.mousePosition))
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Move;

                    float mouseY = e.mousePosition.y;
                    float rectMiddle = rect.y + rect.height / 2;
                    bool shouldInsertAfter = mouseY > rectMiddle;

                    insertBeforeFolder = folder;
                    insertBeforeFileGuid = null;
                    insertParentFolder = parentFolder;
                    insertAfter = shouldInsertAfter;

                    e.Use();
                }
                else if (e.type == EventType.DragPerform && expandedRect.Contains(e.mousePosition))
                {
                    float mouseY = e.mousePosition.y;
                    float rectMiddle = rect.y + rect.height / 2;
                    bool shouldInsertAfter = mouseY > rectMiddle;

                    ReorderFolder(draggedFolder, folder, parentFolder, shouldInsertAfter);
                    DragAndDrop.AcceptDrag();
                    draggedFolder = null;
                    draggedFolderParent = null;
                    isDraggingForReorder = false;

                    insertBeforeFolder = null;
                    insertBeforeFileGuid = null;
                    insertParentFolder = null;
                    insertAfter = false;

                    e.Use();
                }
            }
        }

        if (e.type == EventType.DragExited || e.type == EventType.MouseUp)
        {
            if (draggedFolder != null)
            {
                draggedFolder = null;
                draggedFolderParent = null;
                isDraggingForReorder = false;

                insertBeforeFolder = null;
                insertBeforeFileGuid = null;
                insertParentFolder = null;
                insertAfter = false;
            }
        }
    }

    private void ReorderFolder(VirtualFolder sourceFolder, VirtualFolder targetFolder, VirtualFolder parentFolder, bool insertAfter)
    {
        List<VirtualFolder> list = parentFolder == null ? folderManager.FolderData.rootFolders : parentFolder.subfolders;

        int sourceIndex = list.IndexOf(sourceFolder);
        int targetIndex = list.IndexOf(targetFolder);

        if (sourceIndex != -1 && targetIndex != -1 && sourceIndex != targetIndex)
        {
            list.RemoveAt(sourceIndex);
            targetIndex = list.IndexOf(targetFolder);

            if (insertAfter)
            {
                targetIndex++;
            }

            list.Insert(targetIndex, sourceFolder);
            folderManager.SaveVirtualFolderStructure();
        }
    }

    private void HandleFolderDragAndDrop(Rect rect, VirtualFolder folder)
    {
        Event e = Event.current;

        if (rect.Contains(e.mousePosition))
        {
            if (e.type == EventType.DragUpdated)
            {
                // 只处理文件拖拽到文件夹
                if (!string.IsNullOrEmpty(draggedFileGuid) && !isDraggingForReorder)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Move;
                    e.Use();
                }
            }
            else if (e.type == EventType.DragPerform)
            {
                if (!string.IsNullOrEmpty(draggedFileGuid) && !isDraggingForReorder)
                {
                    MoveFileToFolder(draggedFileGuid, draggedFromFolder, folder);
                    DragAndDrop.AcceptDrag();
                    draggedFileGuid = null;
                    draggedFromFolder = null;

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
            folderManager.FolderData.rootFileGuids.Remove(fileGuid);
        }
        else
        {
            fromFolder.fileGuids.Remove(fileGuid);
        }

        if (!toFolder.fileGuids.Contains(fileGuid))
        {
            toFolder.fileGuids.Add(fileGuid);
        }

        folderManager.SaveVirtualFolderStructure();
    }

    #endregion

    #region 文件夹操作

    private void HandleFolderContextMenu(Rect rect, VirtualFolder folder, VirtualFolder parentFolder)
    {
        Event e = Event.current;

        if (e.type == EventType.ContextClick && rect.Contains(e.mousePosition))
        {
            GenericMenu menu = new GenericMenu();
            
            if (folder.id == "default_folder")
            {
                menu.AddItem(new GUIContent("+ New Dialogue"), false, () =>
                {
                    CreateNewDialogueFile(folder);
                });
            }
            
            menu.AddItem(new GUIContent("New Folder"), false, () =>
            {
                CreateVirtualFolder(folder);
            });
            
            if (folder.id != "default_folder")
            {
                menu.AddItem(new GUIContent("Rename"), false, () => RenameFolder(folder));
                menu.AddItem(new GUIContent("Delete"), false, () =>
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
                });
            }
            
            menu.ShowAsContext();
            e.Use();
        }
    }

    private void CreateVirtualFolder(VirtualFolder parent)
    {
        string folderName = "New Folder";
        int counter = 1;

        var existingNames = parent == null
            ? folderManager.FolderData.rootFolders.Select(f => f.name).ToList()
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
            folderManager.FolderData.rootFolders.Add(newFolder);
        }
        else
        {
            parent.subfolders.Add(newFolder);
        }

        folderManager.SaveVirtualFolderStructure();
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
            folderManager.FolderData.rootFolders.Remove(folder);
        }
        else
        {
            parent.subfolders.Remove(folder);
        }

        folderManager.SaveVirtualFolderStructure();
    }

    private void CollectAllFilesRecursive(VirtualFolder folder, List<string> fileList)
    {
        fileList.AddRange(folder.fileGuids);

        foreach (var subfolder in folder.subfolders)
        {
            CollectAllFilesRecursive(subfolder, fileList);
        }
    }

    private VirtualFolder GetDefaultFolder()
    {
        return folderManager.FolderData.rootFolders.FirstOrDefault(f => f.id == "default_folder");
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
                    folderManager.SaveVirtualFolderStructure();
                }
            });
        };
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
                    folderManager.SaveVirtualFolderStructure();
                }
            });
        };
    }

    #endregion

    #region 文件操作

    private void CreateNewDialogueFile(VirtualFolder folder)
    {
        string defaultPath = Path.Combine(UnityEngine.Application.dataPath, "StreamingAssets");
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

            string json = UnityEngine.JsonUtility.ToJson(emptyTree, true).Trim();
            json = json.Replace("\r\n", "\n");
            System.Text.UTF8Encoding utf8WithoutBom = new System.Text.UTF8Encoding(false);
            File.WriteAllText(savePath, json, utf8WithoutBom);

            // 创建对应的 .json 运行时文件
            string jsonPath = Path.ChangeExtension(savePath, ".json");
            string runtimeJson = "{\n  \"conversations\": [\n    {\n      \"index\": 0,\n      \"name\": \"\",\n      \"avatarAddr\": \"\",\n      \"isPlayer\": false,\n      \"content\": \"Start dialogue here...\",\n      \"nextIndex\": -1,\n      \"choices\": [],\n      \"eventCalls\": [],\n      \"conditionalBranches\": []\n    }\n  ],\n  \"currentIndex\": 0\n}";
            File.WriteAllText(jsonPath, runtimeJson, utf8WithoutBom);

            AssetDatabase.Refresh();

            Debug.Log($"Created new dialogue tree: {savePath}");

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

    private void OpenInDialogueEditor(string dtreePath)
    {
        DialogueTreeEditor.OpenWindow();

        EditorApplication.delayCall += () =>
        {
            var window = EditorWindow.GetWindow<DialogueTreeEditor>();
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
}