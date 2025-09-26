// LightControl.cs — minimal static helper (soft blink + dim)
// Uses Light2D.intensity; no scene component required.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using URPLight2D = UnityEngine.Rendering.Universal.Light2D;

public static class LightControl
{
    private class Runner : MonoBehaviour { }
    private static Runner _runner;
    private static Runner R
    {
        get
        {
            if (_runner == null)
            {
                var go = new GameObject("_LightControlRunner");
                Object.DontDestroyOnLoad(go);
                _runner = go.AddComponent<Runner>();
            }
            return _runner;
        }
    }

    private static readonly Dictionary<URPLight2D, Coroutine> _blinkCo = new();
    private static readonly Dictionary<URPLight2D, Coroutine> _dimCo = new();

    // 开始柔和闪烁（cycle=整次时长；onTime=“光峰”宽度；min/max=强度范围）
    public static void StartBlink(URPLight2D light, float cycle, float onTime,
                                  float minIntensity = 0.2f, float maxIntensity = 1f)
    {
        if (!light) return;
        StopBlink(light);
        _blinkCo[light] = R.StartCoroutine(SoftBlinkRoutine(light, cycle, onTime, minIntensity, maxIntensity));
    }

    public static void StopBlink(URPLight2D light)
    {
        if (!light) return;
        if (_blinkCo.TryGetValue(light, out var co) && co != null) R.StopCoroutine(co);
        _blinkCo.Remove(light);
    }

    public static void Dim(URPLight2D light, float targetIntensity, float duration)
    {
        if (!light) return;
        if (_dimCo.TryGetValue(light, out var co) && co != null) R.StopCoroutine(co);
        _dimCo[light] = R.StartCoroutine(DimRoutine(light, Mathf.Max(0f, targetIntensity), Mathf.Max(0.0001f, duration)));
    }

    // -------- internals --------
    private static float GetI(URPLight2D l) => l ? l.intensity : 0f;
    private static void SetI(URPLight2D l, float i) { if (l) l.intensity = Mathf.Max(0f, i); }

    // 柔和脉冲：onTime 作为脉冲窗宽，位于每个 cycle 的中间；Hann 窗 + 少量噪声 + 低通
    private static IEnumerator SoftBlinkRoutine(URPLight2D light, float cycle, float onTime,
                                                float minI, float maxI)
    {
        cycle = Mathf.Max(0.05f, cycle);
        onTime = Mathf.Clamp(onTime, 0.01f, cycle * 0.95f);

        float center = cycle * 0.5f;
        float halfW = onTime * 0.5f;
        float t = 0f;
        float lastI = GetI(light);

        // 细微噪声参数（不暴露给外部，保持简单）
        float noiseSeed = Random.value * 1000f;
        float noiseAmp = 0.07f;   // 相对范围的 7% 左右
        float noiseFreq = 1.2f;    // Hz-ish
        float smoothK = 0.25f;   // 低通比例（越小越平滑）

        while (true)
        {
            t += Time.deltaTime;
            if (t >= cycle) t -= cycle;

            // 计算当前是否落在脉冲窗内（居中）
            float start = center - halfW;
            float end = center + halfW;

            float i = minI;
            if (t >= start && t <= end)
            {
                // Hann window: w ∈ [0,1] → 0.5*(1 - cos(2πw))
                float w = (t - start) / (2f * halfW);              // 0..1
                float window = 0.5f * (1f - Mathf.Cos(2f * Mathf.PI * w));
                i = Mathf.Lerp(minI, maxI, window);                // 平滑上亮下暗
            }

            // 细微呼吸/电流噪声（相对范围）
            float n = (Mathf.PerlinNoise(noiseSeed, Time.time * noiseFreq) - 0.5f) * 2f; // [-1,1]
            i += n * noiseAmp * Mathf.Max(0.0001f, (maxI - minI));

            // 低通平滑，消除“抖”
            i = Mathf.Lerp(lastI, i, smoothK);
            lastI = i;

            SetI(light, i);
            yield return null;
        }
    }

    private static IEnumerator DimRoutine(URPLight2D light, float target, float duration)
    {
        float start = GetI(light);
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            // smootherstep
            k = k * k * k * (k * (6f * k - 15f) + 10f);
            SetI(light, Mathf.Lerp(start, target, k));
            yield return null;
        }
        SetI(light, target);
        _dimCo[light] = null;
    }
}
