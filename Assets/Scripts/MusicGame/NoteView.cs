using System;
using UnityEngine;
using UnityEngine.UI;

public class NoteView : MonoBehaviour
{
    [Header("Prefab children (RectTransform)")]
    [SerializeField] private RectTransform targetCircle;
    [SerializeField] private RectTransform shrinkingRing;

    [Header("Feedback Images")]
    [Tooltip("Used for Good (and also Perfect fallback if perfectFeedback is not assigned).")]
    [SerializeField] private Image greenFeedback;

    [Tooltip("Optional. If assigned, used for Perfect.")]
    [SerializeField] private Image perfectFeedback;

    [Tooltip("Used for Miss.")]
    [SerializeField] private Image redFeedback;

    [Header("Auto-added if missing")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Scales (multipliers)")]
    [SerializeField] private float baseTargetScale = 1.0f;
    [SerializeField] private float startRingScale = 1.0f;

    [Header("Timing")]
    [SerializeField] private double holdAfterNoteTime = 0.10;
    [SerializeField] private double fadeDuration = 0.20;
    [SerializeField] private double feedbackHoldSeconds = 0.25;

    public double NoteTime { get; private set; }
    public bool IsJudged { get; private set; }

    public Action<NoteView> OnMiss;
    public Action<NoteView, HitJudgement> OnJudged;

    private double spawnTime;
    private double preSpawnTime;
    private double hitWindow;

    private bool pressedInWindow;
    private bool resolved;

    private HitJudgement pendingJudgement = HitJudgement.Good;

    private double despawnTime;
    private double resolvedTimeDsp;

    private Vector3 targetBaseScale;
    private Vector3 ringBaseScale;

    void Awake()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        // 这些反馈图片不要挡 raycast
        if (greenFeedback) greenFeedback.raycastTarget = false;
        if (perfectFeedback) perfectFeedback.raycastTarget = false;
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

        pendingJudgement = HitJudgement.Good;
        resolvedTimeDsp = double.NaN;

        canvasGroup.alpha = 1f;
        HideFeedback();

        if (targetCircle) targetCircle.localScale = targetBaseScale * baseTargetScale;
        if (shrinkingRing) shrinkingRing.localScale = ringBaseScale * startRingScale;

        despawnTime = NoteTime + holdAfterNoteTime + fadeDuration;
    }

    public void SetAnchoredPosition(Vector2 anchoredPos)
    {
        ((RectTransform)transform).anchoredPosition = anchoredPos;
    }

    public void RegisterHit(double nowDsp, HitJudgement judgement)
    {
        if (resolved) return;

        // 安全检查：必须在 Good window 里
        double delta = Math.Abs(nowDsp - NoteTime);
        if (delta > hitWindow) return;

        pressedInWindow = true;
        pendingJudgement = judgement;

        // 如果已经到/过拍点，立刻结算；否则等 Update 到 NoteTime 再结算
        if (nowDsp >= NoteTime) Resolve(judgement);
    }

    public void ForceMiss()
    {
        if (resolved) return;
        Resolve(HitJudgement.Miss);
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

                shrinkingRing.localScale = ringBaseScale * ringMul;
            }
            else
            {
                shrinkingRing.localScale = ringBaseScale * targetMul;
            }
        }

        if (!resolved)
        {
            if (now >= NoteTime && pressedInWindow) Resolve(pendingJudgement);
            else if (now > NoteTime + hitWindow) Resolve(HitJudgement.Miss);
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

    void Resolve(HitJudgement judgement)
    {
        resolved = true;
        IsJudged = true;
        resolvedTimeDsp = AudioSettings.dspTime;

        HideFeedback();

        if (judgement == HitJudgement.Perfect)
        {
            ShowPerfect();
        }
        else if (judgement == HitJudgement.Good)
        {
            ShowGood();
        }
        else
        {
            ShowMiss();
            OnMiss?.Invoke(this);
        }

        OnJudged?.Invoke(this, judgement);

        double minDespawn = resolvedTimeDsp + feedbackHoldSeconds + fadeDuration;
        if (despawnTime < minDespawn) despawnTime = minDespawn;
    }

    void HideFeedback()
    {
        if (greenFeedback) greenFeedback.gameObject.SetActive(false);
        if (perfectFeedback) perfectFeedback.gameObject.SetActive(false);
        if (redFeedback) redFeedback.gameObject.SetActive(false);
    }

    void ShowPerfect()
    {
        // 如果没配 perfectFeedback，就用 greenFeedback 顶一下（只是视觉上不区分，但逻辑上区分）
        if (perfectFeedback != null)
        {
            perfectFeedback.transform.SetAsLastSibling();
            perfectFeedback.gameObject.SetActive(true);
        }
        else
        {
            ShowGood();
        }
    }

    void ShowGood()
    {
        if (greenFeedback == null) return;
        greenFeedback.transform.SetAsLastSibling();
        greenFeedback.gameObject.SetActive(true);
    }

    void ShowMiss()
    {
        if (redFeedback == null) return;
        redFeedback.transform.SetAsLastSibling();
        redFeedback.gameObject.SetActive(true);
    }
}