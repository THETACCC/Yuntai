using UnityEngine;
using UnityEditor;

/// <summary>
/// NoteBookManager 自定义 Inspector（风格对齐 DialogueEventTargetEditor）。
/// </summary>
[CustomEditor(typeof(NoteBookManager), true)]
[CanEditMultipleObjects]
public class NoteBookManagerEditor : Editor
{
    private NoteBookManager manager;
    private NoteBookLocalization localization;
    private SerializedObject localizationSerializedObject;

    private Vector2 dataUrlScrollPos;
    private Vector2 localizationUrlScrollPos;

    private void OnEnable()
    {
        manager = (NoteBookManager)target;
        localization = manager.GetComponent<NoteBookLocalization>();
        if (localization != null)
            localizationSerializedObject = new SerializedObject(localization);
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        if (localizationSerializedObject != null)
            localizationSerializedObject.Update();

        DrawHeaderSection();
        EditorGUILayout.Space();
        DrawPullButtons();
        EditorGUILayout.Space();
        DrawDefaultFields();

        serializedObject.ApplyModifiedProperties();
        if (localizationSerializedObject != null)
            localizationSerializedObject.ApplyModifiedProperties();
    }

    private void DrawHeaderSection()
    {
        EditorGUILayout.LabelField("Notebook Data Manager", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("远程数据源（Google Sheets CSV URL）", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "URL 仅用于编辑器拉取。运行时只读取 Assets/External/ 下的本地 CSV。",
            EditorStyles.wordWrappedMiniLabel);

        EditorGUILayout.Space(4);
        DrawLocalFileStatus("NoteBookData", NoteBookLocalData.NoteBookDataAssetPath);
        DrawLocalFileStatus("LocalizationTable", NoteBookLocalData.LocalizationTableAssetPath);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(5);

        EditorGUILayout.LabelField("NoteBookData CSV URL", EditorStyles.boldLabel);
        dataUrlScrollPos = EditorGUILayout.BeginScrollView(dataUrlScrollPos, GUILayout.Height(42));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("googleSheetsURL"), GUIContent.none);
        EditorGUILayout.EndScrollView();

        if (localizationSerializedObject != null)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("LocalizationTable CSV URL", EditorStyles.boldLabel);
            localizationUrlScrollPos = EditorGUILayout.BeginScrollView(localizationUrlScrollPos, GUILayout.Height(42));
            EditorGUILayout.PropertyField(localizationSerializedObject.FindProperty("googleSheetsURL"), GUIContent.none);
            EditorGUILayout.EndScrollView();
        }
        else
        {
            EditorGUILayout.HelpBox("未找到 NoteBookLocalization 组件，无法拉取本地化表。", MessageType.Warning);
        }
    }

    private static void DrawLocalFileStatus(string label, string assetPath)
    {
        bool exists = System.IO.File.Exists(assetPath);
        string status = exists ? "✅ 已存在" : "❌ 不存在";
        EditorGUILayout.LabelField($"{label}：{status}  {assetPath}", EditorStyles.miniLabel);
    }

    private void DrawPullButtons()
    {
        EditorGUILayout.BeginHorizontal();
        {
            if (GUILayout.Button("📥 拉取并保存到本地", GUILayout.Height(30)))
            {
                NoteBookPullUtility.PullAndSave(manager, localization, (success, message) =>
                {
                    if (success)
                    {
                        Debug.Log("[NoteBookManager] " + message);
                        EditorUtility.DisplayDialog("拉取成功", message, "确定");
                        Repaint();
                    }
                    else
                    {
                        Debug.LogError("[NoteBookManager] " + message);
                        EditorUtility.DisplayDialog("拉取失败", message, "确定");
                    }
                });
            }

            if (GUILayout.Button("📂 选中本地 CSV", GUILayout.Height(30), GUILayout.Width(120)))
            {
                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(NoteBookLocalData.NoteBookDataAssetPath);
                if (asset != null)
                    Selection.activeObject = asset;
                else
                    EditorUtility.DisplayDialog("提示", "本地 NoteBookData.csv 尚不存在，请先拉取。", "确定");
            }
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawDefaultFields()
    {
        EditorGUILayout.LabelField("组件配置", EditorStyles.boldLabel);
        DrawPropertiesExcluding(serializedObject, "m_Script", "googleSheetsURL");

        if (localizationSerializedObject != null)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("NoteBookLocalization", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(localizationSerializedObject.FindProperty("localizationTable"));
            EditorGUILayout.PropertyField(localizationSerializedObject.FindProperty("currentLanguage"));
            EditorGUILayout.PropertyField(localizationSerializedObject.FindProperty("csvDelimiter"));
            EditorGUILayout.PropertyField(localizationSerializedObject.FindProperty("csvQuoteChar"));
        }
    }
}
