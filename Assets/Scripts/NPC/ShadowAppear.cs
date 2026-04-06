using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class ShadowAppear : MonoBehaviour
{
    [Header("Timing")]
    [SerializeField] private float interval = 8f;      // 每隔多久触发一次
    [SerializeField] private float fadeInTime = 1f;    // 出现用时
    [SerializeField] private float fadeOutTime = 1f;   // 消失用时

    [Header("Alpha")]
    [Range(0f, 1f)]
    [SerializeField] private float maxAlpha = 0.8f;    // 最大透明度

    private SpriteRenderer sr;
    private Color originalColor;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        originalColor = sr.color;

        SetAlpha(0f); // 一开始完全透明
    }

    private void OnEnable()
    {
        StartCoroutine(ShadowLoop());
    }

    private IEnumerator ShadowLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(interval);

            // 从 0 淡入到 maxAlpha
            yield return StartCoroutine(FadeAlpha(0f, maxAlpha, fadeInTime));

            // 再从 maxAlpha 淡出到 0
            yield return StartCoroutine(FadeAlpha(maxAlpha, 0f, fadeOutTime));
        }
    }

    private IEnumerator FadeAlpha(float from, float to, float duration)
    {
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = timer / duration;
            float alpha = Mathf.Lerp(from, to, t);
            SetAlpha(alpha);
            yield return null;
        }

        SetAlpha(to);
    }

    private void SetAlpha(float alpha)
    {
        Color c = originalColor;
        c.a = alpha;
        sr.color = c;
    }
}