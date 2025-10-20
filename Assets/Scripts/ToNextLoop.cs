using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

public class ToNextLoop : MonoBehaviour
{
    // Scenes
    public string scenename;
    public int SpawnPointLocation;

    [Header("Presentation")]
    public GameObject DeathAnimationPlayer;  // Container that holds your VideoPlayer/UI
    public VideoPlayer myVideoPlayer;        // Assign in Inspector (optional if found on DeathAnimationPlayer)

    // Public entry point (e.g., call from button, trigger, etc.)
    public void toNextLoop()
    {
        StartCoroutine(PlayDeathVideoThenLoad());
    }

    private IEnumerator PlayDeathVideoThenLoad()
    {
        // Ensure presentation object is active (so VideoPlayer can render)
        if (DeathAnimationPlayer != null)
            DeathAnimationPlayer.SetActive(true);

        // Resolve VideoPlayer (prefer explicit field; otherwise look on the DeathAnimationPlayer)
        VideoPlayer vp = myVideoPlayer;
        if (vp == null && DeathAnimationPlayer != null)
            vp = DeathAnimationPlayer.GetComponentInChildren<VideoPlayer>(true);

        // If no VideoPlayer available, proceed immediately
        if (vp == null)
        {
            ProceedToNextLoop();
            yield break;
        }

        // Prepare the video if needed (handles cases where the clip isn¡¯t preloaded)
        if (!vp.isPrepared)
        {
            vp.Prepare();
            // Wait until prepared (guards against null clips or long I/O)
            yield return new WaitUntil(() => vp == null || vp.isPrepared);
            if (vp == null) // Safety check in case object was destroyed
            {
                ProceedToNextLoop();
                yield break;
            }
        }

        // Start playback if not already playing
        if (!vp.isPlaying)
            vp.Play();

        // Wait until the video actually starts (some platforms need a frame to advance)
        yield return null;

        // Wait until the video finishes:
        // Condition: has started (frame > 0) AND not playing anymore
        // This avoids false positives if the player hasn't begun playback yet.
        yield return new WaitUntil(() =>
            vp == null || ((vp.frame > 0) && !vp.isPlaying));

        // Now proceed
        ProceedToNextLoop();
    }

    private void ProceedToNextLoop()
    {
        // Loop +1 (moved here so it happens exactly when we transition)
        LoopTracker.I?.IncrementLoop();

        // Load target scene and teleport
        SceneController.instance.LoadSceneAndTeleport(scenename, SpawnPointLocation);
    }
}
