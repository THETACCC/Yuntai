using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using URPLight2D = UnityEngine.Rendering.Universal.Light2D;
using DialogueSystem;

public class LevelManager3_5 : BaseLevelManager
{
    // ========== ① CG 播放 ==========

    [Header("CGDialogue Settings")]
    [SerializeField] private CGDialogue cgDialogue;

    [Header("周叔对话")]
    [SerializeField] private DialogueTrigger zhoushu1Trigger;
    [SerializeField] private DialogueTrigger zhoushu2Trigger;

    // ========== ② 黑场（Global Light）+ 音效 ==========

    [Header("黑场（Global Light）")]
    [SerializeField] private URPLight2D globalLight;
    [SerializeField, Min(0f)] private float fadeOutDuration = 0.5f;
    [SerializeField, Min(0f)] private float blackHoldDuration = 1.5f;
    [SerializeField, Min(0f)] private float fadeInDuration = 0.8f;
    [SerializeField] private AudioSource blackoutSfx;

    private Coroutine _blackoutRoutine;

    // ========== ③ 周叔外观切换：Normal → Blood ==========

    [Header("ZhouShu 外观切换")]
    [Tooltip("普通状态的周叔（黑屏前显示的那个）")]
    [SerializeField] private GameObject zhoushuNormalObject;
    [Tooltip("血周叔（黑屏期间切换到这个）")]
    [SerializeField] private GameObject zhoushuBloodObject;

    // ========== ④ 周叔 + 玩家一起“发光消失”并传送 ==========

    [Header("Next Loop 跳转")]
    [Tooltip("下一回圈的场景名（若用 Scene Controller 也请填）")]
    [SerializeField] private string nextSceneName;
    [Tooltip("若使用 SceneController，则指定出生点编号")]
    [SerializeField] private int nextSpawnPointLocation = 0;
    [Tooltip("使用 SceneController.instance.LoadSceneAndTeleport() 跳转")]
    [SerializeField] private bool useSceneControllerTeleport = true;

    [Header("离场 Spot Light 演出")]
    [SerializeField] private URPLight2D exitSpot;
    [SerializeField, Min(0f)] private float spotRiseDuration = 3f;
    [SerializeField] private float spotTargetIntensity = 1000f;
    [SerializeField] private float spotTargetOuterRadius = 7.5f;
    [SerializeField] private float spotTargetInnerRadius = 3f;
    [SerializeField, Min(0f)] private float spotQuickFadeDuration = 0.25f;

    [Header("离场时要消失的周叔（runtime 会在黑屏时改成血周叔）")]
    [SerializeField] private GameObject zhoushuObject;

    [Header("Debug")]
    [SerializeField] private bool verboseLog = false;

    private Coroutine _exitRoutine;

    // ========== 生命周期 ==========

    /// <summary>
    /// 3-5 这里我们希望一进场玩家就“在场且可动”。
    /// </summary>
    protected override void Awake()
    {
        hidePlayerOnSceneStart = false;
        lockPlayerOnSceneStart = false;
        base.Awake();
    }

    private void Start()
    {
        // 保险：再次确认玩家可见 + 可移动
        ShowPlayerAndAllowMove();

        if (cgDialogue != null)
        {
            cgDialogue.OnCGFinished -= HandleCGFinished;
            cgDialogue.OnCGFinished += HandleCGFinished;
        }
        else
        {
            Debug.LogWarning("[LevelManager3_5] cgDialogue 未设置。");
        }

        // 如果没手动指定 zhoushuObject，就默认用 normal 版本
        if (!zhoushuObject && zhoushuNormalObject)
            zhoushuObject = zhoushuNormalObject;
    }

    // ========== ① CG 相关 ==========

    /// <summary>
    /// 给 Fungus / Trigger 调用：
    /// “开始 CG”——会先锁住玩家，然后让 CGDialogue 播放。
    /// </summary>
    public void StartGC()
    {
        if (cgDialogue == null)
        {
            Debug.LogWarning("[LevelManager3_5] StartGC: cgDialogue 未设置。");
            return;
        }

        // 锁住玩家移动
        DisablePlayerMovement();
        if (Gamemanager.instance)
            Gamemanager.instance.phase = GamePhase.Eventing;

        cgDialogue.StartCG();
    }

    /// <summary>
    /// CG 完成后由 CGDialogue 回调：先播放 ZhouShu1 对话。
    /// </summary>
    private void HandleCGFinished()
    {
        if (zhoushu1Trigger != null)
        {
            zhoushu1Trigger.TriggerDialogue();
        }
        else
        {
            Debug.LogWarning("[LevelManager3_5] zhoushu1Trigger 未设置。");
        }

        // 这里仍然保持玩家被锁住，
        // 直到黑屏 + ZhouShu2 完成后或者你直接调用 ZhouShuLeave_3_5。
    }

    // ========== ② Global Light 黑屏 + ZhouShu2（期间切换 Blood 版） ==========

    /// <summary>
    /// 请在 ZhouShu1 对话的最后一行（Fungus 或你的对话系统里）调用：
    /// Global Light 黑屏（带音效，黑屏期间把周叔换成 Blood 版）→ 灯光恢复 → 解锁玩家 → 触发 ZhouShu2。
    /// </summary>
    public void StartBlackoutThenZhouShu2()
    {
        if (_blackoutRoutine != null)
            StopCoroutine(_blackoutRoutine);

        _blackoutRoutine = StartCoroutine(CoBlackoutThenZhouShu2());
    }

    private IEnumerator CoBlackoutThenZhouShu2()
    {
        if (!globalLight)
        {
            Debug.LogWarning("[LevelManager3_5] globalLight 未设置，无法执行黑场。");
            yield break;
        }

        float originalIntensity = globalLight.intensity;

        // 播放黑场音效
        if (blackoutSfx)
            blackoutSfx.Play();

        // --- 1) 灯光渐暗到全黑（缓出） ---
        if (fadeOutDuration <= 0f)
        {
            globalLight.intensity = 0f;
        }
        else
        {
            float t = 0f;
            while (t < fadeOutDuration)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / fadeOutDuration);
                globalLight.intensity = Mathf.Lerp(originalIntensity, 0f, u);
                yield return null;
            }
            globalLight.intensity = 0f;
        }

        // ⭐ 此时已经是全黑，趁黑屏把周叔从 normal 换成 blood
        SwitchZhoushuToBlood();

        // --- 2) 全黑维持 blackHoldDuration 秒 ---
        if (blackHoldDuration > 0f)
            yield return new WaitForSeconds(blackHoldDuration);

        // --- 3) 灯光从黑渐亮回原本强度（缓入） ---
        if (fadeInDuration <= 0f)
        {
            globalLight.intensity = originalIntensity;
        }
        else
        {
            float t = 0f;
            while (t < fadeInDuration)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / fadeInDuration);
                globalLight.intensity = Mathf.Lerp(0f, originalIntensity, u);
                yield return null;
            }
            globalLight.intensity = originalIntensity;
        }

        // 黑屏完成：按你之前的规则，这里解锁玩家
        EnablePlayerMovement();
        if (Gamemanager.instance)
            Gamemanager.instance.phase = GamePhase.Moving;

        // 然后触发 ZhouShu2 对话（现在画面上是血周叔）
        if (zhoushu2Trigger != null)
        {
            zhoushu2Trigger.TriggerDialogue();
        }
        else
        {
            Debug.LogWarning("[LevelManager3_5] zhoushu2Trigger 未设置。");
        }

        _blackoutRoutine = null;
    }

    /// <summary>
    /// 正常周叔 → 关掉；血周叔 → 打开；并把 zhoushuObject 指向血周叔（后面离场发光消失用）。
    /// </summary>
    private void SwitchZhoushuToBlood()
    {
        if (zhoushuNormalObject && zhoushuNormalObject.activeSelf)
            zhoushuNormalObject.SetActive(false);

        if (zhoushuBloodObject)
        {
            zhoushuBloodObject.SetActive(true);
            // 之后 ZhouShuLeave_3_5 时消失的就是血周叔
            zhoushuObject = zhoushuBloodObject;

            if (verboseLog)
                Debug.Log("[LevelManager3_5] Switched ZhouShu: Normal → Blood.");
        }
        else
        {
            if (verboseLog)
                Debug.LogWarning("[LevelManager3_5] zhoushuBloodObject 未设置，无法切换到血周叔。");
        }
    }

    // ========== ③ 周叔 + 玩家 SpotLight 发光消失 + 传送（目标是血周叔） ==========

    /// <summary>
    /// 和 3-1 / 3-4 一样：
    /// Spot Light 亮起 → 周叔和玩家“消失” → 切到下一回圈 Scene。
    /// 这里会使用当前的 zhoushuObject（黑屏后已被改成血周叔）。
    /// </summary>
    public void ZhouShuLeave_3_5()
    {
        if (_exitRoutine != null)
            StopCoroutine(_exitRoutine);

        _exitRoutine = StartCoroutine(CoZhouShuLeave_3_5());
    }

    private IEnumerator CoZhouShuLeave_3_5()
    {
        if (verboseLog) Debug.Log("[3-5] ZhouShuLeave_3_5 start");

        // 0) 锁住玩家：不能再乱走
        DisablePlayerMovement();              // Base 的接口：锁 GamePhase + PlayerController
        if (Gamemanager.instance)
            Gamemanager.instance.phase = GamePhase.Eventing;

        if (playerRb != null)
        {
            playerRb.velocity = Vector2.zero;
            playerRb.isKinematic = false;     // 保持物理，但速度为 0
        }

        // 1) 初始化离场 Spot Light（从 0 开始）
        InitExitSpot();

        // 2) Spot Light 由暗到亮（强度、半径同时拉大）
        if (exitSpot && spotRiseDuration > 0f)
        {
            float t = 0f;
            while (t < spotRiseDuration)
            {
                t += Time.deltaTime;
                float s = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / spotRiseDuration));
                exitSpot.intensity = Mathf.Lerp(0f, spotTargetIntensity, s);
                exitSpot.pointLightOuterRadius = Mathf.Lerp(0f, spotTargetOuterRadius, s);
                exitSpot.pointLightInnerRadius = Mathf.Lerp(0f, spotTargetInnerRadius, s);
                yield return null;
            }
            exitSpot.intensity = spotTargetIntensity;
            exitSpot.pointLightOuterRadius = spotTargetOuterRadius;
            exitSpot.pointLightInnerRadius = spotTargetInnerRadius;
        }

        // 3) 周叔 & 玩家“被传送走” —— 直接隐藏
        if (zhoushuObject && zhoushuObject.activeSelf)
            zhoushuObject.SetActive(false);      // 这里是血周叔

        HidePlayerSprites();      // BaseLevelManager 提供
        ForceHidePlayerSprites(); // 把 alpha 也置 0，保险

        // 4) Spot Light 快速淡出
        yield return FadeOutExitSpot();

        // 5) 跳到下一回圈
        GotoNextLoop();

        if (verboseLog) Debug.Log("[3-5] ZhouShuLeave_3_5 end");
        _exitRoutine = null;
    }

    // ========== 工具：初始化 / 淡出 Spot Light ==========

    private void InitExitSpot()
    {
        if (!exitSpot) return;

        if (!exitSpot.gameObject.activeInHierarchy) exitSpot.gameObject.SetActive(true);
        if (!exitSpot.enabled) exitSpot.enabled = true;
        var lt = UnityEngine.Rendering.Universal.Light2D.LightType.Point;
        if (exitSpot.lightType != lt) exitSpot.lightType = lt;

        exitSpot.intensity = 0f;
        exitSpot.pointLightOuterRadius = 0f;
        exitSpot.pointLightInnerRadius = 0f;

        if (spotTargetInnerRadius > spotTargetOuterRadius)
            spotTargetInnerRadius = Mathf.Max(0f, spotTargetOuterRadius - 0.01f);
    }

    private IEnumerator FadeOutExitSpot()
    {
        if (!exitSpot) yield break;
        if (!exitSpot.enabled ||
            (exitSpot.intensity <= 0.001f && exitSpot.pointLightOuterRadius <= 0.001f))
            yield break;

        float i0 = exitSpot.intensity;
        float o0 = exitSpot.pointLightOuterRadius;
        float in0 = exitSpot.pointLightInnerRadius;

        float tf = 0f;
        float dur = Mathf.Max(0.01f, spotQuickFadeDuration);
        while (tf < dur)
        {
            tf += Time.deltaTime;
            float u = Mathf.Clamp01(tf / dur);
            float k = 1f - u;
            exitSpot.intensity = i0 * k;
            exitSpot.pointLightOuterRadius = o0 * k;
            exitSpot.pointLightInnerRadius = in0 * k;
            yield return null;
        }

        exitSpot.intensity = 0f;
        exitSpot.pointLightOuterRadius = 0f;
        exitSpot.pointLightInnerRadius = 0f;
        exitSpot.enabled = false;
    }

    // ========== 工具：强制隐藏玩家所有 Sprite（含 alpha=0） ==========

    private void ForceHidePlayerSprites()
    {
        if (playerObject == null) return;

        CachePlayerSprites(); // BaseLevelManager 里有

        foreach (var sr in _playerSprites)
        {
            if (!sr) continue;
            sr.enabled = false;
            var c = sr.color;
            c.a = 0f;
            sr.color = c;
        }
    }

    // ========== 工具：跳转到下一个 Scene（和 3-1 / 3-4 同逻辑） ==========

    private void GotoNextLoop()
    {
        if (useSceneControllerTeleport && SceneController.instance != null)
        {
            if (!string.IsNullOrEmpty(nextSceneName))
            {
                SceneController.instance.LoadSceneAndTeleport(nextSceneName, nextSpawnPointLocation);
            }
            else
            {
                Debug.LogWarning("[LevelManager3_5] 未配置 nextSceneName，无法通过 SceneController 跳转。");
            }
        }
        else if (!string.IsNullOrEmpty(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
        }
        else
        {
            Debug.LogWarning("[LevelManager3_5] 未配置下一回圈跳转：请填 nextSceneName 或开启 useSceneControllerTeleport。");
        }
    }
}
