using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using URPLight2D = UnityEngine.Rendering.Universal.Light2D;
using DialogueSystem;
using Cinemachine;


public class LevelManager4_2 : BaseLevelManager
{
    // ===================== Blackout: fade multiple Light2D =====================
    [Header("Blackout (Fade Multiple Light2D)")]
    [SerializeField] private URPLight2D[] lightsToFade;
    [SerializeField] private Transform lightsRoot;

    [SerializeField, Min(0f)] private float fadeOutDuration = 0.5f;
    [SerializeField, Min(0f)] private float blackHoldDuration = 0.2f;
    [SerializeField, Min(0f)] private float fadeInDuration = 0.6f;
    [SerializeField] private AudioSource blackoutSfx;

    private float[] _originalIntensities;
    private bool _cachedLightValues;

    // ===================== Cinemachine: move + zoom =====================
    [Header("Cinemachine Camera Move + Zoom")]
    [Tooltip("可不填；会自动 FindGameObjectWithTag(vcamTag)")]
    [SerializeField] private CinemachineVirtualCamera vcam;
    [SerializeField] private string vcamTag = "VirtualCam";

    [Tooltip("全黑时把相机向右推的世界单位（TrackedObjectOffset.x）")]
    [SerializeField] private float moveRightX = 2f;

    [Tooltip("第一次移动时额外 zoom out 的量（OrthographicSize 增量）。例如 0.8")]
    [SerializeField] private float zoomOutDelta = 0.2f;

    [SerializeField, Min(0f)] private float cameraMoveDuration = 0.2f;
    [SerializeField, Min(0f)] private float cameraResetDuration = 0.2f;

    private CinemachineFramingTransposer _framing;
    private Vector3 _originTrackedOffset;
    private float _originOrthoSize;
    private bool _cachedCamState;

    // ===================== Enable object when fully black =====================
    [Header("Enable Object When Fully Black")]
    [SerializeField] private GameObject objectToEnable;

    // ===================== Dialogue chaining =====================
    [Header("Dialogue Triggers (Start2 / Start3)")]
    [SerializeField] private DialogueTrigger start2Trigger;
    [SerializeField] private DialogueTrigger start3Trigger;

    [Header("Debug")]
    [SerializeField] private bool debugLog = false;

    private Coroutine _sequenceRoutine;

    protected override void Awake()
    {
        hidePlayerOnSceneStart = false;
        lockPlayerOnSceneStart = false;
        base.Awake();
    }

    private void Start()
    {
        AutoCollectLightsIfNeeded();
        EnsureVcam();
        CacheCameraState();
    }

    // =========================================================
    // Start1 OnDialogueEnd:
    // blackout -> move right -> zoom out -> trigger Start2
    // =========================================================
    public void OnStart1_End_BlackoutAndGoStart2()
    {
        if (_sequenceRoutine != null) StopCoroutine(_sequenceRoutine);
        _sequenceRoutine = StartCoroutine(Co_Start1_End_BlackoutAndGoStart2());
    }

    private IEnumerator Co_Start1_End_BlackoutAndGoStart2()
    {
        AutoCollectLightsIfNeeded();
        CacheLightOriginalsIfNeeded();

        if (lightsToFade == null || lightsToFade.Length == 0)
        {
            Debug.LogWarning("[LevelManager4_2] No Light2D to fade. Fill lightsToFade or set lightsRoot.");
            yield break;
        }

        DisablePlayerMovement();
        if (Gamemanager.instance) Gamemanager.instance.phase = GamePhase.Eventing;

        if (debugLog) Debug.Log("[4-2] Start1 end -> blackout + move + zoomOut -> Start2");

        if (blackoutSfx) blackoutSfx.Play();

        // 1) fade to black
        yield return FadeAllLights(1f, 0f, fadeOutDuration);

        // 2) fully black: move + zoom out (same duration)
        EnsureVcam();
        CacheCameraState();

        if (_framing != null && vcam != null)
        {
            float targetOffsetX = _originTrackedOffset.x + moveRightX;
            float targetSize = _originOrthoSize + Mathf.Max(0f, zoomOutDelta);
            yield return MoveOffsetAndZoom(targetOffsetX, targetSize, cameraMoveDuration);
        }
        else
        {
            Debug.LogWarning("[LevelManager4_2] Cinemachine components missing; cannot move/zoom.");
        }

        // 3) enable object
        if (objectToEnable) objectToEnable.SetActive(true);

        // 4) hold black
        if (blackHoldDuration > 0f)
            yield return new WaitForSeconds(blackHoldDuration);

        // 5) fade in
        yield return FadeAllLights(0f, 1f, fadeInDuration);

        // 6) trigger Start2 (wait 1 frame for safety)
        yield return null;

        if (start2Trigger != null)
            start2Trigger.TriggerDialogue();
        else
            Debug.LogWarning("[LevelManager4_2] start2Trigger 未设置。");

        _sequenceRoutine = null;
    }

    // =========================================================
    // Start2 OnDialogueEnd:
    // reset offsetX + reset zoom -> trigger Start3
    // =========================================================
    public void OnStart2_End_ResetCameraAndGoStart3()
    {
        if (_sequenceRoutine != null) StopCoroutine(_sequenceRoutine);
        _sequenceRoutine = StartCoroutine(Co_Start2_End_ResetCameraAndGoStart3());
    }

    private IEnumerator Co_Start2_End_ResetCameraAndGoStart3()
    {
        if (debugLog) Debug.Log("[4-2] Start2 end -> reset move + reset zoom -> Start3");

        EnsureVcam();
        CacheCameraState();

        if (_framing != null && vcam != null)
        {
            float targetOffsetX = _originTrackedOffset.x;
            float targetSize = _originOrthoSize;
            yield return MoveOffsetAndZoom(targetOffsetX, targetSize, cameraResetDuration);
        }
        else
        {
            Debug.LogWarning("[LevelManager4_2] Cinemachine components missing; cannot reset move/zoom.");
        }

        yield return null;

        if (start3Trigger != null)
            start3Trigger.TriggerDialogue();
        else
            Debug.LogWarning("[LevelManager4_2] start3Trigger 未设置。");

        EnablePlayerMovement();
        if (Gamemanager.instance) Gamemanager.instance.phase = GamePhase.Moving;

        _sequenceRoutine = null;
    }

    // ===================== Cinemachine helpers =====================
    private void EnsureVcam()
    {
        if (vcam == null)
        {
            var go = GameObject.FindGameObjectWithTag(vcamTag);
            if (go != null) vcam = go.GetComponent<CinemachineVirtualCamera>();
        }

        if (vcam != null && _framing == null)
            _framing = vcam.GetCinemachineComponent<CinemachineFramingTransposer>();
    }

    private void CacheCameraState()
    {
        if (_cachedCamState) return;

        EnsureVcam();
        if (vcam == null) return;

        _originOrthoSize = vcam.m_Lens.OrthographicSize;

        // framing may be null depending on Body setting
        if (_framing != null)
            _originTrackedOffset = _framing.m_TrackedObjectOffset;

        _cachedCamState = true;
    }

    private IEnumerator MoveOffsetAndZoom(float targetOffsetX, float targetOrthoSize, float dur)
    {
        EnsureVcam();
        if (vcam == null) yield break;

        // Zoom
        float startSize = vcam.m_Lens.OrthographicSize;

        // Offset (if framing exists)
        Vector3 startOffset = _framing ? _framing.m_TrackedObjectOffset : Vector3.zero;
        Vector3 targetOffset = new Vector3(targetOffsetX, startOffset.y, startOffset.z);

        if (dur <= 0f)
        {
            vcam.m_Lens.OrthographicSize = targetOrthoSize;
            if (_framing) _framing.m_TrackedObjectOffset = targetOffset;
            yield break;
        }

        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / dur);
            float s = Mathf.SmoothStep(0f, 1f, u);

            vcam.m_Lens.OrthographicSize = Mathf.Lerp(startSize, targetOrthoSize, s);

            if (_framing)
                _framing.m_TrackedObjectOffset = Vector3.Lerp(startOffset, targetOffset, s);

            yield return null;
        }

        vcam.m_Lens.OrthographicSize = targetOrthoSize;
        if (_framing) _framing.m_TrackedObjectOffset = targetOffset;
    }

    // ===================== Lights helpers =====================
    private void AutoCollectLightsIfNeeded()
    {
        if (lightsToFade != null && lightsToFade.Length > 0) return;
        if (!lightsRoot) return;

        lightsToFade = lightsRoot.GetComponentsInChildren<URPLight2D>(true);

        if (debugLog)
            Debug.Log($"[LevelManager4_2] Auto collected Light2D count = {lightsToFade.Length}");
    }

    private void CacheLightOriginalsIfNeeded()
    {
        if (_cachedLightValues) return;

        if (lightsToFade == null) lightsToFade = new URPLight2D[0];
        _originalIntensities = new float[lightsToFade.Length];

        for (int i = 0; i < lightsToFade.Length; i++)
        {
            var l = lightsToFade[i];
            _originalIntensities[i] = l ? l.intensity : 0f;
        }

        _cachedLightValues = true;
    }

    private IEnumerator FadeAllLights(float fromMul, float toMul, float dur)
    {
        CacheLightOriginalsIfNeeded();

        if (dur <= 0f)
        {
            ApplyLightMultiplier(toMul);
            yield break;
        }

        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / dur);
            float m = Mathf.Lerp(fromMul, toMul, u);
            ApplyLightMultiplier(m);
            yield return null;
        }

        ApplyLightMultiplier(toMul);
    }

    private void ApplyLightMultiplier(float mul)
    {
        if (lightsToFade == null) return;

        for (int i = 0; i < lightsToFade.Length; i++)
        {
            var l = lightsToFade[i];
            if (!l) continue;
            l.intensity = _originalIntensities[i] * mul;
        }
    }
}





