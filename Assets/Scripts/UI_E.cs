using System.Collections;
using UnityEngine;
using Cinemachine;

public class UI_E : MonoBehaviour
{
    [Header("Interaction UI")]
    [SerializeField] protected GameObject InteractIndicator;

    // ===== 可选：按 E 时相机缩放 =====
    [Header("Camera Zoom On E (Optional)")]
    [Tooltip("默认 false；勾上后，玩家在此 Trigger 里按 E 会触发相机 Zoom In。")]
    [SerializeField] private bool enableZoomOnE = false;

    [Tooltip("要缩放的虚拟相机（可留空，自动 FindObjectOfType）。")]
    [SerializeField] private CinemachineVirtualCamera zoomVCam;

    [Tooltip("按 E 时的目标 Orthographic Size。")]
    [SerializeField] private float zoomInOrthoSize = 5f;

    [Tooltip("缩放时间（秒）。")]
    [SerializeField, Min(0f)] private float zoomDuration = 0.5f;

    [Header("Zoom Out 行为")]
    [Tooltip("true = 离开 Trigger 自动 zoom out；false = 只通过外部调用 ZoomOutToOriginal() 来缩回。")]
    [SerializeField] private bool autoZoomOutOnExit = true;

    // --- 内部状态 ---
    protected bool isPlayerInTrigger = false;

    private float _originalOrthoSize;
    private bool _hasOriginalOrthoSize = false;
    private bool _isZoomedIn = false;
    private Coroutine _zoomRoutine;

    protected virtual void Start()
    {
        if (InteractIndicator) InteractIndicator.SetActive(false);

        if (!zoomVCam)
        {
#if UNITY_2023_1_OR_NEWER
            zoomVCam = FindFirstObjectByType<CinemachineVirtualCamera>();
#else
            zoomVCam = FindObjectOfType<CinemachineVirtualCamera>();
#endif
        }

        // 原始大小可以提前记一次，也可以在第一次 ZoomIn 时再记。
        if (zoomVCam && !_hasOriginalOrthoSize)
        {
            _originalOrthoSize = zoomVCam.m_Lens.OrthographicSize;
            _hasOriginalOrthoSize = true;
        }
    }

    protected virtual void Update()
    {
        if (!enableZoomOnE) return;
        if (!isPlayerInTrigger) return;
        if (!zoomVCam) return;

        // 按下 E：触发 ZoomIn（只负责缩放，不做别的逻辑）
        if (Input.GetKeyDown(KeyCode.E))
        {
            // 如果之前没记录过原始值，就在第一次 ZoomIn 前记录
            if (!_hasOriginalOrthoSize)
            {
                _originalOrthoSize = zoomVCam.m_Lens.OrthographicSize;
                _hasOriginalOrthoSize = true;
            }

            StartZoom(zoomInOrthoSize);
            _isZoomedIn = true;
        }
    }

    protected virtual void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInTrigger = true;
            SetIndicator(true);
        }
    }

    protected virtual void OnTriggerStay2D(Collider2D collision)
    {
        // 原本的逻辑：按 E/W 时把提示关掉
        if (collision.CompareTag("Player"))
        {
            if (Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.W))
                SetIndicator(false);
        }
    }

    protected virtual void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInTrigger = false;
            SetIndicator(false);

            // 选项：离开 Trigger 时自动缩回
            if (autoZoomOutOnExit)
            {
                TryZoomOutToOriginal();
            }
        }
    }

    protected void SetIndicator(bool on)
    {
        if (InteractIndicator) InteractIndicator.SetActive(on);
    }

    private void Reset()
    {
        if (InteractIndicator == null)
        {
            var t = transform.Find("E") ?? transform.Find("InteractIndicator");
            if (t) InteractIndicator = t.gameObject;
        }
    }

    // ===== 提供给 Listener / Timeline / Fungus 调用的接口 =====

    /// <summary>
    /// 给 DialogueFinishListener_JsonOnly / Timeline / Button 的 UnityEvent 用：
    /// 把相机从当前大小缩回到「第一次 ZoomIn 前记录」的原始 OrthographicSize。
    /// </summary>
    public void ZoomOutToOriginal()
    {
        TryZoomOutToOriginal();
    }

    private void TryZoomOutToOriginal()
    {
        if (!enableZoomOnE) return;
        if (!zoomVCam) return;

        if (!_hasOriginalOrthoSize)
        {
            Debug.LogWarning("[UI_E] ZoomOutToOriginal: 没有记录原始 OrthographicSize，无法缩回。");
            return;
        }

        if (!_isZoomedIn)
        {
            // 已经是原始大小或没 Zoom 过，可以视情况直接返回
            return;
        }

        StartZoom(_originalOrthoSize);
        _isZoomedIn = false;
    }

    // ===== 相机缩放协程（和 BaseLevelManager 一样的逻辑） =====
    private void StartZoom(float targetSize)
    {
        if (!zoomVCam) return;

        if (_zoomRoutine != null)
            StopCoroutine(_zoomRoutine);

        _zoomRoutine = StartCoroutine(CoZoom(targetSize));
    }

    private IEnumerator CoZoom(float targetSize)
    {
        if (!zoomVCam)
        {
            _zoomRoutine = null;
            yield break;
        }

        float startSize = zoomVCam.m_Lens.OrthographicSize;

        if (Mathf.Approximately(zoomDuration, 0f))
        {
            zoomVCam.m_Lens.OrthographicSize = targetSize;
            _zoomRoutine = null;
            yield break;
        }

        float t = 0f;
        while (t < zoomDuration)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / zoomDuration);
            float k = Mathf.SmoothStep(0f, 1f, u);
            zoomVCam.m_Lens.OrthographicSize = Mathf.Lerp(startSize, targetSize, k);
            yield return null;
        }

        zoomVCam.m_Lens.OrthographicSize = targetSize;
        _zoomRoutine = null;
    }
}
