using MoreMountains.Tools;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

public class CG_Manager : MonoBehaviour
{
    //scenes
    public string scenename;
    public int SpawnPointLocation;
    [Header("Video Settings")]
    [Tooltip("Assign your Video Player component here.")]
    public VideoPlayer videoPlayer;

    // Start is called before the first frame update
    void Start()
    {
        if (videoPlayer == null)
        {
            Debug.LogError("VideoPlayer not assigned in the Inspector.", this);
            return;
        }

        // Subscribe to the event that triggers when the video finishes
        videoPlayer.loopPointReached += OnVideoFinished;
    }

    // This function is called when the video finishes playing
    private void OnVideoFinished(VideoPlayer vp)
    {
        Debug.Log("Video has finished playing.");
        SceneController.instance.LoadSceneAndTeleport(scenename, SpawnPointLocation);
    }

    // Your custom function to handle scene loading and teleportation


    // Optional: Clean up event subscription
    private void OnDestroy()
    {
        if (videoPlayer != null)
            videoPlayer.loopPointReached -= OnVideoFinished;
    }
}
