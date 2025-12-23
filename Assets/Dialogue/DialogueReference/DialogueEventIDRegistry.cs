using UnityEngine;
using System.Collections.Generic;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DialogueSystem
{
    /// <summary>
    /// 对话系统ID注册表 - 自动管理所有DialogueEventTarget的唯一ID
    /// 用户无需手动维护，完全自动化
    /// </summary>
    public class DialogueEventIDRegistry : ScriptableObject
    {
        private static DialogueEventIDRegistry instance;

        [System.Serializable]
        public class IDRecord
        {
            public string id;
            public string scenePath;
            public string objectName;  // 用于调试和可视化

            public IDRecord(string id, string scenePath, string objectName)
            {
                this.id = id;
                this.scenePath = scenePath;
                this.objectName = objectName;
            }
        }

        [SerializeField]
        private List<IDRecord> records = new List<IDRecord>();

        // 运行时缓存，不序列化
        [System.NonSerialized]
        private HashSet<string> idLookup;

        [System.NonSerialized]
        private bool isInitialized = false;

        public static DialogueEventIDRegistry Instance
        {
            get
            {
                if (instance == null)
                {
#if UNITY_EDITOR
                    // 尝试加载已存在的注册表
                    instance = AssetDatabase.FindAssets("t:DialogueEventIDRegistry")
                        .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                        .Select(path => AssetDatabase.LoadAssetAtPath<DialogueEventIDRegistry>(path))
                        .FirstOrDefault();

                    // 如果不存在，创建新的
                    if (instance == null)
                    {
                        instance = CreateInstance<DialogueEventIDRegistry>();

                        // 保存到Assets根目录
                        string path = "Assets/DialogueDialogueEventIDRegistry.asset";
                        AssetDatabase.CreateAsset(instance, path);
                        AssetDatabase.SaveAssets();

                        Debug.Log($"[DialogueEventIDRegistry] 创建新的ID注册表: {path}");
                    }
#else
                    Debug.LogError("[DialogueEventIDRegistry] Cannot create DialogueEventIDRegistry at runtime!");
#endif
                }

                if (instance != null && !instance.isInitialized)
                {
                    instance.Initialize();
                }

                return instance;
            }
        }

        private void Initialize()
        {
            if (idLookup == null)
            {
                idLookup = new HashSet<string>();
            }

            idLookup.Clear();
            foreach (var record in records)
            {
                idLookup.Add(record.id);
            }

            isInitialized = true;
        }

        private void OnEnable()
        {
            Initialize();
        }

        /// <summary>
        /// 检查ID是否已存在
        /// </summary>
        public bool Contains(string id)
        {
            if (!isInitialized) Initialize();
            return idLookup.Contains(id);
        }

        /// <summary>
        /// 添加ID记录
        /// </summary>
        public void Add(string id, string scenePath, string objectName)
        {
            if (!isInitialized) Initialize();

            if (idLookup.Contains(id))
            {
                // ID已存在，更新记录
                var existing = records.FirstOrDefault(r => r.id == id);
                if (existing != null)
                {
                    existing.scenePath = scenePath;
                    existing.objectName = objectName;
                }
            }
            else
            {
                // 新ID，添加记录
                records.Add(new IDRecord(id, scenePath, objectName));
                idLookup.Add(id);
            }

            // 排序以保证顺序固定，避免Git产生无意义的diff
            SortRecords();
            MarkDirty();
        }

        /// <summary>
        /// 删除单个ID
        /// </summary>
        public void Remove(string id)
        {
            if (!isInitialized) Initialize();

            records.RemoveAll(r => r.id == id);
            idLookup.Remove(id);

            SortRecords();
            MarkDirty();
        }

        /// <summary>
        /// 删除指定场景的所有ID
        /// </summary>
        public void RemoveByScene(string scenePath)
        {
            if (!isInitialized) Initialize();

            var toRemove = records.Where(r => r.scenePath == scenePath).ToList();

            foreach (var record in toRemove)
            {
                records.Remove(record);
                idLookup.Remove(record.id);
            }

            if (toRemove.Count > 0)
            {
                Debug.Log($"[DialogueEventIDRegistry] 已清理场景 '{scenePath}' 的 {toRemove.Count} 个ID记录");
                SortRecords();
                MarkDirty();
            }
        }

        /// <summary>
        /// 获取所有记录（用于编辑器显示）
        /// </summary>
        public List<IDRecord> GetAllRecords()
        {
            return new List<IDRecord>(records);
        }

        /// <summary>
        /// 获取统计信息
        /// </summary>
        public int GetTotalCount()
        {
            return records.Count;
        }

        /// <summary>
        /// 获取场景分组统计
        /// </summary>
        public Dictionary<string, int> GetSceneStats()
        {
            return records
                .GroupBy(r => r.scenePath)
                .ToDictionary(g => g.Key, g => g.Count());
        }

        /// <summary>
        /// 清理所有记录（谨慎使用）
        /// </summary>
        public void Clear()
        {
            records.Clear();
            if (idLookup != null)
            {
                idLookup.Clear();
            }
            MarkDirty();
            Debug.Log("[DialogueEventIDRegistry] 已清空所有ID记录");
        }

        /// <summary>
        /// 对记录进行排序，确保顺序固定（先按场景路径，再按对象名称）
        /// 避免Git产生无意义的diff
        /// </summary>
        private void SortRecords()
        {
            records.Sort((a, b) =>
            {
                int sceneCompare = string.Compare(a.scenePath, b.scenePath, System.StringComparison.Ordinal);
                if (sceneCompare != 0)
                    return sceneCompare;
                return string.Compare(a.objectName, b.objectName, System.StringComparison.Ordinal);
            });
        }

        private void MarkDirty()
        {
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
        }

#if UNITY_EDITOR
        /// <summary>
        /// 扫描所有场景，重建注册表（用于初始化或修复）
        /// </summary>
        [MenuItem("Tools/Dialogue System/Rebuild ID Registry")]
        public static void RebuildRegistry()
        {
            if (!EditorUtility.DisplayDialog(
                "重建ID注册表索引",
                "这将重新扫描所有场景，重建ID注册表索引。\n\n" +
                "✅ 组件上的ID不会改变\n" +
                "✅ 不会重新生成ID\n" +
                "✅ 只是重新录入现有ID到注册表\n\n" +
                "用途：Registry数据丢失或不一致时使用。",
                "继续", "取消"))
            {
                return;
            }

            var registry = Instance;
            registry.Clear();

            var enabledScenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .Distinct()
                .ToList();

            if (enabledScenes.Count == 0)
            {
                EditorUtility.DisplayDialog("错误", "Build Settings中没有启用的场景", "确定");
                return;
            }

            // 保存当前场景
            var currentScenes = new List<string>();
            for (int i = 0; i < UnityEditor.SceneManagement.EditorSceneManager.sceneCount; i++)
            {
                var scene = UnityEditor.SceneManagement.EditorSceneManager.GetSceneAt(i);
                if (scene.isLoaded)
                {
                    currentScenes.Add(scene.path);
                }
            }

            int totalFound = 0;

            try
            {
                for (int i = 0; i < enabledScenes.Count; i++)
                {
                    string scenePath = enabledScenes[i];
                    EditorUtility.DisplayProgressBar("重建ID注册表",
                        $"扫描场景 {i + 1}/{enabledScenes.Count}: {System.IO.Path.GetFileNameWithoutExtension(scenePath)}",
                        (float)i / enabledScenes.Count);

                    var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                        scenePath,
                        UnityEditor.SceneManagement.OpenSceneMode.Single);

                    var refs = Object.FindObjectsOfType<DialogueEventTarget>();

                    foreach (var refComp in refs)
                    {
                        if (!string.IsNullOrEmpty(refComp.UniqueID))
                        {
                            registry.Add(refComp.UniqueID, scenePath, refComp.gameObject.name);
                            totalFound++;
                        }
                    }
                }

                EditorUtility.ClearProgressBar();

                // 恢复原来的场景
                if (currentScenes.Count > 0)
                {
                    UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                        currentScenes[0],
                        UnityEditor.SceneManagement.OpenSceneMode.Single);

                    for (int i = 1; i < currentScenes.Count; i++)
                    {
                        UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                            currentScenes[i],
                            UnityEditor.SceneManagement.OpenSceneMode.Additive);
                    }
                }

                AssetDatabase.SaveAssets();

                EditorUtility.DisplayDialog("完成",
                    $"重建完成！\n\n" +
                    $"扫描场景数: {enabledScenes.Count}\n" +
                    $"找到ID数: {totalFound}",
                    "确定");
            }
            catch (System.Exception e)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError($"[DialogueEventIDRegistry] 重建失败: {e.Message}");
                EditorUtility.DisplayDialog("错误", $"重建失败:\n{e.Message}", "确定");
            }
        }
        /// <summary>
        /// 检查Registry中的ID冲突（无需打开场景）
        /// </summary>
        [MenuItem("Tools/Dialogue System/Check All Build Scenes for ID Conflicts")]
        public static void CheckIDConflicts()
        {
            // 首先检查Build Settings中是否有重复场景
            var allScenes = EditorBuildSettings.scenes.ToList();
            var enabledScenes = allScenes.Where(s => s.enabled).ToList();

            if (enabledScenes.Count == 0)
            {
                EditorUtility.DisplayDialog("No Scenes", "No enabled scenes in Build Settings.", "OK");
                return;
            }

            var pathGroups = enabledScenes.GroupBy(s => s.path).OrderBy(g => g.Key);
            var duplicateScenes = pathGroups.Where(g => g.Count() > 1).ToList();

            if (duplicateScenes.Count > 0)
            {
                Debug.LogError("====== Build Settings Issue ======");
                Debug.LogError($"⚠️ Found {duplicateScenes.Count} duplicate scenes in Build Settings!");
                foreach (var group in duplicateScenes)
                {
                    string sceneName = System.IO.Path.GetFileNameWithoutExtension(group.Key);
                    Debug.LogError($"  - '{sceneName}' appears {group.Count()} times");
                }
                Debug.LogError("This can cause false ID conflicts!");
                Debug.LogError("Fix: File > Build Settings, remove duplicates.\n");

                EditorUtility.DisplayDialog("⚠️ Duplicate Scenes!",
                    $"Found {duplicateScenes.Count} duplicate scenes in Build Settings!\n\n" +
                    "This will cause false ID conflicts.\n\n" +
                    "Fix: File > Build Settings\nRemove duplicate scenes first!",
                    "OK");
                return;
            }

            // 检查Registry中的ID冲突
            var registry = Instance;
            var records = registry.GetAllRecords();

            var grouped = records.GroupBy(r => r.id).Where(g => g.Count() > 1).ToList();

            if (grouped.Count == 0)
            {
                Debug.Log($"✅ No ID conflicts found! ({enabledScenes.Count} scenes, {records.Count} IDs)");
                EditorUtility.DisplayDialog("✓ No Conflicts",
                    $"Checked {enabledScenes.Count} scenes.\n{records.Count} IDs.\n\nNo conflicts found!", "OK");
                return;
            }

            // 发现冲突
            string errorMsg = $"⚠️ Found {grouped.Count} ID conflicts!\n\n";

            foreach (var group in grouped)
            {
                errorMsg += $"ID: {group.Key.Substring(0, 8)}... ({group.Count()} objects):\n";
                foreach (var record in group)
                {
                    string sceneName = System.IO.Path.GetFileNameWithoutExtension(record.scenePath);
                    errorMsg += $"  - '{record.objectName}' in [{sceneName}]\n";
                }
                errorMsg += "\n";
            }

            Debug.LogError($"[DialogueEventIDRegistry] {errorMsg}");
            EditorUtility.DisplayDialog("⚠️ Conflicts Found!",
                $"Found {grouped.Count} conflicts!\n\nCheck Console.\n\nUse Fix All Duplicate IDs", "OK");
        }

        [MenuItem("Tools/Dialogue System/Fix All Duplicate IDs")]
        public static void FixDuplicateIDs()
        {
            var registry = Instance;
            var duplicates = registry.GetAllRecords().GroupBy(r => r.id).Where(g => g.Count() > 1).ToList();

            if (duplicates.Count == 0)
            {
                EditorUtility.DisplayDialog("No Conflicts", "No duplicate IDs found.", "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog("⚠️ 警告",
                $"发现 {duplicates.Count} 组重复ID！\n\n建议先备份！\n\n确定继续吗？",
                "确定", "取消"))
                return;

            var currentScenes = SaveCurrentScenes();
            var sceneFixList = new Dictionary<string, List<string>>();

            foreach (var dup in duplicates)
            {
                var refs = dup.ToList();
                for (int i = 1; i < refs.Count; i++)
                {
                    if (!sceneFixList.ContainsKey(refs[i].scenePath))
                        sceneFixList[refs[i].scenePath] = new List<string>();
                    sceneFixList[refs[i].scenePath].Add(dup.Key);
                }
            }

            int totalFixed = 0;
            try
            {
                int idx = 0;
                foreach (var kvp in sceneFixList)
                {
                    idx++;
                    EditorUtility.DisplayProgressBar("Fixing", $"Scene {idx}/{sceneFixList.Count}...", (float)idx / sceneFixList.Count);

                    var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(kvp.Key, UnityEditor.SceneManagement.OpenSceneMode.Single);
                    var targets = Resources.FindObjectsOfTypeAll<DialogueEventTarget>().Where(r => r != null && r.gameObject.scene == scene).ToList();
                    var idsToFix = new HashSet<string>(kvp.Value);

                    foreach (var t in targets)
                    {
                        if (idsToFix.Contains(t.UniqueID))
                        {
                            Undo.RecordObject(t, "Fix ID");
                            t.ForceRegenerateID();
                            totalFixed++;
                        }
                    }

                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
                    UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
                }

                EditorUtility.ClearProgressBar();
                RestoreScenes(currentScenes);
                EditorUtility.DisplayDialog("完成", $"修复了 {totalFixed} 个对象", "确定");
            }
            catch (System.Exception e)
            {
                EditorUtility.ClearProgressBar();
                RestoreScenes(currentScenes);
                Debug.LogError($"[DialogueEventIDRegistry] Error: {e.Message}");
            }
        }

        private static List<string> SaveCurrentScenes()
        {
            var list = new List<string>();
            for (int i = 0; i < UnityEditor.SceneManagement.EditorSceneManager.sceneCount; i++)
            {
                var s = UnityEditor.SceneManagement.EditorSceneManager.GetSceneAt(i);
                if (s.isLoaded) list.Add(s.path);
            }
            return list;
        }

        private static void RestoreScenes(List<string> paths)
        {
            if (paths.Count > 0)
            {
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(paths[0], UnityEditor.SceneManagement.OpenSceneMode.Single);
                for (int i = 1; i < paths.Count; i++)
                    UnityEditor.SceneManagement.EditorSceneManager.OpenScene(paths[i], UnityEditor.SceneManagement.OpenSceneMode.Additive);
            }
        }
#endif
    }
}