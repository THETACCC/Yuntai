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

    [Header("Scales")]
    [SerializeField] private float baseTargetScale = 1.48f;
    [SerializeField] private float startRingScale = 2.56f;

    [Header("Timing")]
    [SerializeField] private double holdAfterNoteTime = 0.10;     // NoteTime 到达后停留
    [SerializeField] private double fadeDuration = 0.20;          // 淡出时长
    [SerializeField] private double feedbackHoldSeconds = 0.25;   // ★ 变红/绿后最少显示多久（保证看得见）

    public double NoteTime { get; private set; }
    public bool IsJudged { get; private set; }
    public Action<NoteView> OnMiss;

    private double spawnTime;
    private double preSpawnTime;
    private double hitWindow;

    private bool pressedInWindow;
    private bool resolved;

    // 生命周期控制
    private double despawnTime;        // 最终销毁时间
    private double resolvedTimeDsp;    // 变红/绿那一刻

    void Awake()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        if (greenFeedback) greenFeedback.raycastTarget = false;
        if (redFeedback) redFeedback.raycastTarget = false;
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

        if (targetCircle) targetCircle.localScale = Vector3.one * baseTargetScale;
        if (shrinkingRing) shrinkingRing.localScale = Vector3.one * startRingScale;

        // 先按“正常固定生命周期”算一个初始销毁时间（后面 Resolve 后会再延长保证反馈可见）
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

        // shrink
        if (shrinkingRing != null)
        {
            float targetScale = baseTargetScale;

            if (now < NoteTime)
            {
                double t = (now - spawnTime) / preSpawnTime;
                float k = Mathf.Clamp01((float)t);
                float ringScale = Mathf.Lerp(startRingScale, targetScale, k);
                shrinkingRing.localScale = Vector3.one * ringScale;
            }
            else
            {
                shrinkingRing.localScale = Vector3.one * targetScale;
            }
        }

        // auto resolve miss after window
        if (!resolved)
        {
            if (now >= NoteTime && pressedInWindow) Resolve(true);
            else if (now > NoteTime + hitWindow) Resolve(false);
        }

        // fade starts at despawnTime - fadeDuration
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

        // ★ 保证反馈至少显示 feedbackHoldSeconds，再开始淡出
        // 也就是：despawnTime >= resolvedTime + feedbackHold + fadeDuration
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
