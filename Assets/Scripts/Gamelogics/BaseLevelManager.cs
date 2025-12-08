using System.Collections.Generic;
using UnityEngine;
using Cinemachine;   // 相机缩放用 Cinemachine

/// <summary>
/// 所有关卡 Manager 的基类：统一处理 Player 的查找 / 隐藏 / 锁移动。
/// </summary>
public class BaseLevelManager : MonoBehaviour
{
    [Header("Player Scene Helper")]
    [Tooltip("进入场景时是否自动隐藏玩家 Sprite")]
    [SerializeField] protected bool hidePlayerOnSceneStart = false;

    [Tooltip("进入场景时是否锁住玩家控制（Gamemanager.phase=Loading）")]
    [SerializeField] protected bool lockPlayerOnSceneStart = false;

    // 统一缓存：给子类用
    protected GameObject playerObject;
    protected PlayerController playerCtrl;
    protected Rigidbody2D playerRb;
    protected readonly List<SpriteRenderer> _playerSprites = new();

    // ===== Camera Zoom Helper（通用相机缩放） =====
    [Header("Camera Zoom (Cinemachine)")]
    [Tooltip("本关主用的 CinemachineVirtualCamera；可不填，让脚本自动查场景里的第一个。")]
    [SerializeField] protected CinemachineVirtualCamera mainVCam;

    [Tooltip("Zoom In 时的目标 Orthographic Size（越小越“拉近”。建议设成比当前值小一点点，例如 11.9 → 8）")]
    protected float zoomInOrthoSize = 6f;

    [Tooltip("相机缩放动画时间（秒）")]
    [SerializeField, Min(0f)] protected float zoomDuration = 0.5f;

    /// <summary>本次缩放前记录的 Ortho Size，用于 ZoomOut 回到这个值。</summary>
    protected float _originalOrthoSize;
    protected bool _hasOriginalOrthoSize = false;

    /// <summary>相机缩放协程（避免多次叠加）。</summary>
    protected Coroutine _camZoomRoutine;

    /// <summary>
    /// 基类 Awake：自动查找 PlayerController.Instance，按设置隐藏 / 锁定。
    /// 子类如果重写 Awake，一定要记得 base.Awake().
    /// </summary>
    protected virtual void Awake()
    {
        // 1) 锁 GamePhase（统一开场禁止随便乱走）
        if (Gamemanager.instance && lockPlayerOnSceneStart)
        {
            Gamemanager.instance.phase = GamePhase.Loading;
        }

        // 2) 用单例找 Player（避免场景里有奇怪的重复 prefab 被误拿）
        playerCtrl = PlayerController.Instance;
        if (playerCtrl == null)
        {
            Debug.LogWarning("[BaseLevelManager] PlayerController.Instance is null, player not found.");
            // 不直接 return，后面还有相机初始化
        }
        else
        {
            playerObject = playerCtrl.gameObject;
            playerRb = playerCtrl.GetComponent<Rigidbody2D>();

            CachePlayerSprites();
            // ⭐ 每个场景开头都重置一次可见性，覆盖上一关的隐藏/透明状态
            ApplyInitialPlayerVisibility();

            if (lockPlayerOnSceneStart)
                playerCtrl.DisablePlayerControl();
        }

        // 3) 初始化主相机（用于 ZoomIn / ZoomOut）
        InitMainVCam();
    }

    /// <summary>重新找一遍 Player（比如某些特殊场景你想手动刷新）</summary>
    protected void RefreshPlayerReference(bool recacheSprites = true)
    {
        playerCtrl = PlayerController.Instance;
        if (playerCtrl == null)
        {
            Debug.LogWarning("[BaseLevelManager] RefreshPlayerReference: PlayerController.Instance is null.");
            return;
        }

        playerObject = playerCtrl.gameObject;
        playerRb = playerCtrl.GetComponent<Rigidbody2D>();

        if (recacheSprites)
            CachePlayerSprites();
    }

    // ===== Sprite 相关工具 =====
    protected void CachePlayerSprites()
    {
        _playerSprites.Clear();
        if (!playerObject) return;

        _playerSprites.AddRange(playerObject.GetComponentsInChildren<SpriteRenderer>(true));
    }

    protected void SetPlayerSpritesVisible(bool visible)
    {
        foreach (var sr in _playerSprites)
        {
            if (sr != null)
                sr.enabled = visible;
        }
    }

    /// <summary>
    /// 场景一开始根据 hidePlayerOnSceneStart 决定玩家的可见性：
    /// - hidePlayerOnSceneStart = true  → 全部 sprite 关闭，alpha=0
    /// - hidePlayerOnSceneStart = false → 全部 sprite 打开，alpha=1
    /// 用来“覆盖”上一场景对玩家 sprite 的各种乱改动。
    /// </summary>
    protected void ApplyInitialPlayerVisibility()
    {
        if (!playerObject) return;

        CachePlayerSprites();

        foreach (var sr in _playerSprites)
        {
            if (!sr) continue;

            var c = sr.color;

            if (hidePlayerOnSceneStart)
            {
                sr.enabled = false;
                c.a = 0f;
            }
            else
            {
                sr.enabled = true;
                c.a = 1f;
            }

            sr.color = c;
        }
    }

    /// <summary>给 Fungus / Timeline 用：让玩家出现</summary>
    public void RevealPlayerSprites()
    {
        SetPlayerSpritesVisible(true);
    }

    /// <summary>给 Fungus / Timeline 用：再次隐藏玩家</summary>
    public void HidePlayerSprites()
    {
        SetPlayerSpritesVisible(false);
    }

    // ===== 移动锁定相关 =====
    /// <summary>锁住玩家移动（控制+GamePhase 都锁）</summary>
    public void DisablePlayerMovement()
    {
        if (Gamemanager.instance)
            Gamemanager.instance.phase = GamePhase.Loading;

        if (playerCtrl != null)
            playerCtrl.DisablePlayerControl();
    }

    /// <summary>解锁玩家移动（GamePhase=Moving，控制打开）</summary>
    public void EnablePlayerMovement()
    {
        if (Gamemanager.instance)
            Gamemanager.instance.phase = GamePhase.Moving;

        if (playerCtrl != null)
            playerCtrl.EnablePlayerControl();
    }

    public void ShowPlayerAndAllowMove()
    {
        RevealPlayerSprites();
        EnablePlayerMovement();
    }

    // =========================================================
    // ===============   Camera Zoom 核心逻辑   ================
    // =========================================================

    /// <summary>
    /// 初始化主相机引用：若 Inspector 没手动指定，则自动找场景里的第一个 CinemachineVirtualCamera。
    /// </summary>
    protected void InitMainVCam()
    {
        if (!mainVCam)
        {
#if UNITY_2023_1_OR_NEWER
            mainVCam = FindFirstObjectByType<CinemachineVirtualCamera>();
#else
            mainVCam = FindObjectOfType<CinemachineVirtualCamera>();
#endif
        }

        if (!mainVCam)
        {
            Debug.LogWarning("[BaseLevelManager] 未找到 CinemachineVirtualCamera，相机缩放功能将不可用。");
            return;
        }
    }

    /// <summary>
    /// 给 Fungus / Timeline 直接调用的：相机 Zoom In。
    /// 会在“开始缩放之前”记录当前 Orthographic Size，方便之后 ZoomOut 回到这个值。
    /// </summary>
    public void CameraZoomIn()
    {
        if (!EnsureMainVCam()) return;

        // 在缩放前，记录当前的 size（例如 11.9）
        _originalOrthoSize = mainVCam.m_Lens.OrthographicSize;
        _hasOriginalOrthoSize = true;

        // 然后缩到 Inspector 里设定的 zoomInOrthoSize（例如 8）
        StartCameraZoom(zoomInOrthoSize);
    }

    /// <summary>
    /// 给 Fungus / Timeline 直接调用的：相机 Zoom Out。
    /// 回到“本次 ZoomIn 开始时”记录下来的 Orthographic Size。
    /// </summary>
    public void CameraZoomOut()
    {
        if (!EnsureMainVCam()) return;

        // 如果还没记录过，就以当前值作为“要回去的值”
        if (!_hasOriginalOrthoSize)
        {
            _originalOrthoSize = mainVCam.m_Lens.OrthographicSize;
            _hasOriginalOrthoSize = true;
        }

        // 回到刚才记录的 size（例如从 8 回到 11.9）
        StartCameraZoom(_originalOrthoSize);
    }

    /// <summary>
    /// 如果相机还没初始化，尝试初始化一下。
    /// </summary>
    protected bool EnsureMainVCam()
    {
        if (!mainVCam)
            InitMainVCam();

        if (!mainVCam)
        {
            Debug.LogWarning("[BaseLevelManager] CameraZoom 调用失败：没有可用的 CinemachineVirtualCamera。");
            return false;
        }
        return true;
    }

    /// <summary>
    /// 启动一次新的缩放协程（会自动停止上一次的）。
    /// </summary>
    protected void StartCameraZoom(float targetSize)
    {
        if (_camZoomRoutine != null)
            StopCoroutine(_camZoomRoutine);

        _camZoomRoutine = StartCoroutine(CoCameraZoom(targetSize));
    }

    /// <summary>
    /// 实际的相机缩放协程：在 zoomDuration 内平滑插值到 targetSize。
    /// </summary>
    protected System.Collections.IEnumerator CoCameraZoom(float targetSize)
    {
        if (!mainVCam)
        {
            _camZoomRoutine = null;
            yield break;
        }

        float startSize = mainVCam.m_Lens.OrthographicSize;

        // 缩放时间为 0 或负数时，直接瞬移
        if (zoomDuration <= 0f)
        {
            mainVCam.m_Lens.OrthographicSize = targetSize;
            _camZoomRoutine = null;
            yield break;
        }

        float t = 0f;
        while (t < zoomDuration)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / zoomDuration);
            // 用 SmoothStep 稍微柔一点
            float k = Mathf.SmoothStep(0f, 1f, u);
            mainVCam.m_Lens.OrthographicSize = Mathf.Lerp(startSize, targetSize, k);
            yield return null;
        }

        mainVCam.m_Lens.OrthographicSize = targetSize;
        _camZoomRoutine = null;
    }
}
