using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using URPLight2D = UnityEngine.Rendering.Universal.Light2D;

public class LevelManager3_4 : BaseLevelManager
{
    // ========== ① 黑场 + Zhoushu2 对话 ==========

    [Header("全局灯光（URP 2D Global Light）")]
    [SerializeField] private URPLight2D globalLight;

    [Header("黑场与渐变时间设置")]
    [SerializeField, Min(0f)] private float fadeOutDuration = 0.5f;   // 灯光缓出（变黑）时间
    [SerializeField, Min(0f)] private float blackHoldDuration = 2.5f; // 全黑停留时间
    [SerializeField, Min(0f)] private float fadeInDuration = 0.8f;    // 灯光缓入（变亮）时间

    [Header("周叔对话 Zhoushu2")]
    [SerializeField] private DialogueTrigger dialogueZhoushu2;
    [SerializeField, Min(0f)] private float dialogueDelayAfterFadeIn = 0f; // 灯完全亮起后，再等多久才叫对话

    // ========== ② 周叔离场 + 跳下一回圈 ==========

    [Header("Next Loop 跳转")]
    [Tooltip("下一回圈的场景名（若用 SceneController 也请填）")]
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

    [Header("Actors")]
    [SerializeField] private GameObject zhoushuObject; // 3-4 里要被“传送走”的周叔

    [Header("Debug")]
    [SerializeField] private bool verboseLog = false;

    private Coroutine _blackoutRoutine;
    private Coroutine _exitRoutine;

    // ========== ① 黑场 + Zhoushu2 对话：对外接口 ==========

    /// <summary>
    /// 从外部（Fungus/Trigger）调用：
    /// 让全局灯光黑 2.5 秒（带缓入缓出），然后触发对话 Zhoushu2
    /// </summary>
    public void PlayZhoushu2Blackout()
    {
        if (_blackoutRoutine != null)
            return; // 防止重复触发

        _blackoutRoutine = StartCoroutine(BlackoutThenDialogueRoutine());
    }

    private IEnumerator BlackoutThenDialogueRoutine()
    {
        if (!globalLight)
        {
            Debug.LogWarning("[LevelManager3_4] globalLight 未设置，无法执行黑场演出。");
            yield break;
        }

        // ✅ 黑屏开始前：锁玩家（用 BaseLevelManager 提供的接口）
        DisablePlayerMovement();
        if (Gamemanager.instance)
            Gamemanager.instance.phase = GamePhase.Eventing;

        float originalIntensity = globalLight.intensity;

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

        // ✅ 黑屏 + 渐亮结束：立刻解锁玩家
        EnablePlayerMovement();
        if (Gamemanager.instance)
            Gamemanager.instance.phase = GamePhase.Moving;

        // 灯亮以后再等一点点时间（可选）
        if (dialogueDelayAfterFadeIn > 0f)
            yield return new WaitForSeconds(dialogueDelayAfterFadeIn);

        // --- 4) 触发周叔对话 Zhoushu2 ---
        if (dialogueZhoushu2)
        {
            dialogueZhoushu2.TriggerDialogue();
        }
        else
        {
            Debug.LogWarning("[LevelManager3_4] dialogueZhoushu2 未指定。");
        }

        _blackoutRoutine = null;
    }

    // ========== ② 周叔离场 + 跳下一回圈：对外接口 ==========

    /// <summary>
    /// 和 3-1 的 ZhouShuEscape 类似：
    /// Spot Light 亮起 → 周叔和玩家“消失” → 切到下一回圈 Scene。
    /// </summary>
    public void ZhouShuLeave_3_4()
    {
        if (_exitRoutine != null)
            StopCoroutine(_exitRoutine);

        _exitRoutine = StartCoroutine(CoZhouShuLeave_3_4());
    }

    private IEnumerator CoZhouShuLeave_3_4()
    {
        if (verboseLog) Debug.Log("[3-4] ZhouShuLeave_3_4 start");

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
            zhoushuObject.SetActive(false);

        HidePlayerSprites();      // BaseLevelManager 提供
        ForceHidePlayerSprites(); // 把 alpha 也置 0，保险

        // 4) Spot Light 快速淡出
        yield return FadeOutExitSpot();

        // 5) 跳到下一回圈
        GotoNextLoop();

        if (verboseLog) Debug.Log("[3-4] ZhouShuLeave_3_4 end");
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

    // ========== 工具：隐藏玩家所有 Sprite（和 3-1 同逻辑） ==========

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

    // ========== 工具：跳转到下一个 Scene（和 3-1 同逻辑） ==========

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
                Debug.LogWarning("[LevelManager3_4] 未配置 nextSceneName，无法通过 SceneController 跳转。");
            }
        }
        else if (!string.IsNullOrEmpty(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
        }
        else
        {
            Debug.LogWarning("[LevelManager3_4] 未配置下一回圈跳转：请填 nextSceneName 或开启 useSceneControllerTeleport。");
        }
    }
}
