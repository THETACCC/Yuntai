using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;

/// <summary>
/// Camera shake that NEVER disables CinemachineBrain, so follow/aim keep working.
/// Path A: Perlin (requires NoiseSettings).
/// Path B: CameraOffset jitter (no NoiseSettings needed).
/// Optional: freeze player during shake (disable behaviours & stiffen Rigidbody/Rigidbody2D).
/// Optional: ensure VCam follows the player during shake (temporary Follow binding).
/// </summary>
public class CameraShake : MonoBehaviour
{
    public enum PhysicsMode { Auto, Only2D, Only3D }

    [Header("Target VCam (必填)")]
    [SerializeField] private CinemachineVirtualCamera vcam;

    [Header("Perlin (优先)")]
    [Tooltip("Cinemachine NoiseSettings（如 6D Shake）。若为空，将自动走 CameraOffset 抖动。")]
    [SerializeField] private NoiseSettings noiseProfile;
    [Min(0f)] public float defaultAmplitude = 5f;
    [Min(0f)] public float defaultFrequency = 12f;
    [Min(0f)] public float defaultDuration = 0.5f;

    [Header("Fallback：CameraOffset 抖动（无需 NoiseSettings）")]
    [Tooltip("CameraOffset 抖动的屏幕强度系数（越大抖越明显）。")]
    [Range(0.001f, 0.2f)] public float offsetScreenScale = 0.02f;

    [Header("Trigger Options")]
    [SerializeField] private bool shakeOnPlayerTrigger = false;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private PhysicsMode physicsMode = PhysicsMode.Auto;
    [SerializeField] private float duplicateTriggerWindow = 0.05f;

    [SerializeField] private bool triggerOnlyOnce = true;   // 默认只触发一次
    private bool hasTriggered = false;                      // 记录是否已经触发过

    [Header("Follow & Player Freeze（可选）")]
    [Tooltip("若 VCam.Follow 为空，则在抖动期间临时绑定到玩家，结束后还原。")]
    [SerializeField] private bool ensureFollowPlayerDuringShake = true;
    [Tooltip("抖动期间冻结玩家（禁用下面的脚本+刚体静止），结束后还原。")]
    [SerializeField] private bool freezePlayerDuringShake = false;
    [Tooltip("需要临时禁用的玩家脚本（如 PlayerController 等）")]
    [SerializeField] private List<Behaviour> playerBehavioursToDisable = new();
    [Tooltip("如果你不想用 Tag 寻找玩家，可直接指定玩家 Transform（优先使用）")]
    [SerializeField] private Transform playerTransformOverride;

    [Header("Debug")]
    [SerializeField] private bool verboseLog = false;

    // --- private state ---
    private CinemachineBasicMultiChannelPerlin perlin;
    private CinemachineCameraOffset camOffset;
    private Coroutine running;
    private float baseAmp, baseFreq;
    private Vector3 baseOffset;
    private float lastTriggerTime = -999f;

    // Follow 临时绑定还原
    private Transform savedFollow;

    // 冻结信息缓存
    private Rigidbody2D rb2d;
    private bool rb2dWasKinematic;
    private float rb2dWasGravity;
    private Vector2 rb2dWasVelocity;

    private Rigidbody rb3d;
    private bool rb3dWasKinematic;
    private Vector3 rb3dWasVelocity;

    private List<(Behaviour comp, bool wasEnabled)> disabledComps = new();

    void Awake()
    {
        if (!vcam) vcam = FindFirstObjectByType<CinemachineVirtualCamera>();
        if (!vcam)
        {
            Debug.LogError("[CameraShake] 找不到 CinemachineVirtualCamera。请在 Inspector 指定 vcam。");
            enabled = false;
            return;
        }

        perlin = vcam.GetCinemachineComponent<CinemachineBasicMultiChannelPerlin>();
        if (!perlin) perlin = vcam.AddCinemachineComponent<CinemachineBasicMultiChannelPerlin>();
        if (perlin.m_NoiseProfile == null && noiseProfile != null)
            perlin.m_NoiseProfile = noiseProfile;
        baseAmp = perlin.m_AmplitudeGain;
        baseFreq = perlin.m_FrequencyGain;

        camOffset = vcam.GetComponent<CinemachineCameraOffset>();
        if (!camOffset) camOffset = vcam.gameObject.AddComponent<CinemachineCameraOffset>();
        baseOffset = camOffset.m_Offset;

        var playerTf = GetPlayerTransform();
        if (playerTf)
        {
            rb2d = playerTf.GetComponent<Rigidbody2D>();
            rb3d = playerTf.GetComponent<Rigidbody>();
        }
    }

    public void Shake() => Shake(defaultAmplitude, defaultFrequency, defaultDuration);

    public void Shake(float amplitude, float frequency, float duration)
    {
        if (running != null) StopCoroutine(running);
        running = StartCoroutine(CoShake(amplitude, frequency, duration));
    }

    public void StopShake()
    {
        if (running != null) StopCoroutine(running);
        RestorePerlin();
        RestoreOffset();
        RestoreFollow();
        UnfreezePlayer();
        running = null;
    }

    public void ResetTriggerShake()
    {
        hasTriggered = false;
    }

    private IEnumerator CoShake(float amp, float freq, float dur)
    {
        MaybeBindFollowToPlayer();

        if (freezePlayerDuringShake) FreezePlayer();

        bool usePerlin = (perlin != null && perlin.m_NoiseProfile != null);

        if (usePerlin)
        {
            if (verboseLog) Debug.Log("[CameraShake] Using PERLIN shake.");

            baseAmp = perlin.m_AmplitudeGain;
            baseFreq = perlin.m_FrequencyGain;

            perlin.m_AmplitudeGain = amp;
            perlin.m_FrequencyGain = freq;

            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                yield return null;
            }

            RestorePerlin();
        }
        else
        {
            if (verboseLog) Debug.Log("[CameraShake] Using CAMERA OFFSET shake (no NoiseProfile).");
            baseOffset = camOffset.m_Offset;

            var cam = Camera.main;
            float scale = cam && cam.orthographic ? cam.orthographicSize * offsetScreenScale
                                                  : offsetScreenScale;

            float worldAmp = Mathf.Max(0.001f, amp) * scale;

            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                Vector2 off2 = Random.insideUnitCircle * worldAmp;
                camOffset.m_Offset = baseOffset + new Vector3(off2.x, off2.y, 0f);
                yield return null;
            }

            RestoreOffset();
        }

        RestoreFollow();
        UnfreezePlayer();

        running = null;
    }

    private void RestorePerlin()
    {
        if (perlin != null)
        {
            perlin.m_AmplitudeGain = baseAmp;
            perlin.m_FrequencyGain = baseFreq;
        }
    }

    private void RestoreOffset()
    {
        if (camOffset != null)
            camOffset.m_Offset = baseOffset;
    }

    private Transform GetPlayerTransform()
    {
        if (playerTransformOverride) return playerTransformOverride;
        var go = GameObject.FindGameObjectWithTag(playerTag);
        return go ? go.transform : null;
    }

    private void MaybeBindFollowToPlayer()
    {
        if (!ensureFollowPlayerDuringShake) return;
        if (vcam.Follow != null) return;

        var playerTf = GetPlayerTransform();
        if (!playerTf) return;

        savedFollow = null;
        vcam.Follow = playerTf;
        if (verboseLog) Debug.Log("[CameraShake] Temporarily bound VCam.Follow to player.");
    }

    private void RestoreFollow()
    {
        if (!ensureFollowPlayerDuringShake) return;

        if (savedFollow == null && vcam && vcam.Follow != null && vcam.Follow == GetPlayerTransform())
        {
            vcam.Follow = null;
            if (verboseLog) Debug.Log("[CameraShake] Restored VCam.Follow (set back to null).");
        }
        else if (savedFollow != null && vcam)
        {
            vcam.Follow = savedFollow;
            if (verboseLog) Debug.Log("[CameraShake] Restored VCam.Follow to previous target.");
        }
        savedFollow = null;
    }

    private void FreezePlayer()
    {
        disabledComps.Clear();

        foreach (var comp in playerBehavioursToDisable)
        {
            if (!comp) continue;
            disabledComps.Add((comp, comp.enabled));
            comp.enabled = false;
        }

        if (rb2d)
        {
            rb2dWasKinematic = rb2d.isKinematic;
            rb2dWasGravity = rb2d.gravityScale;
            rb2dWasVelocity = rb2d.velocity;

            rb2d.velocity = Vector2.zero;
            rb2d.isKinematic = true;
            rb2d.gravityScale = 0f;
        }
        else if (rb3d)
        {
            rb3dWasKinematic = rb3d.isKinematic;
            rb3dWasVelocity = rb3d.velocity;

            rb3d.velocity = Vector3.zero;
            rb3d.isKinematic = true;
        }
    }

    private void UnfreezePlayer()
    {
        foreach (var (comp, wasEnabled) in disabledComps)
        {
            if (comp) comp.enabled = wasEnabled;
        }
        disabledComps.Clear();

        if (rb2d)
        {
            rb2d.isKinematic = rb2dWasKinematic;
            rb2d.gravityScale = rb2dWasGravity;
            rb2d.velocity = rb2dWasVelocity;
        }
        else if (rb3d)
        {
            rb3d.isKinematic = rb3dWasKinematic;
            rb3d.velocity = rb3dWasVelocity;
        }
    }

    private bool CanTriggerShake()
    {
        if (!shakeOnPlayerTrigger) return false;
        if (triggerOnlyOnce && hasTriggered) return false;
        if (Time.time - lastTriggerTime < duplicateTriggerWindow) return false;
        return true;
    }

    private void MarkTriggered()
    {
        lastTriggerTime = Time.time;
        hasTriggered = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (physicsMode == PhysicsMode.Only2D) return;
        if (!other.CompareTag(playerTag)) return;
        if (!CanTriggerShake()) return;

        MarkTriggered();
        Shake();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (physicsMode == PhysicsMode.Only3D) return;
        if (!other.CompareTag(playerTag)) return;
        if (!CanTriggerShake()) return;

        MarkTriggered();
        Shake();
    }
}