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

        // ⚠️ 已禁用自动重复检测，避免意外重新生成ID
        // 如果需要检测重复ID，请使用菜单：Tools > Dialogue System > Fix All Duplicate IDs in Scene

        // 如果在播放模式下且对象在DontDestroyOnLoad场景，跳过所有检测
        if (Application.isPlaying && gameObject.scene.name == "DontDestroyOnLoad")
        {
            return;
        }
#else
        // 运行时不应该修改ID
        if (string.IsNullOrEmpty(uniqueID))
        {
            Debug.LogError($"[DialogueReference] GameObject '{gameObject.name}' 没有ID！");
        }
#endif
    }

    private void Awake()
    {
        // 运行时检查ID，如果没有ID则报错（不能在运行时生成新ID，因为JSON中已经保存了ID）
        if (string.IsNullOrEmpty(uniqueID))
        {
            Debug.LogError($"[DialogueReference] GameObject '{gameObject.name}' 没有ID！这个对象可能无法被对话系统找到。请在编辑器中重新保存场景。");
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

        // 筛选出真实场景中的有效对象（排除Prefab资源、自己、以及预览场景）
        var sceneRefs = allRefs.Where(r =>
        {
            if (r == null || r == this) return false;

            // 排除Prefab资源（不在任何场景中的）
            if (!r.gameObject.scene.IsValid()) return false;

            // 排除预览场景
            if (r.gameObject.scene.name == null) return false;

            // 排除Prefab编辑模式下的临时场景
            if (r.gameObject.scene.path == r.gameObject.scene.name) return false;

            // DontDestroyOnLoad的对象在特殊场景中，场景名为"DontDestroyOnLoad"
            // 但它们仍然是有效的，应该被包含在检查中

            return true;
        }).ToList();

        // 检查是否有其他对象使用相同ID
        bool hasDuplicate = sceneRefs.Any(r => r.uniqueID == id);

        if (hasDuplicate)
        {
            // 打印详细信息帮助调试
            var duplicates = sceneRefs.Where(r => r.uniqueID == id).ToList();
            foreach (var dup in duplicates)
            {
                Debug.Log($"[DialogueReference] 发现重复ID的对象: {dup.gameObject.name} (场景: {dup.gameObject.scene.name})");
            }
        }

        return hasDuplicate;
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
    /// ⚠️ 使用此工具后，需要使用 DialogueIDFixer 更新对话树文件中的ID
    /// </summary>
    [MenuItem("Tools/Dialogue System/Fix All Duplicate IDs in Scene")]
    public static void FixAllDuplicateIDsInScene()
    {
        if (!EditorUtility.DisplayDialog("⚠️ 重要警告",
            "此操作会重新生成重复的ID！\n\n" +
            "如果你的对话树文件中引用了这些对象，\n" +
            "修复后你需要使用:\n" +
            "Tools > Dialogue System > Fix Dialogue Tree IDs\n" +
            "来更新对话树文件中的ID引用\n\n" +
            "建议先备份场景和对话树文件！\n\n" +
            "确定要继续吗？",
            "确定", "取消"))
        {
            return;
        }

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
            Debug.Log($"GameObject: {refComp.gameObject.name} | ID: {refComp.UniqueID} | 场景: {refComp.gameObject.scene.name}");
        }
    }

    /// <summary>
    /// 编辑器菜单：为所有Prefab实例重新生成ID
    /// </summary>
    [MenuItem("Tools/Dialogue System/Regenerate IDs for All Prefab Instances")]
    public static void RegenerateIDsForPrefabInstances()
    {
        var allRefs = Resources.FindObjectsOfTypeAll<DialogueReference>();
        var sceneRefs = allRefs.Where(r => r != null && r.gameObject.scene.IsValid()).ToList();

        int count = 0;
        foreach (var refComp in sceneRefs)
        {
            var prefabStatus = PrefabUtility.GetPrefabInstanceStatus(refComp.gameObject);
            if (prefabStatus == PrefabInstanceStatus.Connected)
            {
                Undo.RecordObject(refComp, "Regenerate Prefab Instance ID");
                refComp.ForceRegenerateID();
                count++;
                Debug.Log($"[DialogueReference] 为Prefab实例 '{refComp.gameObject.name}' 重新生成ID");
            }
        }

        if (count > 0)
        {
            EditorUtility.DisplayDialog("完成", $"已为 {count} 个Prefab实例重新生成ID", "确定");
        }
        else
        {
            EditorUtility.DisplayDialog("完成", "场景中没有发现Prefab实例", "确定");
        }
    }
#endif
}