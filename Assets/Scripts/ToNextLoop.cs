using System.Collections;
using UnityEngine;
using UnityEngine.Video;

public class ToNextLoop : MonoBehaviour
{
    [Header("Scenes")]
    public string scenename;
    public int SpawnPointLocation;

    [Header("Presentation")]
    [Tooltip("整个过场容器（含 VideoPlayer 或 UI）。可为空。")]
    public GameObject DeathAnimationPlayer;
    [Tooltip("显式指定 VideoPlayer；若为空会在 DeathAnimationPlayer 上查找。")]
    public VideoPlayer myVideoPlayer;

    [Header("Behavior")]
    [Tooltip("若没有可播的 clip（null），是否直接跳转。")]
    public bool proceedIfNoClip = true;
    [Tooltip("安全超时（秒）。到点仍未触发结束事件则继续流程。")]
    [Min(0f)] public float hardTimeoutSeconds = 12f;
    [Tooltip("强制从 0 开始播放，并关掉循环。")]
    public bool forceFromZero = true;
    [Tooltip("仅音频无画面时，也等待播放完毕。")]
    public bool audioOnlyAllowed = true;
    [Tooltip("开始播放后，是否允许按下任意键跳过。")]
    public bool allowSkipWithAnyKey = false;

    [Header("Debug")]
    public bool verboseLog = false;

    private Coroutine _runCo;
    private bool _endedByEvent;

    // 供按钮/Trigger 调用
    public void toNextLoop()
    {
        if (_runCo != null) return;
        _runCo = StartCoroutine(PlayDeathVideoThenLoad());
    }

    private IEnumerator PlayDeathVideoThenLoad()
    {
        // 1) 让呈现容器可见
        if (DeathAnimationPlayer) DeathAnimationPlayer.SetActive(true);

        // 2) 拿到 VideoPlayer
        VideoPlayer vp = myVideoPlayer;
        if (!vp && DeathAnimationPlayer)
            vp = DeathAnimationPlayer.GetComponentInChildren<VideoPlayer>(true);

        if (!vp)
        {
            if (verboseLog) Debug.Log("[ToNextLoop] 没找到 VideoPlayer 组件。");
            if (proceedIfNoClip) { ProceedToNextLoop(); yield break; }
            else { yield break; } // 明确要求没有就不走
        }

        // 3) clip 检查
        bool hasClip = vp.clip != null || !string.IsNullOrEmpty(vp.url);
        if (!hasClip)
        {
            if (verboseLog) Debug.Log("[ToNextLoop] 无 clip/url。");
            if (proceedIfNoClip) { ProceedToNextLoop(); yield break; }
            else { yield break; }
        }

        // 4) 播放前配置
        _endedByEvent = false;
        vp.loopPointReached -= OnLoopPointReached; // 防重
        vp.loopPointReached += OnLoopPointReached;

        // 有些情况下 frame 永远不变（音频/后台解码等），改为同时观测 time 与 isPlaying
        if (forceFromZero)
        {
            if (vp.canSetTime) vp.time = 0.0;
            vp.frame = 0; // 虽然有时无效，但不坏事
            vp.playbackSpeed = 1f;
            vp.isLooping = false;
        }

        // 5) Prepare（避免空读）
        if (!vp.isPrepared)
        {
            if (verboseLog) Debug.Log("[ToNextLoop] 调用 Prepare...");
            vp.Prepare();
            float prepareWait = 0f;
            while (!vp.isPrepared)
            {
                // 错误/空对象防御
                if (vp == null) { SafeCleanupAndProceed(); yield break; }
                prepareWait += Time.unscaledDeltaTime;
                if (prepareWait > 8f) // 准备过久兜底
                {
                    if (verboseLog) Debug.LogWarning("[ToNextLoop] Prepare 超时，直接尝试播放。");
                    break;
                }
                yield return null;
            }
        }

        // 6) 播放
        if (!vp.isPlaying) vp.Play();
        // 等到真正开始推进
        float startGuard = 0f;
        while (vp != null && !vp.isPlaying && startGuard < 1f)
        {
            startGuard += Time.unscaledDeltaTime;
            yield return null;
        }
        if (verboseLog) Debug.Log("[ToNextLoop] 已开始播放。");

        // 7) 等待：事件优先 + 条件兜底 + 硬超时 + 可选跳过
        float t = 0f;
        double lastTime = -1.0;
        while (true)
        {
            if (vp == null) { SafeCleanupAndProceed(); yield break; }

            // 事件已触发（最可靠）
            if (_endedByEvent) break;

            // 条件兜底：开始后不再 isPlaying 且时间推进过（避免未起播误判）
            bool started = vp.time > 0.001 || vp.frame > 0;
            if (started && !vp.isPlaying)
            {
                // 注意：纯音频也适用；若是被外部 Stop 打断，也会走到这里
                if (verboseLog) Debug.Log("[ToNextLoop] 通过条件兜底检测到播放结束。");
                break;
            }

            // 可选跳过
            if (allowSkipWithAnyKey && Input.anyKeyDown)
            {
                if (verboseLog) Debug.Log("[ToNextLoop] 用户按键跳过。");
                break;
            }

            // 硬超时（防止永远卡住）
            t += Time.unscaledDeltaTime;
            if (t > hardTimeoutSeconds)
            {
                if (verboseLog) Debug.LogWarning("[ToNextLoop] 到达硬超时，强制继续。");
                break;
            }

            // 如果既不推进时间也不播放，且没有画面但允许音频，仍然等待；否则给出警告
            if (started)
            {
                // 正常推进
                if (vp.time != lastTime) lastTime = vp.time;
            }

            yield return null;
        }

        // 8) 收尾并跳转
        if (vp) { vp.loopPointReached -= OnLoopPointReached; }
        ProceedToNextLoop();
    }

    private void OnLoopPointReached(VideoPlayer source)
    {
        if (verboseLog) Debug.Log("[ToNextLoop] loopPointReached 事件。");
        _endedByEvent = true;
    }

    private void SafeCleanupAndProceed()
    {
        if (verboseLog) Debug.LogWarning("[ToNextLoop] VideoPlayer 被销毁/丢失，继续流程。");
        ProceedToNextLoop();
    }

    private void ProceedToNextLoop()
    {
        // 放在这里，保证在“确实要切场景”时才 +1
        LoopTracker.I?.IncrementLoop();
        SceneController.instance.LoadSceneAndTeleport(scenename, SpawnPointLocation);
        _runCo = null;
    }
}
