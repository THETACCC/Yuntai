#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(UI_E), true)]
public class UI_E_Editor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawPropertiesExcluding(
            serializedObject,
            "m_Script",
            "triggerManagerFunctionOnE",
            "targetLevelManager",
            "onEPressed"
        );

        SerializedProperty triggerBool = serializedObject.FindProperty("triggerManagerFunctionOnE");
        SerializedProperty managerProp = serializedObject.FindProperty("targetLevelManager");
        SerializedProperty eventProp = serializedObject.FindProperty("onEPressed");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("E Press Trigger", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(triggerBool);

        if (triggerBool.boolValue)
        {
            EditorGUILayout.PropertyField(managerProp);

            if (managerProp.objectReferenceValue != null)
            {
                EditorGUILayout.PropertyField(eventProp);
            }
            else
            {
                EditorGUILayout.HelpBox("Assign a BaseLevelManager first.", MessageType.Info);
            }
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif