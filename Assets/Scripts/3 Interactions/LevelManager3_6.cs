using System.Collections;
using UnityEngine;
using URPLight2D = UnityEngine.Rendering.Universal.Light2D;
using DialogueSystem;
using UnityEngine.SceneManagement;

public class LevelManager3_6 : BaseLevelManager
{
    [Header("ZhouShu 离场 Spot Light")]
    [SerializeField] private URPLight2D exitSpot;
    [SerializeField, Min(0f)] private float spotRiseDuration = 3f;
    [SerializeField] private float spotTargetIntensity = 1000f;
    [SerializeField] private float spotTargetOuterRadius = 7.5f;
    [SerializeField] private float spotTargetInnerRadius = 3f;
    [SerializeField, Min(0f)] private float spotQuickFadeDuration = 0.25f;

    [Header("要离场的周叔对象（只隐藏周叔，不隐藏玩家）")]
    [SerializeField] private GameObject zhoushuObject;

    [Header("周叔离场后要触发的对话（3-6 start2）")]
    [SerializeField] private DialogueTrigger zhoushuStart2Trigger;

    // scene跳转
    [Header("Next Loop 跳转")]
    [Tooltip("下一回合的场景名（若用 Scene Controller 也请填）")]
    [SerializeField] private string nextSceneName;

    [Tooltip("若使用 SceneController，则指定出生点编号")]
    [SerializeField] private int nextSpawnPointLocation = 0;

    [Tooltip("使用 SceneController.instance.LoadSceneAndTeleport() 跳转")]
    [SerializeField] private bool useSceneControllerTeleport = true;

    [Header("Debug")]
    [SerializeField] private bool verboseLog = false;

    private Coroutine _leaveRoutine;

    protected override void Awake()
    {
        // 3-6 这关默认一进来玩家可见 & 可动
        hidePlayerOnSceneStart = false;
        lockPlayerOnSceneStart = false;
        base.Awake();
    }

    private void Start()
    {
        // 再保险一次
        ShowPlayerAndAllowMove();

        if (!exitSpot)
            Debug.LogWarning("[LevelManager3_6] exitSpot 未设置，离场演出将不会有灯光。");

        if (!zhoushuObject)
            Debug.LogWarning("[LevelManager3_6] zhoushuObject 未设置，无法让周叔离场。");

        if (!zhoushuStart2Trigger)
            Debug.LogWarning("[LevelManager3_6] zhoushuStart2Trigger 未设置，离场后不会触发对话。");
    }

    /// <summary>
    /// 给 Fungus / Trigger 调用：
    /// 周叔一个人被 Spot Light 打亮 → 周叔消失 → 触发 3-6 start2 对话。
    /// （玩家不会被隐藏，也不会切换 Scene）
    /// </summary>
    public void ZhouShuLeave_3_6()
    {
        if (_leaveRoutine != null)
            StopCoroutine(_leaveRoutine);

        _leaveRoutine = StartCoroutine(CoZhouShuLeave_3_6());
    }

    private IEnumerator CoZhouShuLeave_3_6()
    {
        if (verboseLog) Debug.Log("[3-6] ZhouShuLeave_3_6 start");

        // 0) 锁住玩家移动（如果你想一边走一边看这场，可以把这几行注释掉）
        DisablePlayerMovement();
        if (Gamemanager.instance)
            Gamemanager.instance.phase = GamePhase.Eventing;

        if (playerRb != null)
        {
            playerRb.velocity = Vector2.zero;
            // 保持 dynamic / kinematic 设置，不强制改
        }

        // 1) 初始化 Spot Light（从 0 开始）
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

        // 3) 周叔“被光吞掉”——直接隐藏周叔
        if (zhoushuObject && zhoushuObject.activeSelf)
            zhoushuObject.SetActive(false);

        // 4) Spot Light 快速淡出（环境回到正常）
        yield return FadeOutExitSpot();

        // 5) 演出结束：恢复玩家移动
        EnablePlayerMovement();
        if (Gamemanager.instance)
            Gamemanager.instance.phase = GamePhase.Moving;

        // 6) 触发 3-6 start2 对话
        if (zhoushuStart2Trigger)
        {
            zhoushuStart2Trigger.TriggerDialogue();
        }
        else
        {
            Debug.LogWarning("[LevelManager3_6] zhoushuStart2Trigger 未设置，无法触发 3-6 start2 对话。");
        }

        if (verboseLog) Debug.Log("[3-6] ZhouShuLeave_3_6 end");
        _leaveRoutine = null;
    }
    // ========== scene跳转 ==========

    public void GotoNextLoop()
    {
        if (useSceneControllerTeleport && SceneController.instance != null)
        {
            if (!string.IsNullOrEmpty(nextSceneName))
                SceneController.instance.LoadSceneAndTeleport(nextSceneName, nextSpawnPointLocation);
            else
                Debug.LogWarning("[LevelManager3_6] 未配置 nextSceneName，无法通过 SceneController 跳转。");
        }
        else if (!string.IsNullOrEmpty(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
        }
        else
        {
            Debug.LogWarning("[LevelManager3_6] 未配置下一回合跳转：请填 nextSceneName 或开启 useSceneControllerTeleport。");
        }
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
}
