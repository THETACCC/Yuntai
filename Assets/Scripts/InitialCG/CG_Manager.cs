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

    [Header("Initial CG Content Warning")]
    [Tooltip("Only turn this on for the first / initial CG.")]
    [SerializeField] private bool isInitialCG = false;

    [Tooltip("The UI object that shows the content warning.")]
    [SerializeField] private GameObject contentWarningRoot;

    private bool _waitingForContentWarningInput = false;

    [Header("Skip Settings")]
    [Tooltip("Max delay (seconds) between two Space presses to count as double-tap.")]
    [SerializeField, Min(0f)] private float doubleTapMaxDelay = 0.4f;

    private float _lastSpaceTime = -999f;
    private bool _sceneLoaded = false;

    //Get Notebook stuff



    private void Start()
    {
        NoteBookManager.instance.disableNoteBook();

        if (videoPlayer == null)
        {
            Debug.LogError("[CG_Manager] VideoPlayer not assigned in the Inspector.", this);
            return;
        }

        // Important: stop Play On Awake / first frame showing before warning
        videoPlayer.playOnAwake = false;
        videoPlayer.Stop();

        // Apply once at start, but also apply again right before Play().
        ApplyLanguageClip();

        // Subscribe to the event that triggers when the video finishes
        videoPlayer.loopPointReached += OnVideoFinished;

        // Only the first / initial CG needs to wait on the content warning.
        if (isInitialCG)
        {
            _waitingForContentWarningInput = true;

            if (contentWarningRoot != null)
            {
                contentWarningRoot.SetActive(true);
            }

            // Hide the video object completely so the first frame will not show behind the warning.
            videoPlayer.gameObject.SetActive(false);
        }
        else
        {
            if (contentWarningRoot != null)
            {
                contentWarningRoot.SetActive(false);
            }

            videoPlayer.gameObject.SetActive(true);

            // Re-apply right before playing, in case language was changed before this CG starts.
            ApplyLanguageClip();
            videoPlayer.Play();
        }
    }

    private void Update()
    {
        if (_sceneLoaded) return;                    // 已经跳过/结束就不再检测
        if (videoPlayer == null) return;

        // If this is the initial CG, only Space starts the video.
        // This prevents Esc/settings/language UI clicks from accidentally starting the video.
        if (_waitingForContentWarningInput)
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                _waitingForContentWarningInput = false;

                if (contentWarningRoot != null)
                {
                    contentWarningRoot.SetActive(false);
                }

                videoPlayer.gameObject.SetActive(true);

                // Important: apply current language again right before video starts.
                // This fixes changing language from Esc/settings while on the content warning page.
                ApplyLanguageClip();
                videoPlayer.Play();
            }

            return;
        }

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

    private void ApplyLanguageClip()
    {
        if (videoPlayer == null) return;
        if (Settings.instance == null) return;

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
            default:
                videoPlayer.clip = clip_en;
                break;
        }

        Debug.Log("[CG_Manager] Applied video language clip: " + Settings.instance.currentLanguage);
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