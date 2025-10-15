using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using URPLight2D = UnityEngine.Rendering.Universal.Light2D;

public static class LightControl
{
    // —— 内部 Runner（隐藏、常驻）——
    private sealed class Runner : MonoBehaviour { }
    private static Runner _runner;
    private static Runner RunnerObj
    {
        get
        {
            if (_runner == null)
            {
                var go = new GameObject("[LightControlRunner]");
                go.hideFlags = HideFlags.HideAndDontSave;
                UnityEngine.Object.DontDestroyOnLoad(go);
                _runner = go.AddComponent<Runner>();
            }
            return _runner;
        }
    }

    // —— 每盏灯的闪烁协程句柄 & 初始强度 —— 
    private static readonly Dictionary<URPLight2D, Coroutine> s_BlinkCo = new();
    private static readonly Dictionary<URPLight2D, float> s_BlinkOrig = new();

    /// <summary>
    /// 开始闪烁：每个 cycle 秒一个周期，前 onTime 秒亮到 max，其余时间降到 min。重复直到 StopBlink。
    /// </summary>
    public static void StartBlink(URPLight2D light, float cycle, float onTime, float minIntensity, float maxIntensity)
    {
        if (!light) return;
        StopBlink(light, resetToOriginal: false); // 先停掉已有的
        if (!s_BlinkOrig.ContainsKey(light)) s_BlinkOrig[light] = light.intensity;
        s_BlinkCo[light] = RunnerObj.StartCoroutine(CoBlink(light, cycle, onTime, minIntensity, maxIntensity));
    }

    /// <summary>
    /// 停止闪烁；可选是否把强度重置为 StartBlink 开始时的原值。
    /// </summary>
    public static void StopBlink(URPLight2D light, bool resetToOriginal = false)
    {
        if (!light) return;
        if (s_BlinkCo.TryGetValue(light, out var co) && co != null)
        {
            RunnerObj.StopCoroutine(co);
        }
        s_BlinkCo.Remove(light);

        if (resetToOriginal && s_BlinkOrig.TryGetValue(light, out var orig))
            light.intensity = orig;
        // 不强制清理 orig，允许多次 Stop 后还能恢复
    }

    private static IEnumerator CoBlink(URPLight2D light, float cycle, float onTime, float minI, float maxI)
    {
        cycle = Mathf.Max(0.01f, cycle);
        onTime = Mathf.Clamp(onTime, 0f, cycle);
        while (light)
        {
            light.intensity = maxI;
            if (onTime > 0f) yield return new WaitForSeconds(onTime);
            light.intensity = minI;
            float off = Mathf.Max(0f, cycle - onTime);
            if (off > 0f) yield return new WaitForSeconds(off);
        }
    }

    /// <summary>
    /// 变更强度（启动并返回协程）；若想等待完成，建议使用 DimIE 并 yield return。
    /// </summary>
    public static Coroutine Dim(URPLight2D light, float targetIntensity, float duration, AnimationCurve curve = null, Action onComplete = null)
    {
        return RunnerObj.StartCoroutine(DimIE(light, targetIntensity, duration, curve, onComplete));
    }

    /// <summary>
    /// 变更强度（可被 yield 等待）。
    /// </summary>
    public static IEnumerator DimIE(URPLight2D light, float targetIntensity, float duration, AnimationCurve curve = null, Action onComplete = null)
    {
        if (!light)
        {
            onComplete?.Invoke();
            yield break;
        }

        float start = light.intensity;
        duration = Mathf.Max(0f, duration);
        if (duration == 0f)
        {
            light.intensity = targetIntensity;
            onComplete?.Invoke();
            yield break;
        }

        curve ??= AnimationCurve.EaseInOut(0, 0, 1, 1);
        float t = 0f;
        while (t < duration && light)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / duration);
            float k = Mathf.Clamp01(curve.Evaluate(u));
            light.intensity = Mathf.Lerp(start, targetIntensity, k);
            yield return null;
        }
        if (light) light.intensity = targetIntensity;
        onComplete?.Invoke();
    }

    /// <summary>
    /// 记录一组灯的当前强度（用于之后还原）。
    /// </summary>
    public static float[] CaptureIntensities(IList<URPLight2D> lights)
    {
        if (lights == null) return Array.Empty<float>();
        var arr = new float[lights.Count];
        for (int i = 0; i < lights.Count; i++) arr[i] = lights[i] ? lights[i].intensity : 1f;
        return arr;
    }

    /// <summary>
    /// 对一组灯进行 N 次闪烁：每次先全黑 offTime，再还原到 original 强度 onTime。
    /// original==null 时使用调用当下的强度作为“亮”的值。
    /// </summary>
    public static IEnumerator FlickerSequenceIE(IList<URPLight2D> lights, int count, float offTime, float onTime, float[] original = null)
    {
        if (lights == null || lights.Count == 0) yield break;
        original ??= CaptureIntensities(lights);

        count = Mathf.Max(1, count);
        offTime = Mathf.Max(0f, offTime);
        onTime = Mathf.Max(0f, onTime);

        for (int k = 0; k < count; k++)
        {
            // off
            for (int i = 0; i < lights.Count; i++) if (lights[i]) lights[i].intensity = 0f;
            if (offTime > 0f) yield return new WaitForSeconds(offTime);

            // on (restore)
            for (int i = 0; i < lights.Count; i++) if (lights[i]) lights[i].intensity = original[Mathf.Clamp(i, 0, original.Length - 1)];
            if (onTime > 0f) yield return new WaitForSeconds(onTime);
        }
    }

    /// <summary>
    /// 一次“黑→回亮”的脉冲；中途提供 onWhileDark 回调（可在黑的瞬间换脸/播放音效）。
    /// </summary>
    public static IEnumerator PulseOnceIE(IList<URPLight2D> lights, float offTime, float onTime, Action onWhileDark, float[] original = null)
    {
        if (lights == null || lights.Count == 0)
        {
            onWhileDark?.Invoke();
            if (offTime > 0f) yield return new WaitForSeconds(offTime);
            if (onTime > 0f) yield return new WaitForSeconds(onTime);
            yield break;
        }

        original ??= CaptureIntensities(lights);

        // off
        for (int i = 0; i < lights.Count; i++) if (lights[i]) lights[i].intensity = 0f;

        // 黑的瞬间
        onWhileDark?.Invoke();

        if (offTime > 0f) yield return new WaitForSeconds(offTime);

        // on （还原）
        for (int i = 0; i < lights.Count; i++) if (lights[i]) lights[i].intensity = original[Mathf.Clamp(i, 0, original.Length - 1)];

        if (onTime > 0f) yield return new WaitForSeconds(onTime);
    }

    // 放进 LightControl 里（与现有代码同级）
    // 1) 通用：强度平滑过渡
    public static IEnumerator FadeIntensityIE(URPLight2D light, float target, float duration, AnimationCurve curve = null)
    {
        if (!light) yield break;
        duration = Mathf.Max(0.0001f, duration);
        float start = light.intensity;
        curve ??= AnimationCurve.EaseInOut(0, 0, 1, 1);
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / duration);
            float k = Mathf.Clamp01(curve.Evaluate(u));
            light.intensity = Mathf.Lerp(start, target, k);
            yield return null;
        }
        light.intensity = target;
    }

    // 2) 根据一组“节奏步”做有限次闪烁（带软边），steps: (cycle, onTime, minI, maxI)
    public static IEnumerator BlinkPatternIE(
        URPLight2D light,
        (float cycle, float on, float minI, float maxI)[] steps,
        float edge = 0.08f,          // 亮/灭的过渡时长
        float minFloor = 0.02f,      // 最暗不至 0，更自然
        AnimationCurve curve = null  // 过渡曲线
    )
    {
        if (!light || steps == null || steps.Length == 0) yield break;

        float baseline = (light.intensity > 0f ? light.intensity : 0.9f);
        curve ??= AnimationCurve.EaseInOut(0, 0, 1, 1);

        foreach (var s in steps)
        {
            float onI = (s.maxI > 0f ? s.maxI : baseline);
            float offI = Mathf.Max(minFloor, s.minI);

            float onHold = Mathf.Max(0f, s.on - edge);
            float offHold = Mathf.Max(0f, s.cycle - s.on - edge);

            // 亮 → 持续
            yield return FadeIntensityIE(light, onI, edge, curve);
            if (onHold > 0f) yield return new WaitForSeconds(onHold);

            // 灭 → 持续
            yield return FadeIntensityIE(light, offI, edge, curve);
            if (offHold > 0f) yield return new WaitForSeconds(offHold);
        }
    }

    // 3) 便捷：按 steps 闪完后，再平滑变暗到目标值（非黑）
    public static IEnumerator BlinkThenDimIE(
        URPLight2D light,
        (float cycle, float on, float minI, float maxI)[] steps,
        float dimTarget, float dimDuration,
        float edge = 0.08f, float minFloor = 0.02f, AnimationCurve curve = null
    )
    {
        if (!light) yield break;
        yield return BlinkPatternIE(light, steps, edge, minFloor, curve);
        yield return FadeIntensityIE(light, dimTarget, dimDuration, curve);
    }

}
