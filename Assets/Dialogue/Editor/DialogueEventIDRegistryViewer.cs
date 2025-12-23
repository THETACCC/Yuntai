using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace DialogueSystem
{
    /// <summary>
    /// 对话事件ID注册表查看器 - 可视化显示所有注册的ID
    /// </summary>
    public class DialogueEventIDRegistryViewer : EditorWindow
    {
        private Vector2 scrollPosition;
        private string searchFilter = "";
        private bool groupByScene = true;

        [MenuItem("Tools/Dialogue System/Event ID Registry Viewer")]
        public static void ShowWindow()
        {
            var window = GetWindow<DialogueEventIDRegistryViewer>("Event ID Registry");
            window.minSize = new Vector2(600, 400);
            window.Show();
        }

        private void OnGUI()
        {
            var registry = DialogueEventIDRegistry.Instance;

            // 标题
            EditorGUILayout.LabelField("Dialogue Event ID Registry", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // 工具栏
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            {
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
                {
                    Repaint();
                }

                if (GUILayout.Button("Rebuild Registry", EditorStyles.toolbarButton, GUILayout.Width(120)))
                {
                    DialogueEventIDRegistry.RebuildRegistry();
                    Repaint();
                }

                GUILayout.FlexibleSpace();

                GUILayout.Label("Search:", GUILayout.Width(50));
                searchFilter = EditorGUILayout.TextField(searchFilter, EditorStyles.toolbarTextField, GUILayout.Width(200));

                if (GUILayout.Button("×", EditorStyles.toolbarButton, GUILayout.Width(20)))
                {
                    searchFilter = "";
                    GUI.FocusControl(null);
                }
            }
            EditorGUILayout.EndHorizontal();

            // 统计信息
            var records = registry.GetAllRecords();
            int totalCount = records.Count;

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            {
                EditorGUILayout.LabelField($"Total IDs: {totalCount}", EditorStyles.miniLabel);

                var sceneStats = registry.GetSceneStats();
                EditorGUILayout.LabelField($"Scenes: {sceneStats.Count}", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // 显示选项
            EditorGUILayout.BeginHorizontal();
            {
                groupByScene = EditorGUILayout.ToggleLeft("Group by Scene", groupByScene, GUILayout.Width(150));
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // 过滤记录
            var filteredRecords = records;
            if (!string.IsNullOrEmpty(searchFilter))
            {
                filteredRecords = records.Where(r =>
                    r.id.Contains(searchFilter) ||
                    r.objectName.ToLower().Contains(searchFilter.ToLower()) ||
                    r.scenePath.ToLower().Contains(searchFilter.ToLower())
                ).ToList();
            }

            // 显示记录
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            {
                if (filteredRecords.Count == 0)
                {
                    EditorGUILayout.HelpBox("No records found.", MessageType.Info);
                }
                else
                {
                    if (groupByScene)
                    {
                        DrawGroupedByScene(filteredRecords);
                    }
                    else
                    {
                        DrawFlatList(filteredRecords);
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawGroupedByScene(List<DialogueEventIDRegistry.IDRecord> records)
        {
            var grouped = records.GroupBy(r => r.scenePath).OrderBy(g => g.Key);

            foreach (var group in grouped)
            {
                string sceneName = System.IO.Path.GetFileNameWithoutExtension(group.Key);

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                {
                    // 场景标题
                    EditorGUILayout.BeginHorizontal();
                    {
                        EditorGUILayout.LabelField($"📁 {sceneName} ({group.Count()} IDs)", EditorStyles.boldLabel);

                        if (GUILayout.Button("Open Scene", GUILayout.Width(100)))
                        {
                            if (UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                            {
                                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(group.Key);
                            }
                        }
                    }
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.LabelField(group.Key, EditorStyles.miniLabel);
                    EditorGUILayout.Space(5);

                    // 该场景的所有记录
                    foreach (var record in group.OrderBy(r => r.objectName))
                    {
                        DrawRecord(record);
                    }
                }
                EditorGUILayout.EndVertical();

                EditorGUILayout.Space(5);
            }
        }

        private void DrawFlatList(List<DialogueEventIDRegistry.IDRecord> records)
        {
            foreach (var record in records.OrderBy(r => r.objectName))
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                DrawRecord(record);
                EditorGUILayout.EndVertical();
            }
        }

        private void DrawRecord(DialogueEventIDRegistry.IDRecord record)
        {
            EditorGUILayout.BeginHorizontal();
            {
                // 对象名称
                EditorGUILayout.LabelField($"🎮 {record.objectName}", GUILayout.Width(200));

                // ID（可选择复制）
                EditorGUILayout.SelectableLabel(record.id, EditorStyles.miniLabel, GUILayout.Height(16));

                // 查找按钮
                if (GUILayout.Button("Find", GUILayout.Width(50)))
                {
                    FindAndSelectObject(record);
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void FindAndSelectObject(DialogueEventIDRegistry.IDRecord record)
        {
            // 尝试在当前加载的场景中查找
            var obj = DialogueEventTarget.FindByID(record.id);

            if (obj != null)
            {
                Selection.activeGameObject = obj;
                EditorGUIUtility.PingObject(obj);
                Debug.Log($"[DialogueEventIDRegistry] Found object '{record.objectName}' with ID {record.id}");
            }
            else
            {
                // 对象不在当前场景中，询问是否打开场景
                if (EditorUtility.DisplayDialog(
                    "Object Not Found",
                    $"GameObject '{record.objectName}' is not in the currently loaded scene(s).\n\n" +
                    $"Scene: {System.IO.Path.GetFileNameWithoutExtension(record.scenePath)}\n\n" +
                    "Open the scene?",
                    "Yes", "No"))
                {
                    if (UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    {
                        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                            record.scenePath,
                            UnityEditor.SceneManagement.OpenSceneMode.Single);

                        // 场景加载后再次查找
                        obj = DialogueEventTarget.FindByID(record.id);
                        if (obj != null)
                        {
                            Selection.activeGameObject = obj;
                            EditorGUIUtility.PingObject(obj);
                        }
                        else
                        {
                            Debug.LogWarning($"[DialogueEventIDRegistry] Object not found even after loading scene. The object may have been deleted.");
                        }
                    }
                }
            }
        }
    }
}