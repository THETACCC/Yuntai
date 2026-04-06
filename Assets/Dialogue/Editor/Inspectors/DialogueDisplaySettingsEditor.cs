using TMPro;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DialogueDisplaySettings))]
public class DialogueDisplaySettingsEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DialogueDisplaySettings settings = (DialogueDisplaySettings)target;

        settings.currentLanguage = EditorGUILayout.TextField("Dialogue Language", settings.currentLanguage);
        //settings.ChineseFont = (TMP_FontAsset)EditorGUILayout.ObjectField("Chinese Font", settings.ChineseFont, typeof(TMP_FontAsset), false);
        //settings.EnglishFont = (TMP_FontAsset)EditorGUILayout.ObjectField("English Font", settings.EnglishFont, typeof(TMP_FontAsset), false);
        //settings.JapaneseFont = (TMP_FontAsset)EditorGUILayout.ObjectField("Japanese Font", settings.JapaneseFont, typeof(TMP_FontAsset), false);

        EditorGUILayout.Space(); // 顶部空一行
        EditorGUILayout.LabelField("Template Settings", EditorStyles.boldLabel);
        settings.separatePlayerAndNPC = EditorGUILayout.Toggle("Separate Player and NPC", settings.separatePlayerAndNPC);


        // 如果 separatePlayerAndNPC 勾选了，显示额外字段
        if (settings.separatePlayerAndNPC)
        {
            EditorGUI.indentLevel++;
            settings.inactiveAvatarColor = EditorGUILayout.ColorField("Inactive Avatar Color", settings.inactiveAvatarColor);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space();
        settings.showDialogueHistory = EditorGUILayout.Toggle("Show Dialogue History", settings.showDialogueHistory);



        // ✅ 智能显示引用字段逻辑
        ShowReferenceIfNull("Dialogue Manager", ref settings.dialogueController);
        //ShowReferenceIfNull("Avatar Prefab", ref settings.avatarPrefab);
        //ShowReferenceIfNull("Name Prefab", ref settings.namePrefab);

        // 保存更改
        if (GUI.changed)
        {
            EditorUtility.SetDirty(settings);
        }
    }

    /// <summary>
    /// 仅当引用为空时才显示 ObjectField
    /// </summary>
    void ShowReferenceIfNull<T>(string label, ref T reference) where T : Object
    {
        if (reference == null)
        {
            EditorGUILayout.HelpBox($"{label} is not assigned!", MessageType.Warning);
            reference = (T)EditorGUILayout.ObjectField(label, reference, typeof(T), true);
        }
    }
}