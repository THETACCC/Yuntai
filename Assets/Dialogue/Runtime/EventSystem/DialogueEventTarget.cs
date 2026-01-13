using UnityEngine;
using System.Collections.Generic;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 为GameObject提供持久化的唯一ID，用于对话系统的事件目标引用
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class DialogueEventTarget : MonoBehaviour
{
    [SerializeField] private string uniqueID;

    public string UniqueID
    {
        get
        {
            if (string.IsNullOrEmpty(uniqueID))
            {
                GenerateNewID();
            }
            return uniqueID;
        }
    }

#if UNITY_EDITOR
    private static HashSet<string> registeredIDs = new HashSet<string>();
#endif

    private void OnValidate()
    {
#if UNITY_EDITOR
        if (string.IsNullOrEmpty(uniqueID))
        {
            GenerateNewID();
            RegisterToIDRegistry();
            return;
        }

        // 运行时完全跳过ID冲突检查，避免误报
        if (Application.isPlaying)
        {
            return;
        }

        // 检查IDRegistry中是否已存在此ID
        if (DialogueSystem.DialogueEventIDRegistry.Instance.Contains(uniqueID))
        {
            // 再次确认：真的有其他对象在用这个ID吗？
            var duplicates = FindDuplicateObjects(uniqueID);

            if (duplicates.Count > 0)
            {
                // 确实有重复！自动重新生成ID
                string oldID = uniqueID;
                Debug.LogWarning($"⚠️ [DialogueEventTarget] 检测到重复ID '{oldID.Substring(0, 8)}...' on GameObject '{gameObject.name}'，自动生成新ID", this);

                GenerateNewID();

                Debug.Log($"[DialogueEventTarget] GameObject '{gameObject.name}' ID已更新: {oldID.Substring(0, 8)}... → {uniqueID.Substring(0, 8)}...");
            }
        }

        // 注册到IDRegistry（新ID或已存在但无冲突的ID）
        RegisterToIDRegistry();

        // 额外检查：使用旧方法double check（防御性编程）
        if (IsDuplicateID(uniqueID))
        {
            var duplicates = FindDuplicateObjects(uniqueID);
            Debug.LogError($"⚠️ [DialogueEventTarget] ID CONFLICT DETECTED!\nGameObject '{gameObject.name}' has duplicate ID: {uniqueID}\nConflicts with: {string.Join(", ", duplicates.Select(d => $"'{d.gameObject.name}'"))}\nThis should not happen with ID Registry enabled.", this);
        }
#else
        if (string.IsNullOrEmpty(uniqueID))
        {
            Debug.LogError($"[DialogueEventTarget] GameObject '{gameObject.name}' 没有ID！");
        }
#endif
    }

    private void Awake()
    {
        if (string.IsNullOrEmpty(uniqueID))
        {
            Debug.LogError($"[DialogueEventTarget] GameObject '{gameObject.name}' 没有ID！");
        }
    }

#if UNITY_EDITOR
    private bool IsDuplicateID(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;

        var allRefs = Resources.FindObjectsOfTypeAll<DialogueEventTarget>();
        var sceneRefs = allRefs.Where(r =>
        {
            if (r == null || r == this) return false;
            if (!r.gameObject.scene.IsValid()) return false;
            if (r.gameObject.scene.name == null) return false;
            if (r.gameObject.scene.path == r.gameObject.scene.name) return false;
            return true;
        }).ToList();

        return sceneRefs.Any(r => r.uniqueID == id);
    }

    private List<DialogueEventTarget> FindDuplicateObjects(string id)
    {
        if (string.IsNullOrEmpty(id)) return new List<DialogueEventTarget>();

        var allRefs = Resources.FindObjectsOfTypeAll<DialogueEventTarget>();
        return allRefs.Where(r =>
        {
            if (r == null || r == this) return false;
            if (!r.gameObject.scene.IsValid()) return false;
            if (r.gameObject.scene.name == null) return false;
            if (r.gameObject.scene.path == r.gameObject.scene.name) return false;
            return r.uniqueID == id;
        }).ToList();
    }

    /// <summary>
    /// 获取GameObject在场景中的完整路径
    /// </summary>
    private static string GetGameObjectPath(GameObject obj)
    {
        string path = obj.name;
        Transform parent = obj.transform.parent;
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }
        return path;
    }

    /// <summary>
    /// 通过路径查找GameObject
    /// </summary>
    private static GameObject FindGameObjectByPath(string path)
    {
        var parts = path.Split('/');
        GameObject current = null;

        // 查找根对象
        foreach (GameObject rootObj in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (rootObj.name == parts[0])
            {
                current = rootObj;
                break;
            }
        }

        if (current == null) return null;

        // 遍历子对象
        for (int i = 1; i < parts.Length; i++)
        {
            Transform child = current.transform.Find(parts[i]);
            if (child == null) return null;
            current = child.gameObject;
        }

        return current;
    }
#endif

    private void GenerateNewID()
    {
        string oldID = uniqueID;
        uniqueID = System.Guid.NewGuid().ToString();

#if UNITY_EDITOR
        if (!string.IsNullOrEmpty(oldID))
        {
            Debug.Log($"[DialogueEventTarget] GameObject '{gameObject.name}' ID已更新: {oldID} → {uniqueID}");
        }

        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(this);
        }
#endif
    }

    public void ForceRegenerateID()
    {
        GenerateNewID();
#if UNITY_EDITOR
        RegisterToIDRegistry();
#endif
    }

#if UNITY_EDITOR
    /// <summary>
    /// 注册到ID注册表
    /// </summary>
    private void RegisterToIDRegistry()
    {
        if (string.IsNullOrEmpty(uniqueID)) return;
        if (!gameObject.scene.IsValid()) return;  // prefab asset不注册

        string scenePath = gameObject.scene.path;
        if (string.IsNullOrEmpty(scenePath)) return;  // 未保存的场景

        DialogueSystem.DialogueEventIDRegistry.Instance.Add(uniqueID, scenePath, gameObject.name);
    }


    // 注意：不需要OnDestroy来清理ID
    // ID的清理由自动清理系统在场景打开/保存时处理
#endif


    public static GameObject FindByID(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        var allRefs = Resources.FindObjectsOfTypeAll<DialogueEventTarget>();
        var target = System.Array.Find(allRefs, x => x.UniqueID == id && x.gameObject.scene.IsValid());

        return target?.gameObject;
    }

    public static DialogueEventTarget GetOrCreate(GameObject obj)
    {
        if (obj == null) return null;

        var component = obj.GetComponent<DialogueEventTarget>();
        if (component == null)
        {
            component = obj.AddComponent<DialogueEventTarget>();
#if UNITY_EDITOR
            Debug.Log($"[DialogueEventTarget] 为GameObject '{obj.name}' 创建DialogueEventTarget组件，ID: {component.UniqueID}");
#endif
        }

        return component;
    }
}