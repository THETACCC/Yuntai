using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using URPLight2D = UnityEngine.Rendering.Universal.Light2D;

public class LevelManager3_1 : MonoBehaviour
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

    [Header("Timing / Debug")]
    [SerializeField] private bool useUnscaledTime = true;
    [SerializeField] private bool verboseLog = false;

    // runtime
    private Coroutine _escapeRoutine;
    private GameObject _player;
    private readonly List<SpriteRenderer> _playerSprites = new();

    private void Awake()
    {
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
        }

        // 找主角并缓存所有 SpriteRenderer（仅控制显隐）
        _player = GameObject.FindGameObjectWithTag("Player");
        if (!_player)
            Debug.LogWarning("[LevelManager3_1] 未找到 tag=Player 的对象。");
        else
            CachePlayerSprites(_player);
    }

    /// <summary>触发：坐->站，打灯，上人消失，玩家消失，灯灭，跳下一回圈。</summary>
    public void ZhouShuEscape()
    {
        if (_escapeRoutine != null) StopCoroutine(_escapeRoutine);
        _escapeRoutine = StartCoroutine(CoZhouShuEscape());
    }

    private IEnumerator CoZhouShuEscape()
    {
        if (verboseLog) Debug.Log("[3-1] ZhouShuEscape start");

        // 0) 先切换对象：关闭“坐着”，启用“站着”
        SwitchZhoushuToStanding();

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

        // 3) 主角仅隐藏 Sprite（不 Destroy、不禁用脚本/碰撞，只看不见）
        SetPlayerSpritesVisible(false);

        // 4) 灯快速淡出
        yield return FadeOutDeathLight();

        // 5) 跳到下一回圈
        GotoNextLoop();

        if (verboseLog) Debug.Log("[3-1] ZhouShuEscape end");
        _escapeRoutine = null;
    }

    // —— 工具 —— //
    private void SwitchZhoushuToStanding()
    {
        // 关闭坐着
        if (zhoushuSitting && zhoushuSitting.activeSelf)
            zhoushuSitting.SetActive(false);

        // 启用站着
        if (zhoushuStanding && !zhoushuStanding.activeSelf)
            zhoushuStanding.SetActive(true);
    }

    private void CachePlayerSprites(GameObject playerRoot)
    {
        _playerSprites.Clear();
        if (!playerRoot) return;
        _playerSprites.AddRange(playerRoot.GetComponentsInChildren<SpriteRenderer>(true));
    }

    private void SetPlayerSpritesVisible(bool visible)
    {
        foreach (var sr in _playerSprites) if (sr) sr.enabled = visible;
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
