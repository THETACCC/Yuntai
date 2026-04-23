using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static AudioManager;

[System.Serializable]
public class CanvasImageMoveFadeData
{
    public RectTransform imageRect;
    public Vector2 targetAnchoredPosition;
    [Range(0f, 1f)] public float targetAlpha = 1f;
}

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

    [Header("4 Canvas Images Move + Fade")]
    [SerializeField] private float imageMoveFadeDuration = 1f;
    [SerializeField] private CanvasImageMoveFadeData leftImage1;
    [SerializeField] private CanvasImageMoveFadeData leftImage2;
    [SerializeField] private CanvasImageMoveFadeData rightImage1;
    [SerializeField] private CanvasImageMoveFadeData rightImage2;

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

    public void PlayFourImagesMoveFade()
    {
        StartCoroutine(PlayFourImagesMoveFadeCoroutine());
    }

    private IEnumerator PlayFourImagesMoveFadeCoroutine()
    {
        List<ImageAnimState> states = new List<ImageAnimState>();

        AddImageState(leftImage1, states);
        AddImageState(leftImage2, states);
        AddImageState(rightImage1, states);
        AddImageState(rightImage2, states);

        float timer = 0f;

        while (timer < imageMoveFadeDuration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / imageMoveFadeDuration);

            for (int i = 0; i < states.Count; i++)
            {
                if (states[i].rect == null || states[i].image == null) continue;

                states[i].rect.anchoredPosition = Vector2.Lerp(states[i].startPos, states[i].targetPos, t);

                Color c = states[i].baseColor;
                c.a = Mathf.Lerp(0f, states[i].targetAlpha, t);
                states[i].image.color = c;
            }

            yield return null;
        }

        for (int i = 0; i < states.Count; i++)
        {
            if (states[i].rect == null || states[i].image == null) continue;

            states[i].rect.anchoredPosition = states[i].targetPos;

            Color c = states[i].baseColor;
            c.a = states[i].targetAlpha;
            states[i].image.color = c;
        }
    }

    private void AddImageState(CanvasImageMoveFadeData data, List<ImageAnimState> states)
    {
        if (data == null || data.imageRect == null) return;

        Image img = data.imageRect.GetComponent<Image>();
        if (img == null) return;

        Color baseColor = img.color;
        baseColor.a = 1f; // RGB 保留，alpha 单独控制

        ImageAnimState state = new ImageAnimState
        {
            rect = data.imageRect,
            image = img,
            startPos = data.imageRect.anchoredPosition,
            targetPos = data.targetAnchoredPosition,
            targetAlpha = data.targetAlpha,
            baseColor = baseColor
        };

        Color startColor = baseColor;
        startColor.a = 0f;
        img.color = startColor;

        states.Add(state);
    }

    private class ImageAnimState
    {
        public RectTransform rect;
        public Image image;
        public Vector2 startPos;
        public Vector2 targetPos;
        public float targetAlpha;
        public Color baseColor;
    }
}