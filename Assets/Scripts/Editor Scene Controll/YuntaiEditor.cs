using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using SKCell;
[ExecuteInEditMode]

public class YuntaiEditor : EditorWindow
{
    static SceneTitle sceneTitle;
    static string lastActiveScene;
    static int spawnPoint = 0;
    static LanguageSupport language;
    // static StartTeleportType startTeleportType;
    static bool waitForReset = false;

    static GameObject cam_Preview;

    private SceneTitle selectedSceneTitle;

    [MenuItem("Yuntai/Scene Controller")]
    public static void Initialize()
    {
        YuntaiEditor window = GetWindow<YuntaiEditor>("Scene Controller");
        Texture HierarchyIcon = AssetDatabase.LoadAssetAtPath<Texture>("Assets/Editor/Textures/SceneController.png");
        GUIContent content = new GUIContent("Scene Controller", HierarchyIcon);
        window.titleContent = content;
    }

    private void OnGUI()
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Start Mode:", GUILayout.Width(70));
        //startTeleportType = (StartTeleportType)PlayerPrefs.GetInt("StartTeleportType");
        //startTeleportType = (StartTeleportType)EditorGUILayout.EnumPopup(startTeleportType, GUILayout.Width(80));

        EditorGUILayout.EndHorizontal();


        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Scene:", GUILayout.Width(50));
        sceneTitle = (SceneTitle)PlayerPrefs.GetInt("StartScene");
        GUI.skin.button.fontStyle = FontStyle.Bold;
        GUI.contentColor = new Color(0.8f, 0.9f, 0.2f);
        if (GUILayout.Button(selectedSceneTitle.ToString(), GUILayout.Width(200)))
        {
            GenericMenu menu = new GenericMenu();
            SceneCategory[] sceneCategories = (SceneCategory[])System.Enum.GetValues(typeof(SceneCategory));

            foreach (SceneCategory category in sceneCategories)
            {
                SceneTitle[] sceneTitlesInCategory = GetSceneTitlesForCategory(category);

                foreach (SceneTitle sceneTitle in sceneTitlesInCategory)
                {
                    SceneTitle localSceneTitle = sceneTitle;
                    SceneCategory localCategory = category;
                    menu.AddItem(new GUIContent(localCategory + "/" + localSceneTitle), false, () => { SelectScene(localSceneTitle); });
                }
            }

            menu.ShowAsContext();
        }
        GUI.skin.button.fontStyle = FontStyle.Normal;
        GUI.contentColor = Color.white;
        PlayerPrefs.SetInt("StartScene", (int)selectedSceneTitle);

        EditorGUILayout.LabelField("Spawn Point:", GUILayout.Width(100));
        spawnPoint = PlayerPrefs.GetInt("StartSceneSpawnPoint");
        spawnPoint = EditorGUILayout.IntField(spawnPoint, GUILayout.Width(30));
        PlayerPrefs.SetInt("StartSceneSpawnPoint", spawnPoint);

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Language:", GUILayout.Width(70));
        language = (LanguageSupport)PlayerPrefs.GetInt("StartLanguage");
        language = (LanguageSupport)EditorGUILayout.EnumPopup(language, GUILayout.Width(150));
        PlayerPrefs.SetInt("StartLanguage", (int)language);

        EditorGUILayout.EndHorizontal();

        GUILayout.Space(10);
        GUI.contentColor = new Color(0.9f, 0.8f, 0.2f);
        if (GUILayout.Button("Start!"))
        {
            EndCameraPreview();
            if (Application.isPlaying)
            {
                // Scenecontroller.instance.LoadSceneAsset(sceneTitle);
            }
            else
            {
                EditorApplication.ExitPlaymode();
                lastActiveScene = "Assets/Scenes/" + EditorSceneManager.GetActiveScene().name + ".unity";
                PlayerPrefs.SetString("LastActiveScene", lastActiveScene);

                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
                EditorSceneManager.OpenScene("Assets/Scenes/" + GlobalLibrary.G_SCENE_PREPARE_ASSET_NAME + ".unity");
                EditorApplication.EnterPlaymode();
            }
        }
        GUI.contentColor = Color.white;
        if (Application.isPlaying)
        {
            if (GUILayout.Button("End!"))
            {
                EditorApplication.ExitPlaymode();
                waitForReset = true;
            }
        }
        else
        {
            EditorGUILayout.BeginHorizontal();
            /*
            if (startTeleportType == StartTeleportType.Developer)
                if (GUILayout.Button("Open scene in editor"))
                {
                    EndCameraPreview();
                    EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
                    EditorSceneManager.OpenScene("Assets/Scenes/" + GlobalLibrary.G_SCENE_ASSET_NAME[sceneTitle] + ".unity");
                }
                */
            if (GUILayout.Button("Open prepare scene"))
            {
                EndCameraPreview();
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
                EditorSceneManager.OpenScene("Assets/Scenes/" + GlobalLibrary.G_SCENE_PREPARE_ASSET_NAME + ".unity");
            }

            if (GUILayout.Button("Camera preview"))
            {
                EnterCameraPreview();
            }
            if (GUILayout.Button("Open scene in editor"))
            {
                OpenSceneInEditor();
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(20);
        EditorGUILayout.LabelField("Scene specifics:");
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Camera size: {GlobalLibrary.G_SCENE_SPECIFICS[sceneTitle].cameraSize}");
        EditorGUILayout.LabelField($"PP profile: {GlobalLibrary.G_SCENE_SPECIFICS[sceneTitle].postprocessingProfile}");
        EditorGUILayout.EndHorizontal();
        if (waitForReset)
        {
            if (!Application.isPlaying)
            {
                lastActiveScene = PlayerPrefs.GetString("LastActiveScene");
                EditorSceneManager.OpenScene(lastActiveScene);
                waitForReset = false;
            }
        }
    }
    private void OpenSceneInEditor()
    {
        EndCameraPreview();
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        EditorSceneManager.OpenScene("Assets/Scenes/" + GlobalLibrary.G_SCENE_ASSET_NAME[sceneTitle] + ".unity");
        if (SceneView.lastActiveSceneView != null)
        {
            //doesn't need anymore
            //Transform spawnPointCT = GameObject.FindGameObjectWithTag(GlobalLibrary.G_SCENE_TAG_SPAWNPOINT).transform;
            //int s = spawnPoint >= spawnPointCT.childCount ? 0 : spawnPoint;
            //SceneView.lastActiveSceneView.pivot = spawnPointCT.GetChild(s).position;
            SceneView.lastActiveSceneView.Repaint();
        }
    }

    private void EnterCameraPreview()
    {

    }

    void SelectScene(SceneTitle sceneTitle)
    {
        selectedSceneTitle = sceneTitle;
    }

    SceneTitle[] GetSceneTitlesForCategory(SceneCategory category)
    {
        return GlobalLibrary.G_SCENE_CATEGORY_DICT[category];
    }
    private void EndCameraPreview()
    {
        if (cam_Preview != null)
            DestroyImmediate(cam_Preview);
    }
}

