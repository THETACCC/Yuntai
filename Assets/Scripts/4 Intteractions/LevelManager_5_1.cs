using UnityEngine;

public class LevelManager_5_1 : BaseLevelManager
{
    [Header("Next Scene")]
    [SerializeField] private string nextSceneName = "FinalCG";
    [SerializeField] private int spawnPointLocation = 0;

    private bool hasLoadedScene = false;

    public void GoToFinalCG()
    {
        if (hasLoadedScene) return;
        hasLoadedScene = true;

        if (SceneController.instance == null)
        {
            Debug.LogError("[LevelManager_5_1] SceneController.instance is null, cannot load scene.");
            hasLoadedScene = false;
            return;
        }

        SceneController.instance.LoadSceneAndTeleport(nextSceneName, spawnPointLocation);
    }
}