using UnityEngine;
using System.Collections.Generic;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DialogueSystem
{
    /// <summary>
    /// 对话系统ID注册表 - 自动管理所有DialogueReference的唯一ID
    /// 用户无需手动维护，完全自动化
    /// </summary>
    public class IDRegistry : ScriptableObject
    {
        private static IDRegistry instance;

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

        public static IDRegistry Instance
        {
            get
            {
                if (instance == null)
                {
#if UNITY_EDITOR
                    // 尝试加载已存在的注册表
                    instance = AssetDatabase.FindAssets("t:IDRegistry")
                        .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                        .Select(path => AssetDatabase.LoadAssetAtPath<IDRegistry>(path))
                        .FirstOrDefault();

                    // 如果不存在，创建新的
                    if (instance == null)
                    {
                        instance = CreateInstance<IDRegistry>();

                        // 保存到Assets根目录
                        string path = "Assets/DialogueIDRegistry.asset";
                        AssetDatabase.CreateAsset(instance, path);
                        AssetDatabase.SaveAssets();

                        Debug.Log($"[IDRegistry] 创建新的ID注册表: {path}");
                    }
#else
                    Debug.LogError("[IDRegistry] Cannot create IDRegistry at runtime!");
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
                Debug.Log($"[IDRegistry] 已清理场景 '{scenePath}' 的 {toRemove.Count} 个ID记录");
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
            Debug.Log("[IDRegistry] 已清空所有ID记录");
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
                "重建ID注册表",
                "这将扫描所有Build Settings中的场景，重建ID注册表。\n\n" +
                "注意：这会清空现有注册表数据。\n\n" +
                "建议：仅在初始化项目或修复问题时使用。",
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

                    var refs = Object.FindObjectsOfType<DialogueReference>();

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
                Debug.LogError($"[IDRegistry] 重建失败: {e.Message}");
                EditorUtility.DisplayDialog("错误", $"重建失败:\n{e.Message}", "确定");
            }
        }

        /// <summary>
        /// 查看ID注册表内容
        /// </summary>
        [MenuItem("Tools/Dialogue System/View ID Registry")]
        public static void ViewRegistry()
        {
            var registry = Instance;
            Selection.activeObject = registry;
            EditorGUIUtility.PingObject(registry);
        }
#endif
    }
}