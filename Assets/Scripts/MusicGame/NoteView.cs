using System;
using System.Collections;
using UnityEngine;

public class NoteView : MonoBehaviour
{
    [Header("Drag these from prefab children")]
    [SerializeField] private RectTransform targetCircle;
    [SerializeField] private RectTransform shrinkingRing;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Scales")]
    [SerializeField] private float baseTargetScale = 1.48f; // 你说的 target scale
    [SerializeField] private float startRingScale = 2.56f;  // 你说的 shrinking 开始 scale

    [Header("Vanish")]
    [SerializeField] private float vanishDuration = 0.12f;
    [SerializeField] private float vanishScale = 0.85f;

    public double NoteTime { get; private set; }
    public bool IsJudged { get; private set; }

    public Action<NoteView> OnMiss;

    private double spawnTime;
    private double preSpawnTime;
    private double hitWindow;

    private float targetScaleMultiplier = 1.0f;

    void Awake()
    {
        // 防止你忘了加/拖 CanvasGroup
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    public void Init(double noteTime, double spawnTime, double preSpawnTime, double hitWindow)
    {
        NoteTime = noteTime;
        this.spawnTime = spawnTime;
        this.preSpawnTime = preSpawnTime;
        this.hitWindow = hitWindow;

        IsJudged = false;
        canvasGroup.alpha = 1f;

        ApplyScales();
    }

    public void SetTargetScaleMultiplier(float m)
    {
        targetScaleMultiplier = m;
        ApplyScales();
    }

    void ApplyScales()
    {
        if (targetCircle == null || shrinkingRing == null) return;

        float finalTargetScale = baseTargetScale * targetScaleMultiplier;
        targetCircle.localScale = Vector3.one * finalTargetScale;
        shrinkingRing.localScale = Vector3.one * startRingScale;
    }

    public void SetAnchoredPosition(Vector2 anchoredPos)
    {
        // 你的 prefab 根必须是 RectTransform（UI）
        ((RectTransform)transform).anchoredPosition = anchoredPos;
    }

    void Update()
    {
        if (IsJudged) return;

        if (targetCircle == null || shrinkingRing == null) return; // 引用没拖会导致不缩

        double now = AudioSettings.dspTime;

        // shrink progress 0..1
        double t = (now - spawnTime) / preSpawnTime;
        float k = Mathf.Clamp01((float)t);

        float finalTargetScale = baseTargetScale * targetScaleMultiplier;
        float ringScale = Mathf.Lerp(startRingScale, finalTargetScale, k);
        shrinkingRing.localScale = Vector3.one * ringScale;

        // timeout miss
        if (now > NoteTime + hitWindow)
        {
            Miss();
        }
    }

    public void JudgeHit()
    {
        if (IsJudged) return;
        IsJudged = true;
        StartCoroutine(VanishAndDestroy());
    }

    void Miss()
    {
        if (IsJudged) return;
        IsJudged = true;
        OnMiss?.Invoke(this);
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
