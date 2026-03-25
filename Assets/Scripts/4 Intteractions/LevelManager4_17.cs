using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using static AudioManager;

public class LevelManager4_17 : BaseLevelManager
{
    [Header("Curtain")]
    [SerializeField] private GameObject curtain;
    [SerializeField] private string curtainAnimStateName = "CurtainOpen";
    [SerializeField] private float curtainAnimDuration = 1f;

    [Header("Next Scene")]
    [SerializeField] private string nextSceneName = "Level4_18MusicGame";
    [SerializeField] private int nextSpawnPointLocation = 0;
    [SerializeField] private bool useSceneControllerTeleport = true;

    private bool hasStartedTransition = false;

    public void playCurtainAnim()
    {
        if (hasStartedTransition) return;
        hasStartedTransition = true;

        if (Gamemanager.instance != null)
            Gamemanager.instance.phase = GamePhase.Eventing;

        if (curtain != null)
        {
            curtain.SetActive(true);

            Animator animator = curtain.GetComponent<Animator>();
            if (animator != null)
            {
                animator.enabled = true;
                animator.Play(curtainAnimStateName, 0, 0f);
            }
        }

        StartCoroutine(LoadAfterCurtain());
    }

    private IEnumerator LoadAfterCurtain()
    {
        yield return new WaitForSecondsRealtime(curtainAnimDuration);
        GotoNextLoop();
    }

    private void GotoNextLoop()
    {
        if (useSceneControllerTeleport && SceneController.instance != null)
        {
            if (!string.IsNullOrEmpty(nextSceneName))
                SceneController.instance.LoadSceneAndTeleport(nextSceneName, nextSpawnPointLocation);
            else
                Debug.LogWarning("[LevelManager4_17] nextSceneName 没填。");
        }
        else if (!string.IsNullOrEmpty(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
        }
        else
        {
            Debug.LogWarning("[LevelManager4_17] 没有可用的下一场景配置。");
        }
    }
}