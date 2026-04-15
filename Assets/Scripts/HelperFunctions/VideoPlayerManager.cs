using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

public class VideoPlayerManager : MonoBehaviour
{
    [SerializeField] private ToNextLoop nextLoop;
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private GameObject VideoCanvas;
    void Start()
    {
        if (videoPlayer != null)
        {
            // Subscribe to the event
            videoPlayer.loopPointReached += OnVideoFinished;
        }
    }
    // Call this function to play the video
    public void PlayVideo()
    {
        if (videoPlayer != null)
        {
            VideoCanvas.SetActive(true);
            videoPlayer.Stop();          // Reset state
            videoPlayer.time = 0;        // Start from beginning
            videoPlayer.Play();          // Play video
        }
    }
    private void OnVideoFinished(VideoPlayer vp)
    {
        if (nextLoop != null)
        {
            nextLoop.toNextLoop();
        }
    }

    private void OnDestroy()
    {
        // Always unsubscribe to avoid memory issues
        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached -= OnVideoFinished;
        }
    }
}
