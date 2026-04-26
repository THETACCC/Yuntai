using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using static AudioManager;

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

    [Header("Feedback Pop Animation")]
    [SerializeField] private float feedbackStartScale = 1.0f;
    [SerializeField] private float feedbackPeakScale = 1.18f;
    [SerializeField] private float feedbackEndScale = 1.0f;
    [SerializeField] private float feedbackPopDuration = 0.12f;

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

    private Coroutine feedbackAnimCoroutine;

    void Awake()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

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

        if (targetCircle)
        {
            targetCircle.gameObject.SetActive(true);
            targetCircle.localScale = targetBaseScale * baseTargetScale;
        }

        if (shrinkingRing)
        {
            shrinkingRing.gameObject.SetActive(true);
            shrinkingRing.localScale = ringBaseScale * startRingScale;
        }

        ResetFeedbackVisual(greenFeedback);
        ResetFeedbackVisual(perfectFeedback);
        ResetFeedbackVisual(redFeedback);

        despawnTime = NoteTime + holdAfterNoteTime + fadeDuration;
    }

    public void SetAnchoredPosition(Vector2 anchoredPos)
    {
        ((RectTransform)transform).anchoredPosition = anchoredPos;
    }

    public void RegisterHit(double nowDsp, HitJudgement judgement)
    {
        if (resolved) return;

        double delta = Math.Abs(nowDsp - NoteTime);
        if (delta > hitWindow) return;

        //Audio
        //AudioManager.Play("Sound Effects/sndDrumHit", AudioGroup.SFX);

        pressedInWindow = true;
        pendingJudgement = judgement;

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

        if (!resolved && shrinkingRing != null)
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

        if (!resolved)
        {
            double fadeStart = despawnTime - fadeDuration;
            if (now >= fadeStart)
            {
                double k = (now - fadeStart) / fadeDuration;
                canvasGroup.alpha = 1f - Mathf.Clamp01((float)k);
            }
        }
        else
        {
            canvasGroup.alpha = 1f;
        }

        if (now >= despawnTime)
            Destroy(gameObject);
    }

    void Resolve(HitJudgement judgement)
    {
        resolved = true;
        IsJudged = true;
        resolvedTimeDsp = AudioSettings.dspTime;

        HideNoteVisuals();
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

    void HideNoteVisuals()
    {
        if (targetCircle) targetCircle.gameObject.SetActive(false);
        if (shrinkingRing) shrinkingRing.gameObject.SetActive(false);
    }

    void HideFeedback()
    {
        if (greenFeedback) greenFeedback.gameObject.SetActive(false);
        if (perfectFeedback) perfectFeedback.gameObject.SetActive(false);
        if (redFeedback) redFeedback.gameObject.SetActive(false);
    }

    void ResetFeedbackVisual(Image img)
    {
        if (img == null) return;

        img.gameObject.SetActive(false);
        img.transform.localScale = Vector3.one * feedbackStartScale;

        Color c = img.color;
        c.a = 1f;
        img.color = c;
    }

    void ShowPerfect()
    {
        if (perfectFeedback != null)
        {
            ShowFeedbackWithPop(perfectFeedback);
        }
        else
        {
            ShowGood();
        }
    }

    void ShowGood()
    {
        if (greenFeedback == null) return;
        ShowFeedbackWithPop(greenFeedback);
    }

    void ShowMiss()
    {
        if (redFeedback == null) return;
        ShowFeedbackWithPop(redFeedback);
    }

    void ShowFeedbackWithPop(Image img)
    {
        if (img == null) return;

        img.transform.SetAsLastSibling();
        img.gameObject.SetActive(true);

        Color c = img.color;
        c.a = 1f; // 确保100%不透明
        img.color = c;

        img.transform.localScale = Vector3.one * feedbackStartScale;

        if (feedbackAnimCoroutine != null)
            StopCoroutine(feedbackAnimCoroutine);

        feedbackAnimCoroutine = StartCoroutine(PlayFeedbackPop(img.transform));
    }

    IEnumerator PlayFeedbackPop(Transform target)
    {
        if (target == null) yield break;

        float half = feedbackPopDuration * 0.5f;
        float timer = 0f;

        while (timer < half)
        {
            timer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(timer / half);
            target.localScale = Vector3.Lerp(
                Vector3.one * feedbackStartScale,
                Vector3.one * feedbackPeakScale,
                t
            );
            yield return null;
        }

        timer = 0f;
        while (timer < half)
        {
            timer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(timer / half);
            target.localScale = Vector3.Lerp(
                Vector3.one * feedbackPeakScale,
                Vector3.one * feedbackEndScale,
                t
            );
            yield return null;
        }

        target.localScale = Vector3.one * feedbackEndScale;
        feedbackAnimCoroutine = null;
    }
}