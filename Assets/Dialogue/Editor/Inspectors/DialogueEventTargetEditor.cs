using UnityEngine;
using UnityEditor;
using System.Linq;

/// <summary>
/// DialogueEventTarget 自定义Inspector
/// 显示ID信息和冲突检测
/// </summary>
[CustomEditor(typeof(DialogueEventTarget), true)] // true允许子类也使用
public class DialogueEventTargetEditor : Editor
{
    private DialogueEventTarget script;
    private bool isObsoleteClass = false;

    private void OnEnable()
    {
        script = (DialogueEventTarget)target;

        // 检查是否是旧的DialogueReference类
        isObsoleteClass = script.GetType().Name == "DialogueReference";
    }

    public override void OnInspectorGUI()
    {
        // 如果是旧类，显示警告
        if (isObsoleteClass)
        {
            EditorGUILayout.HelpBox(
                "⚠️ DialogueReference is deprecated!\n\n" +
                "This component still works, but please migrate to DialogueEventTarget.\n\n" +
                "Use: Tools > Dialogue System > Migrate DialogueReference to DialogueEventTarget",
                MessageType.Warning);
            EditorGUILayout.Space();
        }

        // 标题
        string title = isObsoleteClass ? "Dialogue Reference Component (Deprecated)" : "Dialogue Event Target Component";
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // 显示ID
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        {
            EditorGUILayout.LabelField("Unique ID:", EditorStyles.boldLabel);

            if (string.IsNullOrEmpty(script.UniqueID))
            {
                EditorGUILayout.HelpBox("⚠️ No ID assigned!", MessageType.Error);
            }
            else
            {
                // 显示ID（可选择复制）
                EditorGUILayout.SelectableLabel(script.UniqueID, EditorStyles.textField, GUILayout.Height(18));

                // 检查冲突
                CheckForConflicts();
            }
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space();

        // 操作按钮
        EditorGUILayout.BeginHorizontal();
        {
            if (GUILayout.Button("🔄 Regenerate ID"))
            {
                if (EditorUtility.DisplayDialog(
                    "Regenerate ID",
                    "This will generate a new unique ID for this GameObject.\n\n" +
                    "⚠️ WARNING: Any dialogue events referencing this object will break!\n\n" +
                    "You will need to reconfigure them in the Dialogue Tree Editor.",
                    "Continue", "Cancel"))
                {
                    Undo.RecordObject(script, "Regenerate ID");
                    script.ForceRegenerateID();
                    EditorUtility.SetDirty(script);
                }
            }

            if (GUILayout.Button("📋 Copy ID"))
            {
                EditorGUIUtility.systemCopyBuffer = script.UniqueID;
                Debug.Log($"[DialogueEventTarget] ID copied to clipboard: {script.UniqueID}");
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        // 迁移按钮（仅对旧类显示）
        if (isObsoleteClass)
        {
            if (GUILayout.Button("🔧 Migrate This Component to DialogueEventTarget", GUILayout.Height(30)))
            {
                MigrateSingleComponent();
            }
            EditorGUILayout.Space();
        }

        // 信息
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        {
            EditorGUILayout.LabelField("ℹ️ About this component:", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("• Provides a persistent unique ID for this GameObject", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("• Used by the Dialogue System to reference objects in events", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("• Automatically added when selecting objects in Dialogue Events", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("• ID is preserved when copying, but automatically regenerated if duplicate detected", EditorStyles.wordWrappedMiniLabel);
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space();

        // 调试信息
        if (EditorGUILayout.Foldout(SessionState.GetBool("DialogueEventTarget_ShowDebug", false), "Debug Info"))
        {
            SessionState.SetBool("DialogueEventTarget_ShowDebug", true);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            {
                EditorGUILayout.LabelField("Component Type:", script.GetType().Name);
                EditorGUILayout.LabelField("GameObject:", script.gameObject.name);
                EditorGUILayout.LabelField("Scene:", script.gameObject.scene.name);
                EditorGUILayout.LabelField("Scene Path:", script.gameObject.scene.path);
                EditorGUILayout.LabelField("Is Valid Scene:", script.gameObject.scene.IsValid().ToString());

                var registry = DialogueSystem.DialogueEventIDRegistry.Instance;
                bool inRegistry = registry.Contains(script.UniqueID);
                EditorGUILayout.LabelField("In ID Registry:", inRegistry.ToString());

                if (inRegistry)
                {
                    var records = registry.GetAllRecords();
                    var record = records.FirstOrDefault(r => r.id == script.UniqueID);
                    if (record != null)
                    {
                        EditorGUILayout.LabelField("Registry Scene:", record.scenePath);
                        EditorGUILayout.LabelField("Registry Name:", record.objectName);
                    }
                }
            }
            EditorGUILayout.EndVertical();
        }
        else
        {
            SessionState.SetBool("DialogueEventTarget_ShowDebug", false);
        }
    }

    private void CheckForConflicts()
    {
        // 使用IDRegistry检查
        var registry = DialogueSystem.DialogueEventIDRegistry.Instance;

        if (!registry.Contains(script.UniqueID))
        {
            // ID不在注册表中（罕见情况）
            EditorGUILayout.HelpBox("ℹ️ ID not in registry. It will be registered automatically.", MessageType.Info);
            return;
        }

        // 查找是否有其他对象使用相同ID（包括DialogueReference和DialogueEventTarget）
        var allRefs = FindObjectsOfType<DialogueEventTarget>();
        var duplicates = allRefs.Where(r => r != script && r.UniqueID == script.UniqueID).ToList();

        if (duplicates.Count > 0)
        {
            // 发现冲突！
            EditorGUILayout.HelpBox(
                $"⚠️ ID CONFLICT DETECTED!\n\n" +
                $"This ID is used by {duplicates.Count} other object(s):\n" +
                string.Join("\n", duplicates.Select(d => $"• {d.gameObject.name}")),
                MessageType.Error);

            if (GUILayout.Button("🔧 Auto Fix: Regenerate This Object's ID"))
            {
                Undo.RecordObject(script, "Fix ID Conflict");
                script.ForceRegenerateID();
                EditorUtility.SetDirty(script);
                Debug.Log($"[DialogueEventTarget] ID conflict fixed for '{script.gameObject.name}'");
            }
        }
        else
        {
            // 无冲突
            EditorGUILayout.HelpBox("✅ No conflicts detected. ID is unique.", MessageType.Info);
        }
    }

    private void MigrateSingleComponent()
    {
        if (!isObsoleteClass) return;

        var go = script.gameObject;
        string id = script.UniqueID;

        if (EditorUtility.DisplayDialog(
            "Migrate Component",
            $"This will replace DialogueReference with DialogueEventTarget on '{go.name}'.\n\n" +
            "The ID will be preserved.\n\n" +
            "Continue?",
            "Yes", "Cancel"))
        {
            Undo.RecordObject(go, "Migrate DialogueReference");

            // 移除旧组件
            DestroyImmediate(script);

            // 添加新组件
            var newComponent = go.AddComponent<DialogueEventTarget>();

            // 通过反射设置ID（保持原ID）
            var field = typeof(DialogueEventTarget).GetField("uniqueID",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(newComponent, id);
            }

            EditorUtility.SetDirty(go);

            Debug.Log($"[DialogueEventTarget] Migrated '{go.name}' from DialogueReference to DialogueEventTarget (ID preserved: {id})");
            EditorUtility.DisplayDialog("Success", $"Component migrated!\n\nID preserved: {id.Substring(0, 8)}...", "OK");
        }
    }
}