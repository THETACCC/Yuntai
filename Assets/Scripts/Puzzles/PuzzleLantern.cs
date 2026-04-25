using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static AudioManager;
using URPLight2D = UnityEngine.Rendering.Universal.Light2D;

/// <summary>
/// 单个灯笼：正常挂灯/取灯的黑场 & 行为；
/// 另外提供 ForceSetHanged 和 SetGreen 给 3-3 Manager 控制恐怖演出用。
/// </summary>
public class PuzzleLantern : UI_E
{
    [Header("Lantern (assign an existing child)")]
    [SerializeField] private GameObject lantern;
    [SerializeField] private bool lanternHanged = false;

    [Header("Green Horror Light (per-lantern)")]
    [Tooltip("恐怖演出使用的绿色 Light2D（平时 disabled）。")]
    [SerializeField] private URPLight2D greenLight;

    [Header("Blackout Options")]
    [SerializeField] private bool forceBlackOverlay = true;
    [SerializeField, Min(0f)] private float blackoutHold = 3f;
    [SerializeField, Min(0f)] private float overlayFadeIn = 0.15f;
    [SerializeField, Min(0f)] private float overlayFadeOut = 0.2f;

    [Header("Freeze Player During Blackout")]
    [Tooltip("在普通挂灯黑场期间要禁用的组件（比如 PlayerController / PlayerInput）")]
    [SerializeField] private Behaviour[] movementComponents;

    [Header("Managers")]
    [SerializeField] private LevelManager3_3 levelManager3_3;

    private bool isRunning = false;

    public bool IsHanged => lanternHanged;

    // 灯光组件
    private readonly List<Light> stdLights = new();
    private readonly List<URPLight2D> urpLights = new();

    // 黑幕
    private const string OverlayName = "__BlackOverlay__";
    private CanvasGroup overlayCG;

    // Player
    private GameObject playerObj;
    private Rigidbody2D cachedRB2D;
    private bool rb2dHadSimulated = true;
    private RigidbodyConstraints2D rb2dOldConstraints;

    private void Awake()
    {
        RefreshAllLightComponents();
        if (forceBlackOverlay)
            overlayCG = GetOrCreateBlackOverlay();

        if (!levelManager3_3)
            levelManager3_3 = FindObjectOfType<LevelManager3_3>();

        // 通过 tag 找 Player
        playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj)
            cachedRB2D = playerObj.GetComponentInChildren<Rigidbody2D>();

        // 绿灯默认关掉
        if (greenLight)
            greenLight.enabled = false;
    }

    protected override void Start()
    {
        base.Start();

        if (!lantern) TryAutoFindLantern();
        if (lantern) lantern.SetActive(lanternHanged);
    }

    private void Update()
    {
        if (!isPlayerInTrigger || isRunning)
            return;

        if (Input.GetKeyDown(KeyCode.E))
        {
            if (!lantern)
            {
                Debug.LogWarning("[PuzzleLantern] Lantern not assigned and auto-find failed.", this);
                return;
            }
            //Audio
            AudioManager.Play("Sound Effects/sndLanternPlace", AudioGroup.SFX);
    


            StartCoroutine(ToggleLanternRoutine());
        }
    }

    private IEnumerator ToggleLanternRoutine()
    {
        isRunning = true;

        // —— 冻结玩家（小黑场） —— //
        FreezePlayer(true);

        // 1) 黑幕淡入
        if (forceBlackOverlay && overlayCG)
            yield return FadeOverlay(1f, overlayFadeIn);

        // 2) 关闭场上所有光源
        SetAllLightComponentsEnabled(false);

        // 3) 切换灯笼显隐
        lanternHanged = !lanternHanged;
        lantern.SetActive(lanternHanged);

        // 3.5) 通知 Manager 重算
        if (levelManager3_3)
            levelManager3_3.CheckIfLanternCorrect();

        // 4) 黑场停留
        if (blackoutHold > 0f)
            yield return new WaitForSeconds(blackoutHold);

        // 5) 打开光源
        SetAllLightComponentsEnabled(true);

        // 6) 黑幕淡出
        if (forceBlackOverlay && overlayCG)
            yield return FadeOverlay(0f, overlayFadeOut);

        // —— 解除冻结 —— //
        FreezePlayer(false);

        isRunning = false;
    }

    // ========= 提供给 LevelManager3_3 的接口 =========

    /// <summary>恐怖演出用：强制设置挂灯状态（不走本地黑场协程）。</summary>
    public void ForceSetHanged(bool hanged)
    {
        lanternHanged = hanged;
        if (lantern)
            lantern.SetActive(lanternHanged);
    }

    /// <summary>恐怖演出用：控制自己的绿色 Light2D。</summary>
    public void SetGreen(bool on)
    {
        if (!greenLight) return;

        // 确保 GameObject 本身是激活的
        greenLight.gameObject.SetActive(on);

        // 确保组件是 enabled 的
        greenLight.enabled = on;
    }


    // ========= Light 控制 =========
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
            if (stdLights[i]) stdLights[i].enabled = enabled;

        for (int i = 0; i < urpLights.Count; i++)
            if (urpLights[i]) urpLights[i].enabled = enabled;
    }

    private static bool IsSelfOrAncestorOrDescendant(Transform a, Transform self)
    {
        if (a == self) return true;
        if (a.IsChildOf(self)) return true;
        if (self.IsChildOf(a)) return true;
        return false;
    }

    // ========= 黑幕 =========
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

    // ========= 冻结玩家（普通挂灯用，小范围） =========
    private void FreezePlayer(bool freeze)
    {
        if (movementComponents != null)
        {
            for (int i = 0; i < movementComponents.Length; i++)
            {
                var comp = movementComponents[i];
                if (!comp) continue;
                comp.enabled = !freeze;
            }
        }

        if (cachedRB2D)
        {
            if (freeze)
            {
                rb2dHadSimulated = cachedRB2D.simulated;
                rb2dOldConstraints = cachedRB2D.constraints;

                cachedRB2D.velocity = Vector2.zero;
                cachedRB2D.angularVelocity = 0f;
                cachedRB2D.simulated = false;
            }
            else
            {
                cachedRB2D.simulated = rb2dHadSimulated;
                cachedRB2D.constraints = rb2dOldConstraints;
            }
        }
    }

    // ========= 其他 =========
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
