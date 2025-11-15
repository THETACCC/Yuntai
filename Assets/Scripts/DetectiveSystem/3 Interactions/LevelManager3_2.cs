using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using URPLight2D = UnityEngine.Rendering.Universal.Light2D;

public class LevelManager3_2 : MonoBehaviour
{
    [Header("Spot Light 演出")]
    [SerializeField] private URPLight2D deathSpot;
    [SerializeField, Min(0f)] private float spotRiseDuration = 3f;
    [SerializeField] private float spotTargetIntensity = 1000f;
    [SerializeField] private float spotTargetOuterRadius = 7.5f;
    [SerializeField] private float spotTargetInnerRadius = 3f;
    [SerializeField] private float spotQuickFadeDuration = 0.25f;

    [Header("Actor (周叔站立版)")]
    [SerializeField] private GameObject zhoushuStanding;

    [Header("Timing / Debug")]
    [SerializeField] private bool useUnscaledTime = true;
    [SerializeField] private bool verboseLog = false;

    // runtime
    private Coroutine _leaveRoutine;

    private void Awake()
    {
        // 初始化灯（先关到 0，类型设成 Point）
        InitDeathLight();
    }

    private void Start()
    {
        // 用 tag 找到 DontDestroyOnLoad 的 Player
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (!player)
        {
            Debug.LogWarning("[LevelManager3_2] 没有找到 tag=Player 的对象。");
            return;
        }

        // 把她身上（以及子物体上）的所有 SpriteRenderer 打开
        SpriteRenderer[] sprites = player.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var sr in sprites)
        {
            if (sr != null)
                sr.enabled = true;
        }

        if (verboseLog)
            Debug.Log($"[LevelManager3_2] 已为 Player 重新启用 {sprites.Length} 个 SpriteRenderer。");
    }

    /// <summary>
    /// 周叔离开：只做灯光 + 周叔消失，不动 Player、不切场景。
    /// </summary>
    public void ZhouShuLeave()
    {
        if (_leaveRoutine != null) StopCoroutine(_leaveRoutine);
        _leaveRoutine = StartCoroutine(CoZhouShuLeave());
    }

    private IEnumerator CoZhouShuLeave()
    {
        if (verboseLog) Debug.Log("[3-2] ZhouShuLeave start");

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

        // 2) 周叔消失（整物体禁用）
        if (zhoushuStanding && zhoushuStanding.activeSelf)
            zhoushuStanding.SetActive(false);

        // 3) 灯快速淡出（如果你想一直亮，可以把这行改成 "yield break;"）
        yield return FadeOutDeathLight();

        if (verboseLog) Debug.Log("[3-2] ZhouShuLeave end");
        _leaveRoutine = null;
    }

    // —— 工具函数 —— //

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

        // 防止 inner > outer 出错
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

    private float DeltaTime() => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
}
