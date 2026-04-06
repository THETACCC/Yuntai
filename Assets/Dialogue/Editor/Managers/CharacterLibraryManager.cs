using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DialogueSystem;
using System;

/// <summary>
/// 角色库管理器 - 负责角色数据的CRUD操作
/// </summary>
public class CharacterLibraryManager
{
    private CharacterLibraryData characterLibrary;
    private CharacterFolderData characterFolderData;

    public CharacterLibraryData CharacterLibrary => characterLibrary;
    public CharacterFolderData CharacterFolderData => characterFolderData;

    public void LoadCharacterLibrary()
    {
        try
        {
            string loadPath = GetCharacterLibraryPath();

            if (File.Exists(loadPath))
            {
                string json = File.ReadAllText(loadPath);
                characterLibrary = JsonUtility.FromJson<CharacterLibraryData>(json);

                bool needsSave = false;
                if (characterLibrary?.characters != null)
                {
                    foreach (var character in characterLibrary.characters)
                    {
                        if (string.IsNullOrEmpty(character.character))
                        {
                            character.character = character.characterName?.en ?? "New Character";
                            needsSave = true;
                        }
                    }
                }

                if (needsSave)
                {
                    SaveCharacterLibraryInternal();
                    Debug.Log("Updated character library with new 'character' field for compatibility");
                }
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

    public void LoadCharacterFolderStructure()
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

    public void SaveCharacterLibraryInternal()
    {
        try
        {
            string savePath = GetCharacterLibraryPath();
            string json = JsonUtility.ToJson(characterLibrary, true).Trim().Replace("\r\n", "\n");

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
            Debug.LogError($"Failed to save character library: {e.Message}");
        }
    }

    public void SaveCharacterFolderStructure()
    {
        try
        {
            string savePath = GetCharacterFolderStructurePath();
            string json = JsonUtility.ToJson(characterFolderData, true).Trim().Replace("\r\n", "\n");

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
            Debug.LogError($"Failed to save character folder structure: {e.Message}");
        }
    }

    public void CreateNewCharacter()
    {
        if (characterLibrary == null)
            characterLibrary = new CharacterLibraryData();

        var newCharacter = new CharacterData
        {
            id = System.Guid.NewGuid().ToString(),
            character = "New Character",
            characterName = new LocalizedText
            {
                zh = "新角色",
                en = "New Character",
                ja = "新しいキャラクター"
            },
            useNameId = false,
            nameId = "",
            avatarAssetPath = ""
        };

        // 数组操作：添加
        var list = new List<CharacterData>(characterLibrary.characters ?? new CharacterData[0]);
        list.Add(newCharacter);
        characterLibrary.characters = list.ToArray();

        characterFolderData.rootCharacterIds.Add(newCharacter.id);

        SaveCharacterLibraryInternal();
        SaveCharacterFolderStructure();
    }

    public void DeleteCharacter(string characterId)
    {
        if (characterLibrary == null) return;

        // 数组操作：删除
        var list = new List<CharacterData>(characterLibrary.characters ?? new CharacterData[0]);
        var character = list.FirstOrDefault(c => c.id == characterId);
        if (character != null)
        {
            list.Remove(character);
            characterLibrary.characters = list.ToArray();

            CleanupCharacterFromFolders(characterId);
            SaveCharacterLibraryInternal();
            SaveCharacterFolderStructure();
        }
    }

    public CharacterData GetCharacterById(string id)
    {
        return characterLibrary?.characters?.FirstOrDefault(c => c.id == id);
    }

    public int GetCharacterCount()
    {
        return characterLibrary?.characters?.Length ?? 0;
    }

    private void EnsureDefaultCharacterFolder()
    {
        if (characterFolderData == null)
            characterFolderData = new CharacterFolderData();

        var defaultFolder = characterFolderData.rootFolders?.FirstOrDefault(f => f.id == "default_character_folder");

        if (defaultFolder == null)
        {
            defaultFolder = new CharacterFolder
            {
                id = "default_character_folder",
                name = "Default",
                description = "Default character folder"
            };

            if (characterFolderData.rootFolders == null)
                characterFolderData.rootFolders = new List<CharacterFolder>();

            characterFolderData.rootFolders.Add(defaultFolder);
        }
    }

    private void CleanupCharacterFromFolders(string characterId)
    {
        characterFolderData.rootCharacterIds?.Remove(characterId);
        CleanupCharacterFromFoldersRecursive(characterFolderData.rootFolders, characterId);
    }

    private void CleanupCharacterFromFoldersRecursive(List<CharacterFolder> folders, string characterId)
    {
        if (folders == null) return;

        foreach (var folder in folders)
        {
            folder.characterIds?.Remove(characterId);
            CleanupCharacterFromFoldersRecursive(folder.subfolders, characterId);
        }
    }

    private string GetCharacterLibraryPath()
    {
        // 使用固定路径，避免循环引用
        return "Assets/Dialogue/Editor/Data/CharacterLibrary.json";
    }

    private string GetCharacterFolderStructurePath()
    {
        return "Assets/Dialogue/Editor/Data/CharacterFolderStructure.json";
    }
}