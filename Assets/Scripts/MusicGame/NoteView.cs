using System;
using UnityEngine;
using UnityEngine.UI;

public class NoteView : MonoBehaviour
{
    [Header("Prefab children (RectTransform)")]
    [SerializeField] private RectTransform targetCircle;
    [SerializeField] private RectTransform shrinkingRing;

    [Header("Feedback Images")]
    [SerializeField] private Image greenFeedback;
    [SerializeField] private Image redFeedback;

    [Header("Auto-added if missing")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Scales (multipliers)")]
    [SerializeField] private float baseTargetScale = 1.0f; // 建议先从 1 开始
    [SerializeField] private float startRingScale = 1.0f;  // 建议先从 1 开始，再调大一点比如 1.3

    [Header("Timing")]
    [SerializeField] private double holdAfterNoteTime = 0.10;
    [SerializeField] private double fadeDuration = 0.20;
    [SerializeField] private double feedbackHoldSeconds = 0.25;

    public double NoteTime { get; private set; }
    public bool IsJudged { get; private set; }
    public Action<NoteView> OnMiss;

    private double spawnTime;
    private double preSpawnTime;
    private double hitWindow;

    private bool pressedInWindow;
    private bool resolved;

    private double despawnTime;
    private double resolvedTimeDsp;

    // ✅ prefab 初始 scale（关键）
    private Vector3 targetBaseScale;
    private Vector3 ringBaseScale;

    void Awake()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        if (greenFeedback) greenFeedback.raycastTarget = false;
        if (redFeedback) redFeedback.raycastTarget = false;

        if (targetCircle) targetBaseScale = targetCircle.localScale;
        if (shrinkingRing) ringBaseScale = shrinkingRing.localScale;
    }

    public void Init(double noteTime, double spawnTime, double preSpawnTime, double hitWindow)
    {
        NoteTime = noteTime;
        this.spawnTime = spawnTime;
        this.preSpawnTime = preSpawnTime;
        this.hitWindow = hitWindow;

        pressedInWindow = false;
        resolved = false;
        IsJudged = false;

        resolvedTimeDsp = double.NaN;

        canvasGroup.alpha = 1f;
        HideFeedback();

        // ✅ 用“初始 scale × 倍率”，不覆盖 prefab 的基准大小
        if (targetCircle) targetCircle.localScale = targetBaseScale * baseTargetScale;
        if (shrinkingRing) shrinkingRing.localScale = ringBaseScale * startRingScale;

        despawnTime = NoteTime + holdAfterNoteTime + fadeDuration;
    }

    public void SetAnchoredPosition(Vector2 anchoredPos)
    {
        ((RectTransform)transform).anchoredPosition = anchoredPos;
    }

    public void RegisterHit(double nowDsp)
    {
        if (resolved) return;

        double delta = Math.Abs(nowDsp - NoteTime);
        if (delta <= hitWindow)
        {
            pressedInWindow = true;
            if (nowDsp >= NoteTime) Resolve(true);
        }
    }

    public void ForceMiss()
    {
        if (resolved) return;
        Resolve(false);
    }

    void Update()
    {
        double now = AudioSettings.dspTime;

        if (shrinkingRing != null)
        {
            float targetMul = baseTargetScale;

            if (now < NoteTime)
            {
                double t = (now - spawnTime) / preSpawnTime;
                float k = Mathf.Clamp01((float)t);
                float ringMul = Mathf.Lerp(startRingScale, targetMul, k);

                shrinkingRing.localScale = ringBaseScale * ringMul;  // ✅
            }
            else
            {
                shrinkingRing.localScale = ringBaseScale * targetMul; // ✅
            }
        }

        if (!resolved)
        {
            if (now >= NoteTime && pressedInWindow) Resolve(true);
            else if (now > NoteTime + hitWindow) Resolve(false);
        }

        double fadeStart = despawnTime - fadeDuration;
        if (now >= fadeStart)
        {
            double k = (now - fadeStart) / fadeDuration;
            canvasGroup.alpha = 1f - Mathf.Clamp01((float)k);
        }

        if (now >= despawnTime)
            Destroy(gameObject);
    }

    void Resolve(bool hit)
    {
        resolved = true;
        IsJudged = true;
        resolvedTimeDsp = AudioSettings.dspTime;

        if (hit) ShowGreen();
        else
        {
            ShowRed();
            OnMiss?.Invoke(this);
        }

        double minDespawn = resolvedTimeDsp + feedbackHoldSeconds + fadeDuration;
        if (despawnTime < minDespawn) despawnTime = minDespawn;
    }

    void HideFeedback()
    {
        if (greenFeedback) greenFeedback.gameObject.SetActive(false);
        if (redFeedback) redFeedback.gameObject.SetActive(false);
    }

    void ShowGreen()
    {
        if (greenFeedback == null) return;
        greenFeedback.transform.SetAsLastSibling();
        greenFeedback.gameObject.SetActive(true);
        if (redFeedback) redFeedback.gameObject.SetActive(false);
    }

    void ShowRed()
    {
        if (redFeedback == null) return;
        redFeedback.transform.SetAsLastSibling();
        redFeedback.gameObject.SetActive(true);
        if (greenFeedback) greenFeedback.gameObject.SetActive(false);
    }
}
