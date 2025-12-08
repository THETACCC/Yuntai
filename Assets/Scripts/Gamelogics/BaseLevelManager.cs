using System.Collections.Generic;
using UnityEngine;
using Cinemachine;

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
    [SerializeField] protected CinemachineVirtualCamera mainVCam;
    protected float zoomInOrthoSize = 6f;
    [SerializeField, Min(0f)] protected float zoomDuration = 0.5f;

    protected float _originalOrthoSize;
    protected bool _hasOriginalOrthoSize = false;
    protected Coroutine _camZoomRoutine;

    // ================= Awake =================

    protected virtual void Awake()
    {
        // 1) 锁 GamePhase（统一开场禁止随便乱走）
        if (Gamemanager.instance && lockPlayerOnSceneStart)
        {
            Gamemanager.instance.phase = GamePhase.Loading;
        }

        // 2) 用单例找 Player
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

    /// <summary>给 Fungus / Timeline 用：让玩家出现（enabled=true + alpha=1）</summary>
    public void RevealPlayerSprites()
    {
        if (!playerObject) return;

        CachePlayerSprites();
        foreach (var sr in _playerSprites)
        {
            if (!sr) continue;
            sr.enabled = true;
            var c = sr.color;
            c.a = 1f;             // ⭐ 关键：重新把 alpha 调回 1
            sr.color = c;
        }
    }

    /// <summary>给 Fungus / Timeline 用：再次隐藏玩家（enabled=false + alpha=0）</summary>
    public void HidePlayerSprites()
    {
        if (!playerObject) return;

        CachePlayerSprites();
        foreach (var sr in _playerSprites)
        {
            if (!sr) continue;
            sr.enabled = false;
            var c = sr.color;
            c.a = 0f;             // 对称：隐藏时顺便把 alpha 也清掉
            sr.color = c;
        }
    }

    // ===== 移动锁定相关 =====
    public void DisablePlayerMovement()
    {
        if (Gamemanager.instance)
            Gamemanager.instance.phase = GamePhase.Loading;

        if (playerCtrl != null)
            playerCtrl.DisablePlayerControl();
    }

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

    // ===== Camera Zoom 相关（原样保留） =====
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

    public void CameraZoomIn()
    {
        if (!EnsureMainVCam()) return;

        _originalOrthoSize = mainVCam.m_Lens.OrthographicSize;
        _hasOriginalOrthoSize = true;
        StartCameraZoom(zoomInOrthoSize);
    }

    public void CameraZoomOut()
    {
        if (!EnsureMainVCam()) return;

        if (!_hasOriginalOrthoSize)
        {
            _originalOrthoSize = mainVCam.m_Lens.OrthographicSize;
            _hasOriginalOrthoSize = true;
        }

        StartCameraZoom(_originalOrthoSize);
    }

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

    protected void StartCameraZoom(float targetSize)
    {
        if (_camZoomRoutine != null)
            StopCoroutine(_camZoomRoutine);

        _camZoomRoutine = StartCoroutine(CoCameraZoom(targetSize));
    }

    protected System.Collections.IEnumerator CoCameraZoom(float targetSize)
    {
        if (!mainVCam)
        {
            _camZoomRoutine = null;
            yield break;
        }

        float startSize = mainVCam.m_Lens.OrthographicSize;

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
            float k = Mathf.SmoothStep(0f, 1f, u);
            mainVCam.m_Lens.OrthographicSize = Mathf.Lerp(startSize, targetSize, k);
            yield return null;
        }

        mainVCam.m_Lens.OrthographicSize = targetSize;
        _camZoomRoutine = null;
    }
}
