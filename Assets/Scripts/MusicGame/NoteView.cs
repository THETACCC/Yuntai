using System.Collections;
using UnityEngine;

public class NoteView : MonoBehaviour
{
    [Header("Drag these from prefab children")]
    [SerializeField] private RectTransform targetCircle;
    [SerializeField] private RectTransform shrinkingRing;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Scales")]
    [SerializeField] private float targetScale = 1.48f;
    [SerializeField] private float startRingScale = 2.56f;

    [Header("Vanish")]
    [SerializeField] private float vanishDuration = 0.12f;
    [SerializeField] private float vanishScale = 0.85f;

    public double NoteTime { get; private set; }
    public bool IsJudged { get; private set; }

    private double spawnTime;
    private double preSpawnTime;
    private double hitWindow;

    public void Init(double noteTime, double spawnTime, double preSpawnTime,
                     double hitWindow)
    {
        NoteTime = noteTime;
        this.spawnTime = spawnTime;
        this.preSpawnTime = preSpawnTime;
        this.hitWindow = hitWindow;

        IsJudged = false;
        canvasGroup.alpha = 1f;

        targetCircle.localScale = Vector3.one * targetScale;
        shrinkingRing.localScale = Vector3.one * startRingScale;
    }

    public void SetAnchoredPosition(Vector2 anchoredPos)
    {
        ((RectTransform)transform).anchoredPosition = anchoredPos;
    }

    void Update()
    {
        if (IsJudged) return;

        double now = AudioSettings.dspTime;

        // shrink progress
        double t = (now - spawnTime) / preSpawnTime;
        float k = Mathf.Clamp01((float)t);

        float ringScale = Mathf.Lerp(startRingScale, targetScale, k);
        shrinkingRing.localScale = Vector3.one * ringScale;

        // timeout miss
        if (now > NoteTime + hitWindow)
        {
            IsJudged = true;
            StartCoroutine(VanishAndDestroy());
        }
    }

    public void Judge(double now)
    {
        if (IsJudged) return;
        IsJudged = true;

        StartCoroutine(VanishAndDestroy());
    }

    IEnumerator VanishAndDestroy()
    {
        float t = 0f;
        float startAlpha = canvasGroup.alpha;
        Vector3 startScale = transform.localScale;
        Vector3 endScale = startScale * vanishScale;

        while (t < vanishDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / vanishDuration);
            canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, k);
            transform.localScale = Vector3.Lerp(startScale, endScale, k);
            yield return null;
        }

        Destroy(gameObject);
    }
}
