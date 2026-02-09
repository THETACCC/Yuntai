using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DialogueSystem;
using System;

// 简单的输入对话框辅助类
public class EditorInputDialogue : EditorWindow
{
    private string inputText = "";
    private string dialogTitle = "";
    private string message = "";
    private System.Action<string> onResult;

    public static void ShowAsync(string title, string message, string defaultValue, System.Action<string> onResult)
    {
        var window = CreateInstance<EditorInputDialogue>();
        window.titleContent = new GUIContent(title);
        window.dialogTitle = title;
        window.message = message;
        window.inputText = defaultValue;
        window.minSize = new Vector2(300, 100);
        window.maxSize = new Vector2(300, 100);
        window.onResult = onResult;
        window.ShowUtility();
    }

    private void OnDestroy()
    {
        onResult?.Invoke(inputText);
    }

    private void OnGUI()
    {
        if (position.width <= 0 || position.height <= 0)
            return;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField(message, EditorStyles.wordWrappedLabel);
        EditorGUILayout.Space(5);

        GUI.SetNextControlName("InputField");
        inputText = EditorGUILayout.TextField(inputText);

        EditorGUILayout.Space(10);

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        if (GUILayout.Button("OK", GUILayout.Width(80)))
        {
            Close();
        }

        if (GUILayout.Button("Cancel", GUILayout.Width(80)))
        {
            inputText = "";
            Close();
        }

        EditorGUILayout.EndHorizontal();

        if (Event.current.type == EventType.Layout)
        {
            EditorGUI.FocusTextInControl("InputField");
        }

        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return)
        {
            Close();
        }
    }
}