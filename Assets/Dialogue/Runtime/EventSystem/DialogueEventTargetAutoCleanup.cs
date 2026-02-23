using UnityEngine;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// DialogueEventTarget ID 自动清理管理器
/// 在场景打开和保存时自动清理无效的ID
/// </summary>
[InitializeOnLoad]
public static class DialogueEventTargetAutoCleanup
{
    static DialogueEventTargetAutoCleanup()
    {
        // 场景打开时清理
        EditorSceneManager.sceneOpened += OnSceneOpened;

        // 场景保存时清理
        EditorSceneManager.sceneSaving += OnSceneSaving;
    }

    /// <summary>
    /// 场景打开时：清理该场景在注册表中的无效ID
    /// </summary>
    private static void OnSceneOpened(UnityEngine.SceneManagement.Scene scene, OpenSceneMode mode)
    {
        if (string.IsNullOrEmpty(scene.path)) return;

        CleanupSceneIDs(scene, scene.path);
    }

    /// <summary>
    /// 场景保存时：清理该场景在注册表中的无效ID
    /// </summary>
    private static void OnSceneSaving(UnityEngine.SceneManagement.Scene scene, string path)
    {
        if (string.IsNullOrEmpty(path)) return;

        CleanupSceneIDs(scene, path);
    }

    /// <summary>
    /// 清理指定场景的无效ID
    /// </summary>
    private static void CleanupSceneIDs(UnityEngine.SceneManagement.Scene scene, string scenePath)
    {
        // 【修复】Instance 在 domain reload 极早期可能返回 null（AssetDatabase 尚未就绪）
        // 此时跳过清理，避免用空注册表覆盖磁盘上队友 push 的完整 registry
        var registry = DialogueSystem.DialogueEventIDRegistry.Instance;
        if (registry == null)
        {
            Debug.LogWarning("[DialogueEventTargetAutoCleanup] Registry 尚未就绪，跳过本次 cleanup。");
            return;
        }

        var records = registry.GetAllRecords();

        // 找出注册表中属于这个场景的所有ID
        var sceneRecords = records.Where(r => r.scenePath == scenePath).ToList();

        if (sceneRecords.Count == 0) return;

        int removedCount = 0;

        // 遍历该场景的所有ID，检查GameObject是否还存在
        foreach (var record in sceneRecords)
        {
            var obj = DialogueEventTarget.FindByID(record.id);

            if (obj == null)
            {
                // GameObject不存在了，删除ID
                registry.Remove(record.id);
                removedCount++;
            }
        }

        if (removedCount > 0)
        {
            Debug.Log($"[DialogueEventTarget] 场景 '{scene.name}' 自动清理了 {removedCount} 个无效ID");
        }
    }
}
#endif
