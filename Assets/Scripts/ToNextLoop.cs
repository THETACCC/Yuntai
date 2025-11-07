using System.Collections;
using UnityEngine;
using UnityEngine.Video;

public class ToNextLoop : MonoBehaviour
{
    [Header("Scenes")]
    public string scenename;
    public int SpawnPointLocation;

    [Header("Animation Settings")]
    [Tooltip("包含Animator的对象，例如角色或UI容器。")]
    public GameObject DeathAnimationPlayer;

    [Tooltip("要播放的Animator。")]
    public Animator deathAnimator;

    [Tooltip("要检测的动画状态名称。")]
    public string targetStateName = "Dead";

    [Tooltip("安全超时（秒），以防动画未正常结束。")]
    [Min(0f)] public float hardTimeoutSeconds = 10f;

    [Tooltip("是否允许按任意键跳过动画。")]
    public bool allowSkipWithAnyKey = false;

    [Header("Debug")]
    public bool verboseLog = false;

    private Coroutine _runCo;

    public void toNextLoop()
    {
        if (_runCo != null) return;
        _runCo = StartCoroutine(WaitForAnimationAndLoad());
    }

    private IEnumerator WaitForAnimationAndLoad()
    {
        if (verboseLog) Debug.Log("[ToNextLoop] Waiting for Dead animation...");

        if (DeathAnimationPlayer)
            DeathAnimationPlayer.SetActive(true);

        if (!deathAnimator)
        {
            if (verboseLog) Debug.LogWarning("[ToNextLoop] Missing Animator.");
            ProceedToNextLoop();
            yield break;
        }

        int layer = 0;
        float t = 0f;

        // Wait until the animator ENTERS the target state
        while (true)
        {
            AnimatorStateInfo info = deathAnimator.GetCurrentAnimatorStateInfo(layer);
            if (info.IsName(targetStateName))
            {
                if (verboseLog) Debug.Log("[ToNextLoop] Entered Dead state.");
                ProceedToNextLoop();
                break;
            }

            t += Time.unscaledDeltaTime;
            if (t > hardTimeoutSeconds)
            {
                if (verboseLog) Debug.LogWarning("[ToNextLoop] Timeout before entering Dead state. Proceeding.");
                ProceedToNextLoop();
                yield break;
            }

            yield return null;
        }

        // Now wait until the animation finishes
        t = 0f;
        while (true)
        {
            AnimatorStateInfo info = deathAnimator.GetCurrentAnimatorStateInfo(layer);

            // Animation finished when normalizedTime >= 1 and not looping
            if (info.IsName(targetStateName) && !info.loop && info.normalizedTime >= 1f)
            {
                if (verboseLog) Debug.Log("[ToNextLoop] Dead animation finished.");
                break;
            }

            // Allow skip
            if (allowSkipWithAnyKey && Input.anyKeyDown)
            {
                if (verboseLog) Debug.Log("[ToNextLoop] User skipped animation.");
                break;
            }

            t += Time.unscaledDeltaTime;
            if (t > hardTimeoutSeconds)
            {
                if (verboseLog) Debug.LogWarning("[ToNextLoop] Hard timeout reached. Proceeding.");
                break;
            }

            yield return null;
        }

        ProceedToNextLoop();
    }

    private void ProceedToNextLoop()
    {
        if (verboseLog) Debug.Log("[ToNextLoop] Proceeding to next scene...");
        LoopTracker.I?.IncrementLoop();
        SceneController.instance.LoadSceneAndTeleport(scenename, SpawnPointLocation);
        _runCo = null;
    }
}