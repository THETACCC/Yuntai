using UnityEngine;
using UnityEditor;
using System.Linq;

/// <summary>
/// DialogueReference 自定义Inspector
/// 显示ID信息和冲突检测
/// </summary>
[CustomEditor(typeof(DialogueReference))]
public class DialogueReferenceEditor : Editor
{
    private DialogueReference script;

    private void OnEnable()
    {
        script = (DialogueReference)target;
    }

    public override void OnInspectorGUI()
    {
        // 标题
        EditorGUILayout.LabelField("Dialogue Reference Component", EditorStyles.boldLabel);
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
                Debug.Log($"[DialogueReference] ID copied to clipboard: {script.UniqueID}");
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        // 信息
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        {
            EditorGUILayout.LabelField("ℹ️ About this component:", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("• Provides a persistent unique ID for this GameObject", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("• Used by the Dialogue System to reference objects", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("• Automatically added when selecting objects in Dialogue Events", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("• ID is preserved when copying, but automatically regenerated if duplicate detected", EditorStyles.wordWrappedMiniLabel);
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space();

        // 调试信息
        if (EditorGUILayout.Foldout(SessionState.GetBool("DialogueReference_ShowDebug", false), "Debug Info"))
        {
            SessionState.SetBool("DialogueReference_ShowDebug", true);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            {
                EditorGUILayout.LabelField("GameObject:", script.gameObject.name);
                EditorGUILayout.LabelField("Scene:", script.gameObject.scene.name);
                EditorGUILayout.LabelField("Scene Path:", script.gameObject.scene.path);
                EditorGUILayout.LabelField("Is Valid Scene:", script.gameObject.scene.IsValid().ToString());

#if UNITY_EDITOR
                var registry = DialogueSystem.IDRegistry.Instance;
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
#endif
            }
            EditorGUILayout.EndVertical();
        }
        else
        {
            SessionState.SetBool("DialogueReference_ShowDebug", false);
        }
    }

    private void CheckForConflicts()
    {
#if UNITY_EDITOR
        // 使用IDRegistry检查
        var registry = DialogueSystem.IDRegistry.Instance;

        if (!registry.Contains(script.UniqueID))
        {
            // ID不在注册表中（罕见情况）
            EditorGUILayout.HelpBox("ℹ️ ID not in registry. It will be registered automatically.", MessageType.Info);
            return;
        }

        // 查找是否有其他对象使用相同ID
        var allRefs = FindObjectsOfType<DialogueReference>();
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
                Debug.Log($"[DialogueReference] ID conflict fixed for '{script.gameObject.name}'");
            }
        }
        else
        {
            // 无冲突
            EditorGUILayout.HelpBox("✅ No conflicts detected. ID is unique.", MessageType.Info);
        }
#endif
    }
}