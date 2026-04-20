using System.Collections;
using UnityEngine;

public class UIShake : MonoBehaviour
{
    [SerializeField] private RectTransform target;

    [Header("Default Shake")]
    private float defaultStrength = 220f;
    private float defaultDuration = 0.35f;

    private Coroutine running;
    private Vector2 basePos;

    void Awake()
    {
        if (target == null)
            target = GetComponent<RectTransform>();

        if (target != null)
            basePos = target.anchoredPosition;
    }

    public void Shake()
    {
        Shake(defaultStrength, defaultDuration);
    }

    public void Shake(float strength, float duration)
    {
        if (target == null) return;

        if (running != null)
            StopCoroutine(running);

        running = StartCoroutine(CoShake(strength, duration));
    }

    public void StopShake()
    {
        if (running != null)
            StopCoroutine(running);

        running = null;

        if (target != null)
            target.anchoredPosition = basePos;
    }

    private IEnumerator CoShake(float strength, float duration)
    {
        basePos = target.anchoredPosition;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            Vector2 offset = Random.insideUnitCircle * strength;
            target.anchoredPosition = basePos + offset;
            yield return null;
        }

        target.anchoredPosition = basePos;
        running = null;
    }
}