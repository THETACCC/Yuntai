using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DialogueSystem;

/// <summary>
/// 虚拟文件夹管理器 - 负责对话树文件夹结构管理
/// </summary>
public class VirtualFolderManager
{
    private VirtualFolderData folderData;
    private Dictionary<string, string> guidToPath = new Dictionary<string, string>();

    public VirtualFolderData FolderData => folderData;
    public Dictionary<string, string> GuidToPath => guidToPath;

    public void LoadVirtualFolderStructure()
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

        EnsureDefaultFolder();
    }

    public void SaveVirtualFolderStructure()
    {
        try
        {
            string savePath = GetFolderStructurePath();
            string json = JsonUtility.ToJson(folderData, true).Trim().Replace("\r\n", "\n");

            string folder = Path.GetDirectoryName(savePath);
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            if (File.Exists(savePath) && File.ReadAllText(savePath) == json)
                return;

            System.Text.UTF8Encoding utf8WithoutBom = new System.Text.UTF8Encoding(false);
            File.WriteAllText(savePath, json, utf8WithoutBom);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to save folder structure: {e.Message}");
        }
    }

    public void ScanAllDialogueTrees()
    {
        guidToPath.Clear();

        // 扫描整个Assets目录下的所有.dtree文件
        string[] allFiles = Directory.GetFiles(Application.dataPath, "*.dtree", SearchOption.AllDirectories);

        foreach (var file in allFiles)
        {
            // 转换为Unity Asset路径并获取GUID
            string guid = AssetDatabase.AssetPathToGUID("Assets" + file.Substring(Application.dataPath.Length));
            if (string.IsNullOrEmpty(guid))
            {
                guid = file.GetHashCode().ToString();
            }
            guidToPath[guid] = file;  // 存储完整的文件系统路径
        }

        CleanupDeletedFiles();
        EnsureDefaultFolder();

        // 将未分类的文件添加到default文件夹（新文件按GUID排序追加，保持确定性）
        VirtualFolder defaultFolder = GetDefaultFolder();
        if (defaultFolder != null)
        {
            var newGuids = guidToPath.Keys
                .Where(guid => !IsFileInAnyFolder(guid))
                .OrderBy(guid => guid)
                .ToList();
            foreach (var guid in newGuids)
                defaultFolder.fileGuids.Add(guid);
        }

        SaveVirtualFolderStructure();
        Debug.Log($"[FolderManager] Found {guidToPath.Count} dialogue tree files");
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

    public VirtualFolder FindFolderById(string folderId)
    {
        return FindFolderByIdRecursive(folderData.rootFolders, folderId);
    }

    public void CreateNewFolder(VirtualFolder parentFolder)
    {
        var newFolder = new VirtualFolder
        {
            name = "New Folder",
            id = System.Guid.NewGuid().ToString(),
            description = ""
        };

        if (parentFolder != null)
        {
            if (parentFolder.subfolders == null)
                parentFolder.subfolders = new List<VirtualFolder>();
            parentFolder.subfolders.Add(newFolder);
        }
        else
        {
            if (folderData.rootFolders == null)
                folderData.rootFolders = new List<VirtualFolder>();
            folderData.rootFolders.Add(newFolder);
        }

        SaveVirtualFolderStructure();
    }

    public void DeleteFolder(VirtualFolder folder, VirtualFolder parentFolder)
    {
        if (folder.id == "default_folder")
        {
            Debug.LogWarning("Cannot delete default folder");
            return;
        }

        // Move files to root
        if (folder.fileGuids != null)
        {
            foreach (var guid in folder.fileGuids)
            {
                if (!folderData.rootFileGuids.Contains(guid))
                    folderData.rootFileGuids.Add(guid);
            }
        }

        // Move subfolders to parent
        if (folder.subfolders != null)
        {
            foreach (var subfolder in folder.subfolders)
            {
                if (parentFolder != null)
                {
                    if (parentFolder.subfolders == null)
                        parentFolder.subfolders = new List<VirtualFolder>();
                    parentFolder.subfolders.Add(subfolder);
                }
                else
                {
                    folderData.rootFolders.Add(subfolder);
                }
            }
        }

        // Remove folder
        if (parentFolder != null)
        {
            parentFolder.subfolders.Remove(folder);
        }
        else
        {
            folderData.rootFolders.Remove(folder);
        }

        SaveVirtualFolderStructure();
    }

    public void MoveFileToFolder(string fileGuid, VirtualFolder targetFolder)
    {
        // Remove from current location
        RemoveFileFromAllFolders(fileGuid);

        // Add to target
        if (targetFolder != null)
        {
            if (targetFolder.fileGuids == null)
                targetFolder.fileGuids = new List<string>();

            if (!targetFolder.fileGuids.Contains(fileGuid))
                targetFolder.fileGuids.Add(fileGuid);
        }
        else
        {
            if (!folderData.rootFileGuids.Contains(fileGuid))
                folderData.rootFileGuids.Add(fileGuid);
        }

        SaveVirtualFolderStructure();
    }

    private void EnsureDefaultFolder()
    {
        if (folderData == null)
            folderData = new VirtualFolderData();

        var defaultFolder = folderData.rootFolders?.FirstOrDefault(f => f.id == "default_folder");

        if (defaultFolder == null)
        {
            defaultFolder = new VirtualFolder
            {
                id = "default_folder",
                name = "All Dialogues",  // 改名为 "All Dialogues"
                description = "Default dialogue tree folder"
            };

            if (folderData.rootFolders == null)
                folderData.rootFolders = new List<VirtualFolder>();

            folderData.rootFolders.Insert(0, defaultFolder);  // 插入到最前面
        }
    }

    private VirtualFolder FindFolderByIdRecursive(List<VirtualFolder> folders, string id)
    {
        if (folders == null) return null;

        foreach (var folder in folders)
        {
            if (folder.id == id) return folder;

            var found = FindFolderByIdRecursive(folder.subfolders, id);
            if (found != null) return found;
        }

        return null;
    }

    private void SortAllFileGuids()
    {
        folderData.rootFileGuids?.Sort();
        SortFolderGuidsRecursive(folderData.rootFolders);
    }

    private void SortFolderGuidsRecursive(List<VirtualFolder> folders)
    {
        if (folders == null) return;
        foreach (var folder in folders)
        {
            folder.fileGuids?.Sort();
            SortFolderGuidsRecursive(folder.subfolders);
        }
    }

    private void RemoveFileFromAllFolders(string fileGuid)
    {
        folderData.rootFileGuids?.Remove(fileGuid);
        RemoveFileFromFoldersRecursive(folderData.rootFolders, fileGuid);
    }

    private void RemoveFileFromFoldersRecursive(List<VirtualFolder> folders, string fileGuid)
    {
        if (folders == null) return;

        foreach (var folder in folders)
        {
            folder.fileGuids?.Remove(fileGuid);
            RemoveFileFromFoldersRecursive(folder.subfolders, fileGuid);
        }
    }

    private string GetFolderStructurePath()
    {
        return "Assets/Dialogue/Editor/Data/DialogueTreeFolderStructure.json";
    }
}

[System.Serializable]
public class VirtualFolder
{
    public string name;
    public string id;
    public string description = "";
    public List<string> fileGuids = new List<string>();
    public List<VirtualFolder> subfolders = new List<VirtualFolder>();
}

[System.Serializable]
public class VirtualFolderData
{
    public List<VirtualFolder> rootFolders = new List<VirtualFolder>();
    public List<string> rootFileGuids = new List<string>();
}

[System.Serializable]
public class CharacterFolder
{
    public string name;
    public string id;
    public string description = "";
    public List<string> characterIds = new List<string>();
    public List<CharacterFolder> subfolders = new List<CharacterFolder>();
}

[System.Serializable]
public class CharacterFolderData
{
    public List<CharacterFolder> rootFolders = new List<CharacterFolder>();
    public List<string> rootCharacterIds = new List<string>();
}