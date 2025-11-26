using MoreMountains.Tools;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

public class CG_Manager : MonoBehaviour
{
    // Scenes
    [Header("Next Scene")]
    public string scenename;
    public int SpawnPointLocation;

    [Header("Video Settings")]
    [Tooltip("Assign your Video Player component here.")]
    public VideoPlayer videoPlayer;

    [Header("Skip Settings")]
    [Tooltip("Max delay (seconds) between two Space presses to count as double-tap.")]
    [SerializeField, Min(0f)] private float doubleTapMaxDelay = 0.4f;

    private float _lastSpaceTime = -999f;
    private bool _sceneLoaded = false;

    private void Start()
    {
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
        }

        if (SceneController.instance != null)
        {
            SceneController.instance.LoadSceneAndTeleport(scenename, SpawnPointLocation);
        }
        else
        {
            Debug.LogError("[CG_Manager] SceneController.instance is null, cannot load scene.");
        }
    }

    // Optional: Clean up event subscription
    private void OnDestroy()
    {
        if (videoPlayer != null)
            videoPlayer.loopPointReached -= OnVideoFinished;
    }
}
