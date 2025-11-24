using System.Collections;
using UnityEngine;
using UnityEngine.Video;
using Fungus;    // ★ 为了 StopAllBlocks / SayDialog

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

        // ★ 1) 先关掉所有 Fungus 对话，避免 2-2 的对话跑到 3-1
        KillDialogAndFreezePlayer();

        // ★ 2) 打开承载死亡动画的对象
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

        // === 等 Animator 进入 targetStateName（但不要立刻跳关！）===
        while (true)
        {
            AnimatorStateInfo info = deathAnimator.GetCurrentAnimatorStateInfo(layer);
            if (info.IsName(targetStateName))
            {
                if (verboseLog) Debug.Log("[ToNextLoop] Entered Dead state.");
                // 这里只 break，不再调用 ProceedToNextLoop()
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

        // === 等动画真正播完 ===
        t = 0f;
        while (true)
        {
            AnimatorStateInfo info = deathAnimator.GetCurrentAnimatorStateInfo(layer);

            // 动画播完：在 Dead 状态，且不循环，normalizedTime >= 1
            if (info.IsName(targetStateName) && !info.loop && info.normalizedTime >= 1f)
            {
                if (verboseLog) Debug.Log("[ToNextLoop] Dead animation finished.");
                break;
            }

            // 允许跳过
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

    /// <summary>
    /// 把之前场景的 Fungus 对话关掉，并锁住玩家（防止在死亡动画里乱跑）。
    /// </summary>
    private void KillDialogAndFreezePlayer()
    {
        // 1) 关掉当前的 SayDialog + 停止所有 Flowchart Block
        try
        {
            var activeSay = SayDialog.ActiveSayDialog;
            if (activeSay != null)
                activeSay.gameObject.SetActive(false);

            Flowchart[] charts = FindObjectsOfType<Flowchart>();
            foreach (var fc in charts)
                fc.StopAllBlocks();
        }
        catch (System.Exception e)
        {
            if (verboseLog) Debug.LogWarning("[ToNextLoop] KillDialog error: " + e.Message);
        }

        // 2) 锁玩家，让她在死亡动画期间不能走
        var player = GameObject.FindGameObjectWithTag("Player");
        if (!player) return;

        var pc = player.GetComponent<PlayerController>();
        if (pc != null)
            pc.DisablePlayerControl();  // 你之前写好的接口，会关掉动画和脚步声

        if (Gamemanager.instance)
            Gamemanager.instance.phase = GamePhase.Eventing;
    }

    /// <summary>
    /// 真正切到下一回圈前，可以顺便让玩家恢复正常（下一关能走路）。
    /// </summary>
    private void RestorePlayerForNextScene()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (!player) return;

        var pc = player.GetComponent<PlayerController>();
        if (pc != null)
            pc.EnablePlayerControl();   // 让 3-1 一进来就能动；若新关卡自己改 phase，会覆盖这里
        if (Gamemanager.instance)
            Gamemanager.instance.phase = GamePhase.Moving;
    }

    private void ProceedToNextLoop()
    {
        if (verboseLog) Debug.Log("[ToNextLoop] Proceeding to next scene...");

        // ★ 先把玩家恢复到“正常可以走”的状态，交给下一关管理
        RestorePlayerForNextScene();

        LoopTracker.I?.IncrementLoop();

        if (SceneController.instance != null)
        {
            SceneController.instance.LoadSceneAndTeleport(scenename, SpawnPointLocation);
        }
        else
        {
            Debug.LogError("[ToNextLoop] SceneController.instance 为 null，无法切场景。");
        }

        _runCo = null;
    }
}
