using Fungus;
using MoreMountains.Tools;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using static Unity.Burst.Intrinsics.X86.Avx;
using static AudioManager;

public class CG_Manager : MonoBehaviour
{
    // Scenes
    [Header("Next Scene")]
    public string scenename;
    public int SpawnPointLocation;
    public Image chapterSwitch;

    [Header("Video Settings")]
    [Tooltip("Assign your Video Player component here.")]
    public VideoPlayer videoPlayer;
    public VideoClip clip_zh;
    public VideoClip clip_en;
    public VideoClip clip_ja;

    [Header("Skip Settings")]
    [Tooltip("Max delay (seconds) between two Space presses to count as double-tap.")]
    [SerializeField, Min(0f)] private float doubleTapMaxDelay = 0.4f;

    private float _lastSpaceTime = -999f;
    private bool _sceneLoaded = false;

    private void Start()
    {
        if (Settings.instance != null)
        {
            switch (Settings.instance.currentLanguage) 
            {
                case "zh":
                    videoPlayer.clip = clip_zh;
                    break;
                case "en":
                    videoPlayer.clip = clip_en;
                    break;
                case "ja":
                    videoPlayer.clip = clip_ja;
                    break;
            }
        }
        if (videoPlayer == null)
        {
            Debug.LogError("[CG_Manager] VideoPlayer not assigned in the Inspector.", this);
            return;
        }

        // Subscribe to the event that triggers when the video finishes
        videoPlayer.loopPointReached += OnVideoFinished;
    }

    private void Update()
    {
        if (_sceneLoaded) return;                    // 已经跳过/结束就不再检测
        if (videoPlayer == null) return;

        if (Input.GetKeyDown(KeyCode.Space))
        {
            float now = Time.unscaledTime;

            // 在 doubleTapMaxDelay 时间内连按两次 Space → 视为 double space
            if (now - _lastSpaceTime <= doubleTapMaxDelay)
            {
                Debug.Log("[CG_Manager] Double Space detected, skipping CG and loading next scene.");
                SkipToNextScene();
                _sceneLoaded = true;
                _lastSpaceTime = -999f;
            }
            else
            {
                _lastSpaceTime = now;
            }
        }
    }

    // Called when the video finishes playing normally
    private void OnVideoFinished(VideoPlayer vp)
    {
        if (_sceneLoaded) return;    // 避免 double space 已经触发过
        Debug.Log("[CG_Manager] Video has finished playing.");

        SkipToNextScene();
    }

    /// <summary>
    /// 统一处理切场景逻辑（视频播完 or double space 都走这里）
    /// </summary>
    private void SkipToNextScene()
    {
        _sceneLoaded = true;

        // 可选：先停掉视频
        if (videoPlayer != null && videoPlayer.isPlaying)
        {
            videoPlayer.Stop();
            videoPlayer.gameObject.SetActive(false);
        }

        if (SceneController.instance == null)
        {
            Debug.LogError("[CG_Manager] SceneController.instance is null, cannot load scene.");
        }

        //如果有章节切换，先显示章节切换然后再换场景
        if (chapterSwitch == null)
        {
            SceneController.instance.LoadSceneAndTeleport(scenename, SpawnPointLocation);
        }
        else
        {   
            AudioManager.PlayOneShot("Sound Effects/sndOpeningGong", AudioGroup.SFX);
            StartCoroutine(Tweening.StartTweening(
            TweeningCurve.Linear,
            3f,
            t => chapterSwitch.color = new Color(chapterSwitch.color.r, chapterSwitch.color.g, chapterSwitch.color.b, t),
            () =>
            {
                chapterSwitch.color = new Color(chapterSwitch.color.r, chapterSwitch.color.g, chapterSwitch.color.b, 1);
                StartCoroutine(Tweening.StartTweening(
                    TweeningCurve.Linear,
                    2f,
                    t => chapterSwitch.color = new Color(chapterSwitch.color.r, chapterSwitch.color.g, chapterSwitch.color.b, 1 - t),
                    () =>
                    {
                        chapterSwitch.color = new Color(chapterSwitch.color.r, chapterSwitch.color.g, chapterSwitch.color.b, 0);
                        SceneController.instance.LoadSceneAndTeleport(scenename, SpawnPointLocation);
                    }
                ));
            }
        ));
        }
    }



    // Optional: Clean up event subscription
    private void OnDestroy()
    {
        if (videoPlayer != null)
            videoPlayer.loopPointReached -= OnVideoFinished;
    }
}
