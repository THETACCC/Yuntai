using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using URPLight2D = UnityEngine.Rendering.Universal.Light2D;
using DialogueSystem;
using Cinemachine;

public class LevelManager4_2 : BaseLevelManager
{
    // ===================== Blackout: Lights + Particles =====================
    [Header("Blackout - Collect Targets")]
    [Tooltip("true=全场景收集 Light2D + Light + ParticleSystem；false=只从 root 收集")]
    [SerializeField] private bool collectAllScene = true;

    [Tooltip("collectAllScene=false 时使用（含inactive）")]
    [SerializeField] private Transform blackoutRoot;

    [Header("Blackout Timing")]
    [SerializeField, Min(0f)] private float fadeOutDuration = 0.5f;
    [SerializeField, Min(0f)] private float blackHoldDuration = 0.2f;
    [SerializeField, Min(0f)] private float fadeInDuration = 0.6f;
    [SerializeField] private AudioSource blackoutSfx;

    [Header("Blackout Control")]
    [Tooltip("黑屏保持阶段：每帧把所有灯强制压到 0（防止别的脚本把 intensity 写回去）")]
    [SerializeField] private bool forceHoldLightsAtZeroDuringBlack = true;

    [Tooltip("黑屏期间暂停粒子（StopEmittingAndClear），亮回来时恢复 Play")]
    [SerializeField] private bool controlParticlesDuringBlackout = true;

    [Tooltip("是否包含原本 disabled 的粒子/灯也一起控制")]
    [SerializeField] private bool includeOriginallyDisabled = false;

    // --- captured lights (one blackout sequence) ---
    private URPLight2D[] _cap2D = new URPLight2D[0];
    private Light[] _cap3D = new Light[0];

    private float[] _cap2DIntensity = new float[0];
    private bool[] _cap2DEnabled = new bool[0];

    private float[] _cap3DIntensity = new float[0];
    private bool[] _cap3DEnabled = new bool[0];

    // --- captured particles (one blackout sequence) ---
    private ParticleSystem[] _capParticles = new ParticleSystem[0];
    private bool[] _capParticleActive = new bool[0];
    private bool[] _capParticleWasPlaying = new bool[0];

    private bool _hasCapture;
    private bool _holdingForceZero;
    private Coroutine _forceHoldRoutine;

    // ===================== Cinemachine: move + zoom =====================
    [Header("Cinemachine Camera Move + Zoom")]
    [SerializeField] private CinemachineVirtualCamera vcam;
    [SerializeField] private string vcamTag = "VirtualCam";

    [SerializeField] private float moveRightX = 2f;
    [SerializeField] private float zoomOutDelta = 0.8f;

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

    // ===================== Debug =====================
    [Header("Debug")]
    [SerializeField] private bool debugLog = false;
    [SerializeField] private bool logLightsStillOnAfterFadeToBlack = false;

    private Coroutine _sequenceRoutine;

    protected override void Awake()
    {
        hidePlayerOnSceneStart = false;
        lockPlayerOnSceneStart = false;
        base.Awake();
    }

    private void Start()
    {
        EnsureVcam();
        CacheCameraState();
    }

    // =========================================================
    // Start1 OnDialogueEnd:
    // fadeOut -> (particles off) -> move+zoom -> enable -> hold -> fadeIn -> (particles back) -> trigger Start2
    // =========================================================
    public void OnStart1_End_BlackoutAndGoStart2()
    {
        if (_sequenceRoutine != null) StopCoroutine(_sequenceRoutine);
        _sequenceRoutine = StartCoroutine(Co_Start1_End_BlackoutAndGoStart2());
    }

    private IEnumerator Co_Start1_End_BlackoutAndGoStart2()
    {
        DisablePlayerMovement();
        if (Gamemanager.instance) Gamemanager.instance.phase = GamePhase.Eventing;

        if (blackoutSfx) blackoutSfx.Play();

        // ⭐ 关键：只在黑屏序列开始时 capture 一次（记录原始状态）
        CaptureTargetsForThisBlackout();

        // 1) fade to black (lights)
        yield return FadeCapturedLights(1f, 0f, fadeOutDuration);

        if (logLightsStillOnAfterFadeToBlack)
            LogLightsStillOn("AfterFadeToBlack");

        // 2) fully black: stop particles
        if (controlParticlesDuringBlackout)
            StopCapturedParticles();

        // 3) black hold: force hold lights at 0 to fight other scripts
        if (forceHoldLightsAtZeroDuringBlack)
            StartForceHoldZero();

        // 4) fully black: move + zoom out
        EnsureVcam();
        CacheCameraState();
        if (_framing != null && vcam != null)
        {
            float targetOffsetX = _originTrackedOffset.x + moveRightX;
            float targetSize = _originOrthoSize + Mathf.Max(0f, zoomOutDelta);
            yield return MoveOffsetAndZoom(targetOffsetX, targetSize, cameraMoveDuration);
        }

        // 5) enable object
        if (objectToEnable) objectToEnable.SetActive(true);

        // 6) hold black
        if (blackHoldDuration > 0f)
            yield return new WaitForSeconds(blackHoldDuration);

        // 7) stop force hold before fade in
        StopForceHoldZero();

        // 8) fade back
        yield return FadeCapturedLights(0f, 1f, fadeInDuration);

        // 9) restore particles
        if (controlParticlesDuringBlackout)
            RestoreCapturedParticles();

        // 10) trigger Start2
        yield return null;
        if (start2Trigger != null) start2Trigger.TriggerDialogue();
        else Debug.LogWarning("[LevelManager4_2] start2Trigger 未设置。");

        _sequenceRoutine = null;
    }

    // =========================================================
    // Start2 OnDialogueEnd:
    // reset move+zoom -> trigger Start3
    // =========================================================
    public void OnStart2_End_ResetCameraAndGoStart3()
    {
        if (_sequenceRoutine != null) StopCoroutine(_sequenceRoutine);
        _sequenceRoutine = StartCoroutine(Co_Start2_End_ResetCameraAndGoStart3());
    }

    private IEnumerator Co_Start2_End_ResetCameraAndGoStart3()
    {
        EnsureVcam();
        CacheCameraState();

        if (_framing != null && vcam != null)
        {
            yield return MoveOffsetAndZoom(_originTrackedOffset.x, _originOrthoSize, cameraResetDuration);
        }

        yield return null;
        if (start3Trigger != null) start3Trigger.TriggerDialogue();
        else Debug.LogWarning("[LevelManager4_2] start3Trigger 未设置。");

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
        if (_framing != null) _originTrackedOffset = _framing.m_TrackedObjectOffset;

        _cachedCamState = true;
    }

    private IEnumerator MoveOffsetAndZoom(float targetOffsetX, float targetOrthoSize, float dur)
    {
        EnsureVcam();
        if (vcam == null) yield break;

        float startSize = vcam.m_Lens.OrthographicSize;
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
            if (_framing) _framing.m_TrackedObjectOffset = Vector3.Lerp(startOffset, targetOffset, s);

            yield return null;
        }

        vcam.m_Lens.OrthographicSize = targetOrthoSize;
        if (_framing) _framing.m_TrackedObjectOffset = targetOffset;
    }

    // ===================== Capture targets (lights + particles) =====================
    private void CaptureTargetsForThisBlackout()
    {
        _hasCapture = false;

        if (collectAllScene)
        {
            _cap2D = FindObjectsByType<URPLight2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            _cap3D = FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            _capParticles = FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }
        else
        {
            if (!blackoutRoot)
            {
                _cap2D = new URPLight2D[0];
                _cap3D = new Light[0];
                _capParticles = new ParticleSystem[0];
            }
            else
            {
                _cap2D = blackoutRoot.GetComponentsInChildren<URPLight2D>(true);
                _cap3D = blackoutRoot.GetComponentsInChildren<Light>(true);
                _capParticles = blackoutRoot.GetComponentsInChildren<ParticleSystem>(true);
            }
        }

        // cache 2D lights
        _cap2DIntensity = new float[_cap2D.Length];
        _cap2DEnabled = new bool[_cap2D.Length];
        for (int i = 0; i < _cap2D.Length; i++)
        {
            var l = _cap2D[i];
            if (!l) { _cap2DIntensity[i] = 0f; _cap2DEnabled[i] = false; continue; }
            _cap2DIntensity[i] = l.intensity;
            _cap2DEnabled[i] = l.enabled;
        }

        // cache 3D lights
        _cap3DIntensity = new float[_cap3D.Length];
        _cap3DEnabled = new bool[_cap3D.Length];
        for (int i = 0; i < _cap3D.Length; i++)
        {
            var l = _cap3D[i];
            if (!l) { _cap3DIntensity[i] = 0f; _cap3DEnabled[i] = false; continue; }
            _cap3DIntensity[i] = l.intensity;
            _cap3DEnabled[i] = l.enabled;
        }

        // cache particles state
        _capParticleActive = new bool[_capParticles.Length];
        _capParticleWasPlaying = new bool[_capParticles.Length];
        for (int i = 0; i < _capParticles.Length; i++)
        {
            var ps = _capParticles[i];
            if (!ps) { _capParticleActive[i] = false; _capParticleWasPlaying[i] = false; continue; }

            _capParticleActive[i] = ps.gameObject.activeInHierarchy;
            _capParticleWasPlaying[i] = ps.isPlaying;
        }

        _hasCapture = true;

        if (debugLog)
            Debug.Log($"[LevelManager4_2] Captured: 2D={_cap2D.Length}, 3D={_cap3D.Length}, PS={_capParticles.Length}");
    }

    // ===================== Fade lights =====================
    private IEnumerator FadeCapturedLights(float fromMul, float toMul, float dur)
    {
        if (!_hasCapture) CaptureTargetsForThisBlackout();

        if (dur <= 0f)
        {
            ApplyCapturedLightMul(toMul);
            yield break;
        }

        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / dur);
            float m = Mathf.Lerp(fromMul, toMul, u);
            ApplyCapturedLightMul(m);
            yield return null;
        }

        ApplyCapturedLightMul(toMul);
    }

    private void ApplyCapturedLightMul(float mul)
    {
        // 2D lights
        for (int i = 0; i < _cap2D.Length; i++)
        {
            var l = _cap2D[i];
            if (!l) continue;

            bool shouldControl = includeOriginallyDisabled || _cap2DEnabled[i];
            if (!shouldControl) continue;

            l.enabled = includeOriginallyDisabled ? true : _cap2DEnabled[i];
            l.intensity = _cap2DIntensity[i] * mul;
        }

        // 3D lights
        for (int i = 0; i < _cap3D.Length; i++)
        {
            var l = _cap3D[i];
            if (!l) continue;

            bool shouldControl = includeOriginallyDisabled || _cap3DEnabled[i];
            if (!shouldControl) continue;

            l.enabled = includeOriginallyDisabled ? true : _cap3DEnabled[i];
            l.intensity = _cap3DIntensity[i] * mul;
        }
    }

    // ===================== Particle control =====================
    private void StopCapturedParticles()
    {
        if (!_hasCapture) return;

        for (int i = 0; i < _capParticles.Length; i++)
        {
            var ps = _capParticles[i];
            if (!ps) continue;

            bool shouldControl = includeOriginallyDisabled || _capParticleActive[i];
            if (!shouldControl) continue;

            // stop + clear: 黑屏时不应该残留粒子
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void RestoreCapturedParticles()
    {
        if (!_hasCapture) return;

        for (int i = 0; i < _capParticles.Length; i++)
        {
            var ps = _capParticles[i];
            if (!ps) continue;

            bool shouldControl = includeOriginallyDisabled || _capParticleActive[i];
            if (!shouldControl) continue;

            // 只恢复原本在播放的
            if (_capParticleWasPlaying[i])
                ps.Play(true);
        }
    }

    // ===================== Force hold lights at 0 =====================
    private void StartForceHoldZero()
    {
        _holdingForceZero = true;

        if (_forceHoldRoutine != null) StopCoroutine(_forceHoldRoutine);
        _forceHoldRoutine = StartCoroutine(CoForceHoldZero());
    }

    private void StopForceHoldZero()
    {
        _holdingForceZero = false;

        if (_forceHoldRoutine != null)
        {
            StopCoroutine(_forceHoldRoutine);
            _forceHoldRoutine = null;
        }
    }

    private IEnumerator CoForceHoldZero()
    {
        while (_holdingForceZero)
        {
            ApplyCapturedLightMul(0f);
            yield return null;
        }
    }

    // ===================== Debug =====================
    private void LogLightsStillOn(string tag)
    {
        int count = 0;

        foreach (var l in FindObjectsByType<URPLight2D>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (l && l.enabled && l.intensity > 0.01f)
            {
                Debug.Log($"[{tag}] 2D Light still on: {l.name} intensity={l.intensity}", l);
                count++;
            }
        }

        foreach (var l in FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (l && l.enabled && l.intensity > 0.01f)
            {
                Debug.Log($"[{tag}] 3D Light still on: {l.name} intensity={l.intensity}", l);
                count++;
            }
        }

        if (debugLog) Debug.Log($"[{tag}] still on count={count}");
    }
}






