using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using URPLight2D = UnityEngine.Rendering.Universal.Light2D;

public class PuzzleLantern : UI_E
{
    [Header("Lantern (assign an existing child)")]
    [SerializeField] private GameObject lantern;            // 直接拖入子物体
    [SerializeField] private bool lanternHanged = false;    // 初始状态

    [Header("Blackout Options")]
    [SerializeField] private bool forceBlackOverlay = true; // 黑幕兜底
    [SerializeField, Min(0f)] private float blackoutHold = 3f;   // 黑场停顿（你要的3秒）
    [SerializeField, Min(0f)] private float overlayFadeIn = 0.15f;
    [SerializeField, Min(0f)] private float overlayFadeOut = 0.2f;

    private bool isRunning = false;

    // 直接控制光源组件，而不是关整对象
    private readonly List<Light> stdLights = new();
    private readonly List<URPLight2D> urpLights = new();

    // 黑幕
    private const string OverlayName = "__BlackOverlay__";
    private CanvasGroup overlayCG;

    private void Awake()
    {
        RefreshAllLightComponents();
        if (forceBlackOverlay) overlayCG = GetOrCreateBlackOverlay();
    }

    protected override void Start()
    {
        base.Start();
        if (!lantern) TryAutoFindLantern();
        if (lantern) lantern.SetActive(lanternHanged);
    }

    // ✅ 改为在 Update 里读键，配合 isPlayerInTrigger 更稳定
    private void Update()
    {
        if (!isPlayerInTrigger) return;   // 这个来自 UI_E（protected）
        if (isRunning) return;

        if (Input.GetKeyDown(KeyCode.E))
        {
            if (!lantern)
            {
                Debug.LogWarning("[PuzzleLantern] Lantern not assigned and auto-find failed.");
                return;
            }
            StartCoroutine(ToggleLanternRoutine());
        }
    }

    private IEnumerator ToggleLanternRoutine()
    {
        isRunning = true;

        // 1) 先拉黑幕（更快反馈），再关光源（避免Unlit漏光）
        if (forceBlackOverlay && overlayCG)
            yield return FadeOverlay(1f, overlayFadeIn);

        SetAllLightComponentsEnabled(false);

        // 2) 切换灯笼显隐
        lanternHanged = !lanternHanged;
        lantern.SetActive(lanternHanged);

        // 3) 保持黑场（你要的3秒，可在 Inspector 改 blackoutHold）
        if (blackoutHold > 0f)
            yield return new WaitForSeconds(blackoutHold);

        // 4) 开灯 + 收黑幕
        SetAllLightComponentsEnabled(true);

        if (forceBlackOverlay && overlayCG)
            yield return FadeOverlay(0f, overlayFadeOut);

        isRunning = false;
    }

    // —— Light 控制 —— //
    private void RefreshAllLightComponents()
    {
        stdLights.Clear();
        urpLights.Clear();

        var allStd = Object.FindObjectsOfType<Light>(true);
        var allUrp = Object.FindObjectsOfType<URPLight2D>(true);

        foreach (var l in allStd)
            if (l && !IsSelfOrAncestorOrDescendant(l.transform, transform))
                stdLights.Add(l);

        foreach (var l2 in allUrp)
            if (l2 && !IsSelfOrAncestorOrDescendant(l2.transform, transform))
                urpLights.Add(l2);
    }

    private void SetAllLightComponentsEnabled(bool enabled)
    {
        if (stdLights.Count == 0 && urpLights.Count == 0)
            RefreshAllLightComponents();

        for (int i = 0; i < stdLights.Count; i++)
        {
            var l = stdLights[i];
            if (l) l.enabled = enabled;
        }

        for (int i = 0; i < urpLights.Count; i++)
        {
            var l2 = urpLights[i];
            if (l2) l2.enabled = enabled;
        }
    }

    private static bool IsSelfOrAncestorOrDescendant(Transform a, Transform self)
    {
        if (a == self) return true;
        if (a.IsChildOf(self)) return true;
        if (self.IsChildOf(a)) return true;
        return false;
    }

    // —— 黑幕 —— //
    private CanvasGroup GetOrCreateBlackOverlay()
    {
        var exist = GameObject.Find(OverlayName);
        if (exist)
        {
            var cg = exist.GetComponent<CanvasGroup>();
            if (cg) return cg;
        }

        var go = new GameObject(OverlayName, typeof(Canvas), typeof(CanvasGroup));
        var canvas = go.GetComponent<Canvas>();
        var cgNew = go.GetComponent<CanvasGroup>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;
        cgNew.alpha = 0f;
        cgNew.blocksRaycasts = false;
        cgNew.interactable = false;

        var imgGO = new GameObject("Black", typeof(RectTransform), typeof(Image));
        imgGO.transform.SetParent(go.transform, false);
        var rt = imgGO.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var img = imgGO.GetComponent<Image>();
        img.color = Color.black;
        img.raycastTarget = false;

        return cgNew;
    }

    private IEnumerator FadeOverlay(float target, float dur)
    {
        if (!overlayCG) yield break;
        float start = overlayCG.alpha;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            overlayCG.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(t / dur));
            yield return null;
        }
        overlayCG.alpha = target;
    }

    // —— 其他 —— //
    private void TryAutoFindLantern()
    {
        var t = transform.Find("Lantern");
        if (t) lantern = t.gameObject;
    }

    private void Reset()
    {
        if (!lantern) TryAutoFindLantern();
    }
}
