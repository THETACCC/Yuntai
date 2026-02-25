using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Cinemachine;
using URPLight2D = UnityEngine.Rendering.Universal.Light2D;
using static AudioManager;

public class LevelManager3_1 : BaseLevelManager
{
    [Header("Next Loop 跳转")]
    [Tooltip("下一回圈的场景名（若用 SceneController 也请填）")]
    [SerializeField] private string nextSceneName;
    [Tooltip("若使用 SceneController，则指定出生点编号")]
    [SerializeField] private int nextSpawnPointLocation = 0;
    [Tooltip("使用 SceneController.instance.LoadSceneAndTeleport() 跳转")]
    [SerializeField] private bool useSceneControllerTeleport = true;

    [Header("Spot Light 演出")]
    [SerializeField] private URPLight2D deathSpot;
    [SerializeField, Min(0f)] private float spotRiseDuration = 3f;
    [SerializeField] private float spotTargetIntensity = 1000f;
    [SerializeField] private float spotTargetOuterRadius = 7.5f;
    [SerializeField] private float spotTargetInnerRadius = 3f;
    [SerializeField] private float spotQuickFadeDuration = 0.25f;

    [Header("Actors (先切换坐->站)")]
    [SerializeField] private GameObject zhoushuSitting;   // 周叔（坐着版本）
    [SerializeField] private GameObject zhoushuStanding;  // 周叔（站着版本，后续会消失）

    [Header("Camera 设置")]
    [Tooltip("3-1 开场是否强制把相机对准玩家")]
    [SerializeField] private bool resetCameraOnStart = true;

    [Header("Timing / Debug")]
    [SerializeField] private bool useUnscaledTime = true;
    [SerializeField] private bool verboseLog = false;

    [Header("ZhouShu 失败时相关的物体")]
    [SerializeField] private List<GameObject> zhoushuFailObjects = new();
    [SerializeField] private GameObject interactableSeat;
    [SerializeField] private GameObject playerSit;   // 坐姿替身

    // runtime
    private Coroutine _escapeRoutine;
    private CinemachineVirtualCamera _vcam;

    // ===== 生命周期 =====
    protected override void Awake()
    {
        // 先让 BaseLevelManager 做：锁/找 Player、缓存 sprite 等
        base.Awake();

        // 初始化灯
        if (deathSpot)
        {
            if (!deathSpot.gameObject.activeInHierarchy) deathSpot.gameObject.SetActive(true);
            if (!deathSpot.enabled) deathSpot.enabled = true;
            var lt = UnityEngine.Rendering.Universal.Light2D.LightType.Point;
            if (deathSpot.lightType != lt) deathSpot.lightType = lt;
            deathSpot.intensity = 0f;
            deathSpot.pointLightOuterRadius = 0f;
            deathSpot.pointLightInnerRadius = 0f;
            if (spotTargetInnerRadius > spotTargetOuterRadius)
                spotTargetInnerRadius = Mathf.Max(0f, spotTargetOuterRadius - 0.01f);
        }

        // 相机引用
#if UNITY_2023_1_OR_NEWER
        _vcam = FindFirstObjectByType<CinemachineVirtualCamera>();
#else
        _vcam = FindObjectOfType<CinemachineVirtualCamera>();
#endif
    }

    private void Start()
    {
        // 3-1 开场：玩家应该是可见 + 可以走
        if (Gamemanager.instance != null)
            Gamemanager.instance.phase = GamePhase.Moving;

        if (playerCtrl != null)
            playerCtrl.EnablePlayerControl();

        // 强制把所有 Player sprite 打开并把 alpha 设为 1
        ForceShowPlayerSpritesFullOpaque();

        // 坐姿替身默认关掉（如果开场就要坐，你之后可以手动 PlayerSit）
        if (playerSit != null)
            playerSit.SetActive(false);

        // 重设相机 Follow
        if (resetCameraOnStart && _vcam != null && playerObject != null)
        {
            _vcam.Follow = playerObject.transform;
        }

        if (verboseLog)
            Debug.Log("[LevelManager3_1] Start: 恢复玩家可见 & 可移动。");
    }

    // ================== 周叔逃走 ==================

    /// <summary>触发：坐->站，打灯，上人消失，玩家隐藏，灯灭，跳下一回圈。</summary>
    public void ZhouShuEscape()
    {
        if (_escapeRoutine != null) StopCoroutine(_escapeRoutine);
        _escapeRoutine = StartCoroutine(CoZhouShuEscape());
    }

    private IEnumerator CoZhouShuEscape()
    {
        AudioManager.Play("Sound Effects/Henk/sndZhouShuMirror", AudioGroup.SFX);
        AudioManager.Play("Sound Effects/Chapter1/sndZhouShuTeleport", AudioGroup.SFX);
        if (verboseLog) Debug.Log("[3-1] ZhouShuEscape start");

        // 0) 锁住玩家：不能再乱走
        DisablePlayerMovement();              // Base 的接口：锁 GamePhase + PlayerController
        if (playerRb != null)
        {
            playerRb.velocity = Vector2.zero;
            playerRb.isKinematic = false;     // 保持物理，但速度为 0
        }

        // 切换坐->站
        SwitchZhoushuToStanding();
        //Mirror AUdio

        // 1) 灯从无到有
        InitDeathLight();
        if (deathSpot && spotRiseDuration > 0f)
        {
            float t = 0f;
            while (t < spotRiseDuration)
            {
                t += DeltaTime();
                float s = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / spotRiseDuration));
                deathSpot.intensity = Mathf.Lerp(0f, spotTargetIntensity, s);
                deathSpot.pointLightOuterRadius = Mathf.Lerp(0f, spotTargetOuterRadius, s);
                deathSpot.pointLightInnerRadius = Mathf.Lerp(0f, spotTargetInnerRadius, s);
                yield return null;
            }
            deathSpot.intensity = spotTargetIntensity;
            deathSpot.pointLightOuterRadius = spotTargetOuterRadius;
            deathSpot.pointLightInnerRadius = spotTargetInnerRadius;
        }

        // 2) 让“站着”的周叔消失（整物体禁用）
        if (zhoushuStanding && zhoushuStanding.activeSelf)
            zhoushuStanding.SetActive(false);

        // 3) 主角隐藏（只隐藏 sprite，不 Destroy）
        HidePlayerSprites();      // BaseLevelManager 提供
        // 保险：把 alpha 也清理一下
        ForceHidePlayerSprites();

        // 4) 灯快速淡出
        yield return FadeOutDeathLight();

        // 5) 跳到下一回圈
        GotoNextLoop();

        if (verboseLog) Debug.Log("[3-1] ZhouShuEscape end");
        _escapeRoutine = null;
    }

    // ================== 坐下 / 站起（给 Fungus 之类调用） ==================

    public void PlayerSit()
    {
        // 隐藏玩家 Sprite，启用坐姿替身
        HidePlayerSprites();
        ForceHidePlayerSprites();

        if (playerSit != null)
            playerSit.SetActive(true);

        if (playerCtrl != null)
            playerCtrl.DisablePlayerControl();

        if (Gamemanager.instance != null)
            Gamemanager.instance.phase = GamePhase.Eventing;
    }

    public void PlayerStand()
    {
        // 关闭坐姿替身，恢复玩家 sprite
        if (playerSit != null)
            playerSit.SetActive(false);

        RevealPlayerSprites();
        ForceShowPlayerSpritesFullOpaque();

        if (playerCtrl != null)
            playerCtrl.EnablePlayerControl();

        if (Gamemanager.instance != null)
            Gamemanager.instance.phase = GamePhase.Moving;

        // 确保相机重新 Follow Player
        if (_vcam != null && playerObject != null)
            _vcam.Follow = playerObject.transform;
    }

    // ================== ZhouShu 失败 ==================
    public void ZhouShuFailed()
    {
        foreach (var go in zhoushuFailObjects)
        {
            if (go != null && go.activeSelf)
                go.SetActive(false);
        }

        if (interactableSeat != null)
            interactableSeat.SetActive(true);
    }

    // ================== 工具函数 ==================

    private void SwitchZhoushuToStanding()
    {
        if (zhoushuSitting && zhoushuSitting.activeSelf)
            zhoushuSitting.SetActive(false);

        if (zhoushuStanding && !zhoushuStanding.activeSelf)
            zhoushuStanding.SetActive(true);
    }

    // 把 BaseLevelManager 缓存的 sprite 全部打开 + alpha=1
    private void ForceShowPlayerSpritesFullOpaque()
    {
        if (playerObject == null) return;

        // Base 里已经 Cache 过一次，但再取一次也没问题
        CachePlayerSprites();

        foreach (var sr in _playerSprites)
        {
            if (!sr) continue;
            sr.enabled = true;
            var c = sr.color;
            c.a = 1f;
            sr.color = c;
        }
    }

    // 把 sprite 全关掉（包括 alpha = 0，防止别的地方手滑改了）
    private void ForceHidePlayerSprites()
    {
        if (playerObject == null) return;

        CachePlayerSprites();
        foreach (var sr in _playerSprites)
        {
            if (!sr) continue;
            sr.enabled = false;
            var c = sr.color;
            c.a = 0f;
            sr.color = c;
        }
    }

    private void InitDeathLight()
    {
        if (!deathSpot) return;
        if (!deathSpot.gameObject.activeInHierarchy) deathSpot.gameObject.SetActive(true);
        if (!deathSpot.enabled) deathSpot.enabled = true;
        var lt = UnityEngine.Rendering.Universal.Light2D.LightType.Point;
        if (deathSpot.lightType != lt) deathSpot.lightType = lt;
        deathSpot.intensity = 0f;
        deathSpot.pointLightOuterRadius = 0f;
        deathSpot.pointLightInnerRadius = 0f;

        if (spotTargetInnerRadius > spotTargetOuterRadius)
            spotTargetInnerRadius = Mathf.Max(0f, spotTargetOuterRadius - 0.01f);
    }

    private IEnumerator FadeOutDeathLight()
    {
        if (!deathSpot) yield break;
        if (!deathSpot.enabled || (deathSpot.intensity <= 0.001f && deathSpot.pointLightOuterRadius <= 0.001f))
            yield break;

        float i0 = deathSpot.intensity;
        float o0 = deathSpot.pointLightOuterRadius;
        float in0 = deathSpot.pointLightInnerRadius;

        float tf = 0f;
        float dur = Mathf.Max(0.01f, spotQuickFadeDuration);
        while (tf < dur)
        {
            tf += DeltaTime();
            float u = Mathf.Clamp01(tf / dur);
            float k = 1f - u;
            deathSpot.intensity = i0 * k;
            deathSpot.pointLightOuterRadius = o0 * k;
            deathSpot.pointLightInnerRadius = in0 * k;
            yield return null;
        }

        deathSpot.intensity = 0f;
        deathSpot.pointLightOuterRadius = 0f;
        deathSpot.pointLightInnerRadius = 0f;
        deathSpot.enabled = false;
    }

    private void GotoNextLoop()
    {
        if (useSceneControllerTeleport && SceneController.instance != null)
        {
            if (!string.IsNullOrEmpty(nextSceneName))
                SceneController.instance.LoadSceneAndTeleport(nextSceneName, nextSpawnPointLocation);
            else
                Debug.LogWarning("[LevelManager3_1] 未配置 nextSceneName，无法通过 SceneController 跳转。");
        }
        else if (!string.IsNullOrEmpty(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
        }
        else
        {
            Debug.LogWarning("[LevelManager3_1] 未配置下一回圈跳转：请填 nextSceneName 或开启 useSceneControllerTeleport。");
        }
    }

    private float DeltaTime() => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
}
