using UnityEngine;
using System.Collections.Generic;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 为GameObject提供持久化的唯一ID，用于对话系统的引用
/// 修复版本：自动检测并修复重复ID
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class DialogueReference : MonoBehaviour
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
    // 用于检测是否刚被复制
    private static HashSet<string> registeredIDs = new HashSet<string>();
#endif

    private void OnValidate()
    {
#if UNITY_EDITOR
        // 确保有ID
        if (string.IsNullOrEmpty(uniqueID))
        {
            GenerateNewID();
            return;
        }

        // 检测ID重复（这是新增的关键代码）
        if (IsDuplicateID(uniqueID))
        {
            Debug.LogWarning($"[DialogueReference] 检测到重复ID！GameObject '{gameObject.name}' 的ID '{uniqueID}' 已被其他对象使用，正在重新生成...");
            GenerateNewID();
        }
#else
        // 运行时只确保有ID
        if (string.IsNullOrEmpty(uniqueID))
        {
            GenerateNewID();
        }
#endif
    }

    private void Awake()
    {
        // 运行时也确保有ID
        if (string.IsNullOrEmpty(uniqueID))
        {
            GenerateNewID();
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// 检测当前ID是否与其他对象重复
    /// </summary>
    private bool IsDuplicateID(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;

        // 查找所有DialogueReference
        var allRefs = Resources.FindObjectsOfTypeAll<DialogueReference>();

        // 筛选出场景中的有效对象（排除预制体）
        var sceneRefs = allRefs.Where(r => r != null &&
                                           r.gameObject.scene.IsValid() &&
                                           r != this).ToList();

        // 检查是否有其他对象使用相同ID
        return sceneRefs.Any(r => r.uniqueID == id);
    }
#endif

    private void GenerateNewID()
    {
        string oldID = uniqueID;
        uniqueID = System.Guid.NewGuid().ToString();

#if UNITY_EDITOR
        if (!string.IsNullOrEmpty(oldID))
        {
            Debug.Log($"[DialogueReference] GameObject '{gameObject.name}' ID已更新: {oldID} → {uniqueID}");
        }

        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(this);
        }
#endif
    }

    /// <summary>
    /// 手动重新生成ID（用于修复工具）
    /// </summary>
    public void ForceRegenerateID()
    {
        GenerateNewID();
    }

    /// <summary>
    /// 查找所有场景中的DialogueReference（包括inactive对象）
    /// </summary>
    public static GameObject FindByID(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        var allRefs = Resources.FindObjectsOfTypeAll<DialogueReference>();
        var target = System.Array.Find(allRefs, x => x.UniqueID == id && x.gameObject.scene.IsValid());

        return target?.gameObject;
    }

    /// <summary>
    /// 获取或创建GameObject的DialogueReference组件
    /// </summary>
    public static DialogueReference GetOrCreate(GameObject obj)
    {
        if (obj == null) return null;

        var component = obj.GetComponent<DialogueReference>();
        if (component == null)
        {
            component = obj.AddComponent<DialogueReference>();
#if UNITY_EDITOR
            Debug.Log($"[DialogueReference] 为GameObject '{obj.name}' 创建DialogueReference组件，ID: {component.UniqueID}");
#endif
        }

        return component;
    }

#if UNITY_EDITOR
    /// <summary>
    /// 编辑器菜单：修复当前场景中的所有重复ID
    /// </summary>
    [MenuItem("Tools/Dialogue System/Fix All Duplicate IDs in Scene")]
    public static void FixAllDuplicateIDsInScene()
    {
        var allRefs = Resources.FindObjectsOfTypeAll<DialogueReference>();
        var sceneRefs = allRefs.Where(r => r != null && r.gameObject.scene.IsValid()).ToList();

        // 统计ID出现次数
        var idCounts = new Dictionary<string, List<DialogueReference>>();

        foreach (var refComp in sceneRefs)
        {
            string id = refComp.uniqueID;
            if (string.IsNullOrEmpty(id)) continue;

            if (!idCounts.ContainsKey(id))
            {
                idCounts[id] = new List<DialogueReference>();
            }
            idCounts[id].Add(refComp);
        }

        // 找出重复的ID
        var duplicates = idCounts.Where(kvp => kvp.Value.Count > 1).ToList();

        if (duplicates.Count == 0)
        {
            EditorUtility.DisplayDialog("检查完成", "没有发现重复ID", "确定");
            return;
        }

        int fixedCount = 0;
        foreach (var dup in duplicates)
        {
            Debug.LogWarning($"[DialogueReference] 发现重复ID: {dup.Key}，共 {dup.Value.Count} 个对象");

            // 保留第一个，重新生成其他的ID
            for (int i = 1; i < dup.Value.Count; i++)
            {
                Undo.RecordObject(dup.Value[i], "Fix Duplicate DialogueReference ID");
                dup.Value[i].ForceRegenerateID();
                fixedCount++;
                Debug.Log($"[DialogueReference] 已为 '{dup.Value[i].gameObject.name}' 重新生成ID");
            }
        }

        EditorUtility.DisplayDialog("修复完成",
            $"发现 {duplicates.Count} 组重复ID\n已修复 {fixedCount} 个对象", "确定");
    }

    /// <summary>
    /// 编辑器菜单：显示当前场景所有DialogueReference的ID
    /// </summary>
    [MenuItem("Tools/Dialogue System/List All DialogueReference IDs")]
    public static void ListAllIDs()
    {
        var allRefs = Resources.FindObjectsOfTypeAll<DialogueReference>();
        var sceneRefs = allRefs.Where(r => r != null && r.gameObject.scene.IsValid()).ToList();

        Debug.Log($"====== 场景中的 DialogueReference 列表 (共 {sceneRefs.Count} 个) ======");

        foreach (var refComp in sceneRefs)
        {
            Debug.Log($"GameObject: {refComp.gameObject.name} | ID: {refComp.UniqueID}");
        }
    }
#endif
}