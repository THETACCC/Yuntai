using UnityEngine;
using System.Collections.Generic;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 为GameObject提供持久化的唯一ID，用于对话系统的引用
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
    private static HashSet<string> registeredIDs = new HashSet<string>();
#endif

    private void OnValidate()
    {
#if UNITY_EDITOR
        if (string.IsNullOrEmpty(uniqueID))
        {
            GenerateNewID();
            return;
        }

        if (Application.isPlaying && gameObject.scene.name == "DontDestroyOnLoad")
        {
            return;
        }

        if (IsDuplicateID(uniqueID))
        {
            var duplicates = FindDuplicateObjects(uniqueID);
            Debug.LogError($"⚠️ [DialogueReference] ID CONFLICT!\nGameObject '{gameObject.name}' has duplicate ID: {uniqueID}\nConflicts with: {string.Join(", ", duplicates.Select(d => $"'{d.gameObject.name}'"))}\nFix: Tools > Dialogue System > Fix All Duplicate IDs", this);
        }
#else
        if (string.IsNullOrEmpty(uniqueID))
        {
            Debug.LogError($"[DialogueReference] GameObject '{gameObject.name}' 没有ID！");
        }
#endif
    }

    private void Awake()
    {
        if (string.IsNullOrEmpty(uniqueID))
        {
            Debug.LogError($"[DialogueReference] GameObject '{gameObject.name}' 没有ID！");
        }
    }

#if UNITY_EDITOR
    private bool IsDuplicateID(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;

        var allRefs = Resources.FindObjectsOfTypeAll<DialogueReference>();
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

    private List<DialogueReference> FindDuplicateObjects(string id)
    {
        if (string.IsNullOrEmpty(id)) return new List<DialogueReference>();

        var allRefs = Resources.FindObjectsOfTypeAll<DialogueReference>();
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
            Debug.Log($"[DialogueReference] GameObject '{gameObject.name}' ID已更新: {oldID} → {uniqueID}");
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
    }

    public static GameObject FindByID(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        var allRefs = Resources.FindObjectsOfTypeAll<DialogueReference>();
        var target = System.Array.Find(allRefs, x => x.UniqueID == id && x.gameObject.scene.IsValid());

        return target?.gameObject;
    }

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
    /// 检查所有 Build Settings 场景的ID冲突
    /// </summary>
    [MenuItem("Tools/Dialogue System/Check All Build Scenes for ID Conflicts")]
    public static void CheckAllBuildScenesForConflicts()
    {
        var allScenes = EditorBuildSettings.scenes.ToList();
        var enabledScenes = allScenes.Where(s => s.enabled).ToList();

        if (enabledScenes.Count == 0)
        {
            EditorUtility.DisplayDialog("No Scenes", "No enabled scenes found in Build Settings.", "OK");
            return;
        }

        // 首先检查 Build Settings 中是否有重复场景
        Debug.Log("====== Checking Build Settings ======");
        var pathGroups = enabledScenes.GroupBy(s => s.path).OrderBy(g => g.Key);
        var duplicateScenes = pathGroups.Where(g => g.Count() > 1).ToList();

        if (duplicateScenes.Count > 0)
        {
            Debug.LogError($"⚠️ Found {duplicateScenes.Count} duplicate scenes in Build Settings!");
            foreach (var group in duplicateScenes)
            {
                string sceneName = System.IO.Path.GetFileNameWithoutExtension(group.Key);
                Debug.LogError($"  - '{sceneName}' appears {group.Count()} times in Build Settings!");
            }
            Debug.LogError("This will cause false positives in ID conflict detection!");
            Debug.LogError("Fix: Go to File > Build Settings and remove duplicate scenes.\n");
        }
        else
        {
            Debug.Log($"✓ No duplicate scenes in Build Settings ({enabledScenes.Count} unique scenes).\n");
        }

        // 获取唯一的场景路径列表
        var uniqueScenes = pathGroups.Select(g => g.Key).ToList();

        var idRegistry = new Dictionary<string, List<(string scenePath, string objectPath)>>();
        var currentScenes = SaveCurrentScenes();

        try
        {
            EditorUtility.DisplayProgressBar("Checking ID Conflicts", "Scanning scenes...", 0f);

            Debug.Log("====== Scanning Scenes for ID Conflicts ======");

            for (int i = 0; i < uniqueScenes.Count; i++)
            {
                var scenePath = uniqueScenes[i];
                EditorUtility.DisplayProgressBar("Checking ID Conflicts",
                    $"Scanning {System.IO.Path.GetFileNameWithoutExtension(scenePath)}...",
                    (float)i / uniqueScenes.Count);

                var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);

                var sceneRefs = Resources.FindObjectsOfTypeAll<DialogueReference>()
                    .Where(r => r != null && r.gameObject.scene == scene)
                    .ToList();

                foreach (var refComp in sceneRefs)
                {
                    string id = refComp.uniqueID;
                    if (string.IsNullOrEmpty(id)) continue;

                    string objPath = GetGameObjectPath(refComp.gameObject);

                    if (!idRegistry.ContainsKey(id))
                    {
                        idRegistry[id] = new List<(string, string)>();
                    }
                    idRegistry[id].Add((scenePath, objPath));
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            RestoreScenes(currentScenes);
        }

        var duplicates = idRegistry.Where(kvp => kvp.Value.Count > 1).ToList();

        if (duplicates.Count > 0)
        {
            string errorMsg = $"⚠️ Found {duplicates.Count} ID conflicts!\n\n";

            foreach (var dup in duplicates)
            {
                errorMsg += $"ID: {dup.Key.Substring(0, 8)}... ({dup.Value.Count} objects):\n";
                foreach (var (scenePath, objPath) in dup.Value)
                {
                    string sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
                    errorMsg += $"  - '{objPath}' in [{sceneName}]\n";
                }
                errorMsg += "\n";
            }

            Debug.LogError($"[DialogueReference] {errorMsg}");

            // 根据是否有重复场景给出不同的提示
            string dialogMsg;
            if (duplicateScenes.Count > 0)
            {
                dialogMsg = $"Found {duplicates.Count} ID conflicts!\n\n⚠️ WARNING: You have {duplicateScenes.Count} duplicate scenes in Build Settings!\n\nFix duplicate scenes first:\nFile > Build Settings\n\nThen re-run this check.";
            }
            else
            {
                dialogMsg = $"Found {duplicates.Count} ID conflicts!\n\nCheck Console for details.\n\nUse: Tools > Dialogue System > Fix All Duplicate IDs";
            }

            EditorUtility.DisplayDialog("⚠️ ID Conflicts Found!", dialogMsg, "OK");
        }
        else
        {
            if (duplicateScenes.Count > 0)
            {
                Debug.LogWarning("No ID conflicts found, but you have duplicate scenes in Build Settings.");
                EditorUtility.DisplayDialog("⚠️ Warning",
                    $"No ID conflicts found.\n\nBut you have {duplicateScenes.Count} duplicate scenes in Build Settings!\n\nFix: File > Build Settings", "OK");
            }
            else
            {
                Debug.Log($"✓ No ID conflicts found. All {uniqueScenes.Count} scenes checked.");
                EditorUtility.DisplayDialog("✓ No Conflicts",
                    $"Checked {uniqueScenes.Count} scenes.\n\nNo ID conflicts found!", "OK");
            }
        }
    }

    /// <summary>
    /// 调试工具：显示当前场景所有 DialogueReference 的详细信息
    /// </summary>
    [MenuItem("Tools/Dialogue System/Debug: Show All DialogueReference in Current Scene")]
    public static void DebugShowAllReferencesInCurrentScene()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

        var sceneRefs = Resources.FindObjectsOfTypeAll<DialogueReference>()
            .Where(r => r != null && r.gameObject.scene == scene)
            .ToList();

        Debug.Log($"====== DialogueReference in Scene: {scene.name} ======");
        Debug.Log($"Total found: {sceneRefs.Count}");
        Debug.Log("");

        var idGroups = sceneRefs.GroupBy(r => r.uniqueID).OrderBy(g => g.Key);

        foreach (var group in idGroups)
        {
            string id = group.Key;
            var refs = group.ToList();

            if (refs.Count > 1)
            {
                Debug.LogError($"⚠️ DUPLICATE ID: {id.Substring(0, 8)}... ({refs.Count} objects)");
            }
            else
            {
                Debug.Log($"ID: {id.Substring(0, 8)}...");
            }

            foreach (var refComp in refs)
            {
                string path = GetGameObjectPath(refComp.gameObject);
                string activeStatus = refComp.gameObject.activeInHierarchy ? "Active" : "Inactive";
                string enabledStatus = refComp.gameObject.activeSelf ? "Self-Enabled" : "Self-Disabled";

                Debug.Log($"  └─ {path}");
                Debug.Log($"     Status: {activeStatus}, {enabledStatus}");
                Debug.Log($"     InstanceID: {refComp.GetInstanceID()}");
                Debug.Log($"", refComp.gameObject);  // 可点击定位
            }
            Debug.Log("");
        }

        EditorUtility.DisplayDialog("Debug Complete",
            $"Found {sceneRefs.Count} DialogueReference objects.\nCheck Console for details.", "OK");
    }

    /// <summary>
    /// 修复所有 Build Settings 场景中的重复ID
    /// 策略：按场景处理，在场景打开时直接修复，避免对象查找问题
    /// </summary>
    [MenuItem("Tools/Dialogue System/Fix All Duplicate IDs")]
    public static void FixAllDuplicateIDs()
    {
        var allScenes = EditorBuildSettings.scenes.ToList();
        var enabledScenes = allScenes.Where(s => s.enabled).ToList();

        if (enabledScenes.Count == 0)
        {
            EditorUtility.DisplayDialog("No Scenes", "No enabled scenes found in Build Settings.", "OK");
            return;
        }

        // 检查是否有重复场景
        var pathGroups = enabledScenes.GroupBy(s => s.path);
        var duplicateScenes = pathGroups.Where(g => g.Count() > 1).ToList();

        if (duplicateScenes.Count > 0)
        {
            string warningMsg = $"⚠️ Warning: Found {duplicateScenes.Count} duplicate scenes in Build Settings:\n\n";
            foreach (var group in duplicateScenes)
            {
                string sceneName = System.IO.Path.GetFileNameWithoutExtension(group.Key);
                warningMsg += $"- '{sceneName}' appears {group.Count()} times\n";
            }
            warningMsg += "\nPlease remove duplicates from Build Settings first!\n\nContinue anyway?";

            if (!EditorUtility.DisplayDialog("Duplicate Scenes Detected", warningMsg, "Continue Anyway", "Cancel"))
            {
                return;
            }
        }

        if (!EditorUtility.DisplayDialog("⚠️ 警告",
            "此操作会扫描所有 Build Settings 场景并修复重复ID！\n\n建议先备份！\n\n确定继续吗？",
            "确定", "取消"))
        {
            return;
        }

        // 使用唯一场景列表
        var uniqueScenes = pathGroups.Select(g => g.Key).ToList();
        var currentScenes = SaveCurrentScenes();

        // 第一遍：收集所有ID和它们的引用
        var allIDRefs = new Dictionary<string, List<(string scenePath, DialogueReference refComp)>>();

        try
        {
            EditorUtility.DisplayProgressBar("Scanning", "Step 1/2: Collecting all IDs...", 0f);

            for (int i = 0; i < uniqueScenes.Count; i++)
            {
                var scenePath = uniqueScenes[i];
                EditorUtility.DisplayProgressBar("Scanning",
                    $"Step 1/2: {System.IO.Path.GetFileNameWithoutExtension(scenePath)}...",
                    (float)i / uniqueScenes.Count / 2f);

                var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);

                var sceneRefs = Resources.FindObjectsOfTypeAll<DialogueReference>()
                    .Where(r => r != null && r.gameObject.scene == scene)
                    .ToList();

                foreach (var refComp in sceneRefs)
                {
                    string id = refComp.uniqueID;
                    if (string.IsNullOrEmpty(id)) continue;

                    if (!allIDRefs.ContainsKey(id))
                    {
                        allIDRefs[id] = new List<(string, DialogueReference)>();
                    }
                    allIDRefs[id].Add((scenePath, refComp));
                }
            }

            // 找出所有重复的ID
            var duplicateIDs = allIDRefs.Where(kvp => kvp.Value.Count > 1).ToList();

            if (duplicateIDs.Count == 0)
            {
                EditorUtility.ClearProgressBar();
                RestoreScenes(currentScenes);
                EditorUtility.DisplayDialog("检查完成", "没有发现重复ID", "确定");
                return;
            }

            // 第二遍：按场景分组修复
            // 先按场景分组所有需要修复的对象
            var sceneFixList = new Dictionary<string, List<DialogueReference>>();

            foreach (var dup in duplicateIDs)
            {
                string duplicateID = dup.Key;
                var refs = dup.Value;

                Debug.LogWarning($"[DialogueReference] 发现重复ID: {duplicateID}，共 {refs.Count} 个对象");

                // 保留第一个，修复其他的
                for (int i = 1; i < refs.Count; i++)
                {
                    var (scenePath, refComp) = refs[i];

                    if (!sceneFixList.ContainsKey(scenePath))
                    {
                        sceneFixList[scenePath] = new List<DialogueReference>();
                    }
                    sceneFixList[scenePath].Add(refComp);
                }
            }

            // 逐个场景打开并修复
            int totalFixed = 0;
            int sceneIndex = 0;

            foreach (var scenePath in sceneFixList.Keys)
            {
                sceneIndex++;
                EditorUtility.DisplayProgressBar("Fixing",
                    $"Step 2/2: Fixing scene {sceneIndex}/{sceneFixList.Count}: {System.IO.Path.GetFileNameWithoutExtension(scenePath)}...",
                    0.5f + (float)sceneIndex / sceneFixList.Count / 2f);

                // 打开场景
                var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);

                // 获取场景中所有DialogueReference（因为场景重新加载，需要重新获取）
                var currentSceneRefs = Resources.FindObjectsOfTypeAll<DialogueReference>()
                    .Where(r => r != null && r.gameObject.scene == scene)
                    .ToList();

                // 需要修复的ID列表
                var refsToFix = sceneFixList[scenePath];
                var oldIDsToFix = refsToFix.Select(r => r.uniqueID).ToHashSet();

                // 在当前场景中找到这些ID对应的对象并修复
                int fixedInScene = 0;
                foreach (var refComp in currentSceneRefs)
                {
                    if (oldIDsToFix.Contains(refComp.uniqueID))
                    {
                        string oldID = refComp.uniqueID;
                        string objPath = GetGameObjectPath(refComp.gameObject);

                        Undo.RecordObject(refComp, "Fix Duplicate ID");
                        refComp.ForceRegenerateID();

                        fixedInScene++;
                        totalFixed++;

                        Debug.Log($"[DialogueReference] 已修复 '{objPath}' (场景: {System.IO.Path.GetFileNameWithoutExtension(scenePath)}, 旧ID: {oldID.Substring(0, 8)}...)");
                    }
                }

                // 保存场景
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);

                Debug.Log($"[DialogueReference] 场景 {System.IO.Path.GetFileNameWithoutExtension(scenePath)} 修复完成，共修复 {fixedInScene} 个对象");
            }

            EditorUtility.ClearProgressBar();
            RestoreScenes(currentScenes);

            EditorUtility.DisplayDialog("修复完成",
                $"修复了 {duplicateIDs.Count} 组重复ID\n总共 {totalFixed} 个对象\n保存了 {sceneFixList.Count} 个场景", "确定");
        }
        catch (System.Exception e)
        {
            EditorUtility.ClearProgressBar();
            RestoreScenes(currentScenes);
            Debug.LogError($"[DialogueReference] 修复过程出错: {e.Message}\n{e.StackTrace}");
            EditorUtility.DisplayDialog("错误", $"修复过程出错:\n{e.Message}", "确定");
        }
    }

    private static List<string> SaveCurrentScenes()
    {
        var currentScenes = new List<string>();
        for (int i = 0; i < UnityEditor.SceneManagement.EditorSceneManager.sceneCount; i++)
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetSceneAt(i);
            if (scene.isLoaded)
            {
                currentScenes.Add(scene.path);
            }
        }
        return currentScenes;
    }

    private static void RestoreScenes(List<string> scenePaths)
    {
        if (scenePaths.Count > 0)
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePaths[0], UnityEditor.SceneManagement.OpenSceneMode.Single);
            for (int i = 1; i < scenePaths.Count; i++)
            {
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePaths[i], UnityEditor.SceneManagement.OpenSceneMode.Additive);
            }
        }
    }

    [InitializeOnLoadMethod]
    private static void InitializePlayModeCheck()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            CheckAllIDConflictsBeforePlay();
        }
    }

    private static void CheckAllIDConflictsBeforePlay()
    {
        var allScenes = EditorBuildSettings.scenes.ToList();
        var enabledScenes = allScenes.Where(s => s.enabled).ToList();

        if (enabledScenes.Count == 0)
        {
            Debug.LogWarning("[DialogueReference] No scenes in Build Settings.");
            return;
        }

        // 使用唯一场景列表
        var uniqueScenes = enabledScenes.GroupBy(s => s.path).Select(g => g.Key).ToList();

        var idRegistry = new Dictionary<string, List<(string scenePath, string objectPath)>>();
        var currentScenes = SaveCurrentScenes();

        try
        {
            foreach (var scenePath in uniqueScenes)
            {
                var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);

                var sceneRefs = Resources.FindObjectsOfTypeAll<DialogueReference>()
                    .Where(r => r != null && r.gameObject.scene == scene)
                    .ToList();

                foreach (var refComp in sceneRefs)
                {
                    string id = refComp.uniqueID;
                    if (string.IsNullOrEmpty(id)) continue;

                    string objPath = GetGameObjectPath(refComp.gameObject);

                    if (!idRegistry.ContainsKey(id))
                    {
                        idRegistry[id] = new List<(string, string)>();
                    }
                    idRegistry[id].Add((scenePath, objPath));
                }
            }
        }
        finally
        {
            RestoreScenes(currentScenes);
        }

        var duplicates = idRegistry.Where(kvp => kvp.Value.Count > 1).ToList();

        if (duplicates.Count > 0)
        {
            string errorMsg = "⚠️ ID CONFLICTS! Cannot Play!\n\nCRITICAL for DontDestroyOnLoad!\n\n";

            foreach (var dup in duplicates)
            {
                errorMsg += $"ID: {dup.Key.Substring(0, 8)}... ({dup.Value.Count} objects):\n";
                foreach (var (scenePath, objPath) in dup.Value)
                {
                    string sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
                    errorMsg += $"  - '{objPath}' [{sceneName}]\n";
                }
                errorMsg += "\n";
            }

            errorMsg += "Fix: Tools > Dialogue System > Fix All Duplicate IDs";

            Debug.LogError($"[DialogueReference] {errorMsg}");
            EditorApplication.isPlaying = false;

            EditorUtility.DisplayDialog("⚠️ ID Conflict!",
                $"Found {duplicates.Count} ID conflicts!\n\nCannot enter Play Mode!\n\nCheck Console.", "OK");
        }
    }
#endif
}