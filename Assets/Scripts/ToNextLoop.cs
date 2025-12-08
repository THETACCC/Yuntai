using System.Collections;
using UnityEngine;
using UnityEngine.Video;
using Fungus;    // 为了 StopAllBlocks / SayDialog

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

    [Header("Sound")]
    public AudioSource DeathAnimationSound;
    public AudioSource DeathAnimationSoundOneShot;
    [Header("Debug")]
    public bool verboseLog = false;

    private Coroutine _runCo;

    public void toNextLoop()
    {
        if(DeathAnimationSound) DeathAnimationSound.Play();
        //DeathAnimationSoundOneShot.Play();
        if (_runCo != null) return;
        _runCo = StartCoroutine(WaitForAnimationAndLoad());
    }

    private IEnumerator WaitForAnimationAndLoad()
    {
        if (verboseLog) Debug.Log("[ToNextLoop] Waiting for Dead animation...");

        // 1) 把 Fungus 对话停掉 + 锁住玩家
        KillDialogAndFreezePlayer();

        // 2) 打开死亡动画容器
        if (DeathAnimationPlayer)
            DeathAnimationPlayer.SetActive(true);

        if (!deathAnimator)
        {
            if (verboseLog) Debug.LogWarning("[ToNextLoop] Missing Animator, skip to next loop.");
            ProceedToNextLoop();
            yield break;
        }

        int layer = 0;
        float t = 0f;

        // === 等 Animator 进入 targetStateName ===
        while (true)
        {
            AnimatorStateInfo info = deathAnimator.GetCurrentAnimatorStateInfo(layer);
            if (info.IsName(targetStateName))
            {
                if (verboseLog) Debug.Log("[ToNextLoop] Entered Dead state.");
                break;
            }

            t += Time.unscaledDeltaTime;
            if (t > hardTimeoutSeconds)
            {
                if (verboseLog) Debug.LogWarning("[ToNextLoop] Timeout BEFORE entering Dead state. Proceeding.");
                ProceedToNextLoop();
                yield break;
            }

            yield return null;
        }

        // === 等 Dead 动画真正播完 ===
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
                if (verboseLog) Debug.LogWarning("[ToNextLoop] Hard timeout AFTER entering Dead. Proceeding.");
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
        var pc = PlayerController.Instance;
        if (pc == null)
        {
            // 兜底：如果单例没找到，用 tag 试一次
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                pc = player.GetComponent<PlayerController>();
        }

        if (pc != null)
        {
            pc.DisablePlayerControl();  // 你之前写的接口
            if (verboseLog) Debug.Log("[ToNextLoop] Player control disabled before death animation.");
        }
        else
        {
            if (verboseLog) Debug.LogWarning("[ToNextLoop] KillDialogAndFreezePlayer: PlayerController not found.");
        }

        if (Gamemanager.instance)
            Gamemanager.instance.phase = GamePhase.Eventing;
    }

    /// <summary>
    /// 真正切到下一回圈前，把玩家设成“下一关准备好的状态”。
    /// 注意：这里只管控制状态，坐标是 SceneController.LoadSceneAndTeleport 负责。
    /// </summary>
    private void RestorePlayerForNextScene()
    {
        var pc = PlayerController.Instance;
        if (pc == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                pc = player.GetComponent<PlayerController>();
        }

        if (pc != null)
        {
            pc.EnablePlayerControl();
            if (verboseLog) Debug.Log("[ToNextLoop] Player control enabled for next scene.");
        }

        if (Gamemanager.instance)
            Gamemanager.instance.phase = GamePhase.Moving;
    }

    private void ProceedToNextLoop()
    {
        if (verboseLog)
        {
            var pc = PlayerController.Instance;
            if (pc != null)
            {
                var pos = pc.transform.position;
                Debug.Log($"[ToNextLoop] Proceeding to next scene {scenename}, spawnIndex={SpawnPointLocation}, " +
                          $"current player pos={pos}");
            }
            else
            {
                Debug.Log($"[ToNextLoop] Proceeding to next scene {scenename}, spawnIndex={SpawnPointLocation}, " +
                          $"PlayerController.Instance is null.");
            }
        }

        // 先把玩家恢复到正常“可走”状态（下一个场景的 LevelManager 也可以再改）
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
