using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class NoteViewClick : MonoBehaviour, IPointerClickHandler
{
    [Header("Prefab children (RectTransform)")]
    [SerializeField] private RectTransform targetCircle;
    [SerializeField] private RectTransform shrinkingRing;

    [Header("Feedback Images")]
    [SerializeField] private Image greenFeedback;
    [SerializeField] private Image redFeedback;

    [Header("Clickable Graphic (optional)")]
    [SerializeField] private Image clickCatcher;

    [Header("Auto-added if missing")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Scales (multipliers)")]
    [SerializeField] private float baseTargetScale = 1.0f;
    [SerializeField] private float startRingScale = 1.3f;

    [Header("Timing")]
    [SerializeField] private double holdAfterNoteTime = 0.10;
    [SerializeField] private double fadeDuration = 0.20;
    [SerializeField] private double feedbackHoldSeconds = 0.25;

    [Header("Debug")]
    [SerializeField] private bool debugLog = true;

    public double NoteTime { get; private set; }
    public bool IsJudged { get; private set; }

    public Action<NoteViewClick> OnMiss;
    public Action<NoteViewClick, double> OnClicked;

    private double spawnTime;
    private double preSpawnTime;
    private double hitWindow;

    private bool resolved;

    private double despawnTime;
    private double resolvedTimeDsp;

    private Vector3 targetBaseScale;
    private Vector3 ringBaseScale;

    public int DebugLane { get; private set; } = -1;
    public void SetDebugLane(int lane) => DebugLane = lane;

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

        if (clickCatcher == null) clickCatcher = GetComponent<Image>();
        if (clickCatcher != null) clickCatcher.raycastTarget = true;
    }

    public void Init(double noteTime, double spawnTime, double preSpawnTime, double hitWindow)
    {
        NoteTime = noteTime;
        this.spawnTime = spawnTime;
        this.preSpawnTime = preSpawnTime;
        this.hitWindow = hitWindow;

        resolved = false;
        IsJudged = false;
        resolvedTimeDsp = double.NaN;

        canvasGroup.alpha = 1f;
        HideFeedback();

        if (targetCircle) targetCircle.localScale = targetBaseScale * baseTargetScale;
        if (shrinkingRing) shrinkingRing.localScale = ringBaseScale * startRingScale;

        // 默认寿命：到 NoteTime 后再停留/淡出
        despawnTime = NoteTime + holdAfterNoteTime + fadeDuration;

        if (debugLog)
        {
            double now = AudioSettings.dspTime;
            Debug.Log($"[NoteViewClick] INIT lane={DebugLane} now={now:F3} note={NoteTime:F3} now-note={(now - NoteTime):F3}");
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (resolved) return;
        double now = AudioSettings.dspTime;

        if (debugLog)
            Debug.Log($"[NoteViewClick] CLICK lane={DebugLane} now={now:F3} note={NoteTime:F3} diff={(now - NoteTime):F3}");

        OnClicked?.Invoke(this, now);
    }

    // ✅ 由 Conductor 调用：当超过 NoteTime+hitWindow 且玩家没点中时，判 Miss
    public void ForceExpireAsMiss()
    {
        if (resolved) return;
        Resolve(false);
    }

    // ✅ 由 Conductor 调用：如果你想“到了时间但不算 miss，只是消失”
    public void ForceDespawn()
    {
        if (resolved) resolved = true; // stop further actions
        IsJudged = true;
        despawnTime = AudioSettings.dspTime + fadeDuration;
    }

    public void RegisterHit(double nowDsp)
    {
        if (resolved) return;

        double delta = Math.Abs(nowDsp - NoteTime);
        if (delta <= hitWindow)
        {
            Resolve(true);
        }
    }

    void Update()
    {
        double now = AudioSettings.dspTime;

        // shrink ring
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

        // fade out
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

        if (debugLog)
        {
            double now = resolvedTimeDsp;
            Debug.Log($"[NoteViewClick] RESOLVE {(hit ? "HIT" : "MISS")} lane={DebugLane} now={now:F3} note={NoteTime:F3} diff={(now - NoteTime):F3}");
        }

        if (hit) ShowGreen();
        else
        {
            ShowRed();
            OnMiss?.Invoke(this);
        }

        // 保证反馈可见再淡出
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
