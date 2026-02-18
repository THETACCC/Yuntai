using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using URPLight2D = UnityEngine.Rendering.Universal.Light2D;

public class LevelManager3_3 : BaseLevelManager
{
    [Header("Lantern Stall")]
    [SerializeField] private GameObject LanternStall;
    [SerializeField] private GameObject LanternStall_wait;
    [SerializeField] private GameObject LanternStall_wrong;
    [SerializeField] private GameObject LanternStall_correct;

    [SerializeField] private GameObject LanternPile;
    [SerializeField] private GameObject Lanterns;

    [Header("Puzzle")]
    [SerializeField] private PuzzleLanternManager puzzleLanternManager;
    [SerializeField] private Collider2D puzzleLanternCollider;
    [SerializeField] private Collider2D puzzleLanternColliderChild;

    [Header("Horror Lantern Sequence")]
    [Tooltip("是否需要播放恐怖花灯动画（摊主对话里错误时设为 true）。")]
    public bool NeedToPlayLanternAnim = false;

    [Tooltip("恐怖演出时短暂出现的巨兽幽影（可选）。")]
    [SerializeField] private GameObject beastShadow;

    [Header("Horror Timing")]
    [SerializeField, Min(1)] private int greenBlinkCount = 3;
    [SerializeField, Min(0f)] private float greenOnTime = 0.25f;
    [SerializeField, Min(0f)] private float greenOffTime = 0.15f;
    [SerializeField, Min(0f)] private float shakeDuration = 3f;
    [SerializeField, Min(0f)] private float blackFadeTime = 0.5f;
    [SerializeField, Min(0f)] private float blackHoldTime = 1.5f;

    [Header("Audio")]
    [SerializeField] private AudioSource horrorAudioSource;
    [SerializeField] private AudioClip horrorRoarClip;

    [Header("Dialogue After Horror")]
    [SerializeField] private DialogueTrigger MC_scaredDialogue;

    [Header("Doors")]
    [SerializeField] private GameObject door;

    // Camera shake
    private CameraShake cameraShake;

    // 黑幕
    private const string OverlayName = "__BlackOverlay__";
    private CanvasGroup overlayCG;

    // 状态
    private bool isPlayingLanternHorror = false;

    // 自动收集的 PuzzleLantern 列表
    private PuzzleLantern[] puzzleLanterns = new PuzzleLantern[0];

    // ==== Player 物理 & 动画（基于 BaseLevelManager 的缓存）====
    private bool rb2dHadSimulated;
    private RigidbodyConstraints2D rb2dOldConstraints;

    private Animator[] playerAnimators;
    private float[] animatorOrigSpeeds;

    // ===== 生命周期 =====
    protected override void Awake()
    {
        // 先让 BaseLevelManager 做：找 PlayerController.Instance，按需要隐藏/锁控制
        base.Awake();

        if (!puzzleLanternManager)
            puzzleLanternManager = FindObjectOfType<PuzzleLanternManager>();

        RefreshPuzzleLanternList();

        cameraShake = FindObjectOfType<CameraShake>();

        // Player 相关：用 Base 里的 playerObject / playerRb / playerCtrl
        if (playerObject)
        {
            if (playerRb != null)
            {
                rb2dHadSimulated = playerRb.simulated;
                rb2dOldConstraints = playerRb.constraints;
            }

            playerAnimators = playerObject.GetComponentsInChildren<Animator>(true);
            if (playerAnimators != null && playerAnimators.Length > 0)
            {
                animatorOrigSpeeds = new float[playerAnimators.Length];
                for (int i = 0; i < playerAnimators.Length; i++)
                    animatorOrigSpeeds[i] = playerAnimators[i].speed;
            }
        }
        else
        {
            Debug.LogWarning("[LevelManager3_3] Awake: PlayerController.Instance / Player 未找到。");
        }

        overlayCG = GetOrCreateBlackOverlay();

        Debug.Log($"[LevelManager3_3] Awake: found {puzzleLanterns.Length} PuzzleLantern(s).");
    }

    private void Start()
    {
        // 3-3 一进来，确保玩家可见 + 可以移动
        ShowPlayerAndAllowMove();

        if (playerObject != null)
        {
            CachePlayerSprites();
            foreach (var sr in _playerSprites)
            {
                if (!sr) continue;
                sr.enabled = true;
                var c = sr.color;
                c.a = 1f;
                sr.color = c;
            }
        }
    }

    // ========= 普通 puzzle =========

    public void ChangeLanternToWait()
    {
        LanternStall.SetActive(false);
        LanternStall_wait.SetActive(true);
        LanternStall_wrong.SetActive(false);
        LanternStall_correct.SetActive(false);
    }

    public void CheckIfLanternCorrect()
    {
        if (!puzzleLanternManager)
        {
            Debug.LogError("[LevelManager3_3] puzzleLanternManager is NULL, cannot check.", this);
            return;
        }

        puzzleLanternManager.ForceRecheck();
        Debug.Log($"[LevelManager3_3] CheckIfLanternCorrect, Solved = {puzzleLanternManager.Solved}");

        if (puzzleLanternManager.Solved)
        {
            LanternStall.SetActive(false);
            LanternStall_wait.SetActive(false);
            LanternStall_wrong.SetActive(false);
            LanternStall_correct.SetActive(true);
            /*
            if (puzzleLanternCollider)
                puzzleLanternCollider.enabled = false;
            */
        }
        else
        {
            LanternStall.SetActive(false);
            LanternStall_wait.SetActive(false);
            LanternStall_correct.SetActive(false);
            LanternStall_wrong.SetActive(true);
        }
    }

    public void lanternBlockColliderDisappear()
    {
        puzzleLanternCollider.enabled = false;
        puzzleLanternColliderChild.enabled = false;
    }
    public void LanternPileDisappear()
    {
        LanternPile.SetActive(false);
        Lanterns.SetActive(true);
        ChangeLanternToWait();
    }

    // ========= NeedToPlayLanternAnim =========

    public void MarkNeedToPlayLanternAnim()
    {
        NeedToPlayLanternAnim = true;
    }

    public void ClearNeedToPlayLanternAnim()
    {
        NeedToPlayLanternAnim = false;
    }

    // ========= 收集 / 控制 Lantern =========

    private void RefreshPuzzleLanternList()
    {
        if (puzzleLanternManager)
        {
            puzzleLanterns = puzzleLanternManager.GetComponentsInChildren<PuzzleLantern>(true);
        }
        else
        {
            puzzleLanterns = FindObjectsOfType<PuzzleLantern>(true);
        }
    }

    private void SetAllLanternsGreen(bool on)
    {
        if (puzzleLanterns == null || puzzleLanterns.Length == 0)
            RefreshPuzzleLanternList();

        if (puzzleLanterns == null) return;

        foreach (var p in puzzleLanterns)
        {
            if (!p) continue;
            p.SetGreen(on);
        }

        Debug.Log($"[LevelManager3_3] SetAllLanternsGreen({on}) on {puzzleLanterns.Length} lanterns.");
    }

    private void ForceAllLanternsHanged(bool hanged)
    {
        if (puzzleLanterns == null || puzzleLanterns.Length == 0)
            RefreshPuzzleLanternList();

        if (puzzleLanterns == null) return;

        foreach (var p in puzzleLanterns)
        {
            if (!p) continue;
            p.ForceSetHanged(hanged);
        }

        Debug.Log($"[LevelManager3_3] ForceAllLanternsHanged({hanged}) on {puzzleLanterns.Length} lanterns.");
    }

    // ========= 对外接口：恐怖演出 =========

    public void PlayLanternHorrorSequence()
    {
        if (isPlayingLanternHorror) return;
        if (!NeedToPlayLanternAnim) return;

        StartCoroutine(CoLanternHorrorSequence());
    }

    private IEnumerator CoLanternHorrorSequence()
    {
        isPlayingLanternHorror = true;
        ClearNeedToPlayLanternAnim();

        Debug.Log("[LevelManager3_3] >>> START Lantern Horror Sequence <<<");

        // 防止中途有变化
        RefreshPuzzleLanternList();

        // —— 完全锁 Player（位置 + 输入 + 动画） —— //
        FreezePlayer(true);

        // 1) 所有灯像「挂上」那样亮起来
        if (Lanterns) Lanterns.SetActive(true);
        ForceAllLanternsHanged(true);
        SetAllLanternsGreen(false);   // 先关绿光，再开始闪

        // 2) 绿光闪烁几次，最后一次加野兽幽影
        for (int i = 0; i < greenBlinkCount; i++)
        {
            SetAllLanternsGreen(true);

            if (beastShadow && i == greenBlinkCount - 1)
                beastShadow.SetActive(true);

            yield return new WaitForSeconds(greenOnTime);

            if (beastShadow)
                beastShadow.SetActive(false);

            SetAllLanternsGreen(false);
            yield return new WaitForSeconds(greenOffTime);
        }

        // 3) 抖屏 + 音效
        if (cameraShake)
        {
            cameraShake.Shake(cameraShake.defaultAmplitude,
                              cameraShake.defaultFrequency,
                              shakeDuration);
        }
        if (horrorAudioSource && horrorRoarClip)
        {
            horrorAudioSource.PlayOneShot(horrorRoarClip);
        }

        yield return new WaitForSeconds(shakeDuration);

        // 4) 黑屏
        if (overlayCG)
        {
            // 黑屏淡入
            yield return FadeOverlay(1f, blackFadeTime);

            // 黑屏时，把所有灯「取下」
            ForceAllLanternsHanged(false);

            // 黑屏停留
            yield return new WaitForSeconds(blackHoldTime);

            // 黑屏淡出
            yield return FadeOverlay(0f, blackFadeTime);
        }
        else
        {
            // 没黑幕的话，就直接让灯消失
            ForceAllLanternsHanged(false);
        }

        // —— 解锁 Player —— //
        FreezePlayer(false);

        if (MC_scaredDialogue != null)
        {
            MC_scaredDialogue.TriggerDialogue();
        }

        isPlayingLanternHorror = false;
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

    public void ChangeScenePortal()
    {

    }

    // ========= Player 冻结 =========

    private void FreezePlayer(bool freeze)
    {
        if (!playerObject || playerCtrl == null) return;

        if (freeze)
        {
            // 锁移动（会把 Gamemanager.phase 设为 Loading，并关掉走路动画 & 脚步声）
            playerCtrl.DisablePlayerControl();

            if (playerRb != null)
            {
                rb2dHadSimulated = playerRb.simulated;
                rb2dOldConstraints = playerRb.constraints;

                playerRb.velocity = Vector2.zero;
                playerRb.angularVelocity = 0f;
                playerRb.simulated = false;
            }

            // 暂停所有 Animator
            if (playerAnimators != null && playerAnimators.Length > 0)
            {
                if (animatorOrigSpeeds == null || animatorOrigSpeeds.Length != playerAnimators.Length)
                    animatorOrigSpeeds = new float[playerAnimators.Length];

                for (int i = 0; i < playerAnimators.Length; i++)
                {
                    var anim = playerAnimators[i];
                    if (!anim) continue;
                    animatorOrigSpeeds[i] = anim.speed;
                    anim.speed = 0f;
                }
            }

            if (Gamemanager.instance)
                Gamemanager.instance.phase = GamePhase.Eventing;
        }
        else
        {
            // 恢复物理
            if (playerRb != null)
            {
                playerRb.simulated = rb2dHadSimulated;
                playerRb.constraints = rb2dOldConstraints;
            }

            // 恢复 Animator 速度
            if (playerAnimators != null && playerAnimators.Length > 0)
            {
                for (int i = 0; i < playerAnimators.Length; i++)
                {
                    var anim = playerAnimators[i];
                    if (!anim) continue;

                    float orig = (animatorOrigSpeeds != null && i < animatorOrigSpeeds.Length)
                                 ? animatorOrigSpeeds[i]
                                 : 1f;
                    anim.speed = orig;
                }
            }

            // 解锁移动
            playerCtrl.EnablePlayerControl();
            if (Gamemanager.instance)
                Gamemanager.instance.phase = GamePhase.Moving;
        }
    }

    public void CameraZoomIn_3_3()
    {
        CameraZoomIn();
    }
    public void CameraZoomOut_3_3()
    {
        CameraZoomOut();
    }
}
