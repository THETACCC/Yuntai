using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Cinemachine;
using URPLight2D = UnityEngine.Rendering.Universal.Light2D;

public class LevelManager2_2 : MonoBehaviour
{
    [Header("Loop / Tag")]
    [SerializeField] private int myLoop = 2;

    [Header("全黑由灯光控制（URP 2D Global Light）")]
    [SerializeField] private URPLight2D globalLight;
    [SerializeField] private List<URPLight2D> extraLights = new();

    [Header("时序")]
    [SerializeField, Min(0f)] private float blackHoldSeconds = 3f;
    [SerializeField, Min(0.05f)] private float fadeDuration = 1.0f;
    [SerializeField, Min(0f)] private float lightTargetIntensity = 0.9f;
    [SerializeField] private AnimationCurve fadeCurve = null;

    [Header("对话：Start1 → 显示玩家 → Start2")]
    [SerializeField] private DialogueTrigger start1Trigger;
    [SerializeField] private DialogueTrigger start2Trigger;
    [SerializeField, Min(0f)] private float start1Delay = 0f;
    [SerializeField, Min(0f)] private float start2Delay = 0f;

    [Header("主角可见性/移动")]
    [Tooltip("开场是否隐藏 Sprite（Collider 不再关闭）")]
    [SerializeField] private bool hideSpriteOnAwake = true;

    [Tooltip("演出期间锁住水平位移，让角色只受重力沿Y轴下落")]
    [SerializeField] private bool freezeXWhileCinematic = true;

    [Header("演出结束时处理")]
    [Tooltip("（不再使用）灯灭后是否把玩家Sprite也隐藏")]
    [SerializeField] private bool hidePlayerSpritesWhenEnd = false;   // 不再在代码中使用

    [Header("Audio")]
    [SerializeField] private AudioSource assignedAudioSource;
    public AudioSource AssignedAudioSource => assignedAudioSource;

    // ========= 人物换脸 & 跳场景（无音效） =========
    [System.Serializable]
    private struct FaceSwap { public SpriteRenderer target; public Sprite newSprite; }

    [Header("2-2 换脸设置（和 1-2 同逻辑）")]
    [SerializeField] private List<FaceSwap> faceSwaps = new();

    [Header("下一个 Loop 的场景名")]
    [SerializeField] private string nextSceneName;

    // ====== Death Acting（相机自动寻找版） ======
    [Header("Death Acting - Camera Move")]
    [Tooltip("若场景中有多个VCam，可在此手动指定；留空则自动寻找第一个。")]
    [SerializeField] private CinemachineVirtualCamera vcam;
    [Tooltip("优先移动 VCam 的 Follow 目标；若为空则移动 VCam 自身 Transform。")]
    [SerializeField] private bool moveFollowIfAvailable = true;
    [SerializeField] private float camMoveDeltaX = 6f;             // 往右移动多少（世界单位）
    [SerializeField, Min(0f)] private float camMoveDuration = 2f;  // 相机到达用时
    [SerializeField, Min(0f)] private float waitAfterCameraSeconds = 1f; // 相机到位后额外等待

    [Header("Death Acting - Light")]
    [SerializeField] private URPLight2D deathSpot;       // 2D 点光（Light2D）
    [SerializeField, Min(0f)] private float spotRiseDuration = 3f;
    [SerializeField] private float spotTargetIntensity = 1000f;
    [SerializeField] private float spotTargetOuterRadius = 7.5f;
    [SerializeField] private float spotTargetInnerRadius = 3f;
    [SerializeField] private float spotQuickFadeDuration = 0.25f;

    [Header("Death Acting - Actor & Dialogue")]
    [SerializeField] private GameObject zhoushu;         // 周叔对象（场景实例）
    [SerializeField] private DialogueTrigger deathTrigger; // “Death” 对话触发器

    [Header("Timing / Debug")]
    [Tooltip("使用不受 timeScale 影响的时间（建议开，以免暂停卡住）")]
    [SerializeField] private bool useUnscaledTime = true;
    [SerializeField] private bool verboseLog = false;

    [Header("Death Acting - ToNextLoop（2-2 → 3-1 用）")]
    [SerializeField] private ToNextLoop nextLoop;   // 在 Inspector 里拖 1-2 用的那个，或者另做一个


    // --- runtime ---
    private GameObject _player;
    private readonly List<SpriteRenderer> _playerSprites = new();
    private readonly List<Collider2D> _playerCols = new(); // 不禁用，只缓存
    private PlayerController _playerCtrl;
    private Rigidbody2D _rb2d;

    private float[] _extraLightsOrig;
    private RigidbodyConstraints2D _origConstraints;
    private float _origGravityScale;
    private bool _hasRb = false;

    private Coroutine _deathRoutine;

    // 相机移动的目标 + 原始位置（用来恢复 offset）
    private Transform _camMoveTarget;
    private Vector3 _camOriginalPos;
    private bool _hasCamOriginalPos = false;

    private void Awake()
    {
        LoopTracker.I?.SetLoop(myLoop);

        // 找 Player（禁止手动指派）
        _player = GameObject.FindGameObjectWithTag("Player");
        if (_player)
        {
            CachePlayerSpritesAndColliders();

            _playerCtrl = _player.GetComponent<PlayerController>();
            if (_playerCtrl)
                _playerCtrl.DisablePlayerControl();   // 用接口锁控制（会关掉动画和脚步声）

            _rb2d = _player.GetComponent<Rigidbody2D>();
            _hasRb = _rb2d != null;
            if (_hasRb)
            {
                _origConstraints = _rb2d.constraints;
                _origGravityScale = _rb2d.gravityScale;

                _rb2d.isKinematic = false;
                _rb2d.simulated = true;
                _rb2d.gravityScale = _origGravityScale;

                if (freezeXWhileCinematic)
                    _rb2d.constraints = _origConstraints | RigidbodyConstraints2D.FreezePositionX | RigidbodyConstraints2D.FreezeRotation;

                _rb2d.velocity = new Vector2(0f, _rb2d.velocity.y);
            }

            if (hideSpriteOnAwake) SetPlayerSpritesVisible(false);
        }
        else
        {
            Debug.LogWarning("[LevelManager2_2] 未找到 tag=Player 的对象。");
        }

        // 灯控全黑
        if (!globalLight) Debug.LogError("[LevelManager2_2] 请指定 Global Light 2D。");
        else globalLight.intensity = 0f;

        // 压暗其它灯，并记录原值
        _extraLightsOrig = LightControl.CaptureIntensities(extraLights);
        for (int i = 0; i < extraLights.Count; i++)
            if (extraLights[i]) extraLights[i].intensity = 0f;

        if (Gamemanager.instance) Gamemanager.instance.phase = GamePhase.Loading;

        // 自动寻找 VCam（若未手动指派）
        if (!vcam)
        {
#if UNITY_2023_1_OR_NEWER
            vcam = FindFirstObjectByType<CinemachineVirtualCamera>();
#else
            vcam = FindObjectOfType<CinemachineVirtualCamera>();
#endif
            if (!vcam)
                Debug.LogWarning("[LevelManager2_2] 未找到 CinemachineVirtualCamera，DeathActing 将跳过相机移动步骤。");
        }
    }

    private void Start()
    {
        StartCoroutine(Sequence_Intro());
    }

    private IEnumerator Sequence_Intro()
    {
        if (blackHoldSeconds > 0f) yield return WaitSeconds(blackHoldSeconds);

        if (globalLight)
            yield return LightControl.DimIE(globalLight, lightTargetIntensity, fadeDuration, fadeCurve);

        if (start1Trigger)
        {
            if (start1Delay > 0f) yield return WaitSeconds(start1Delay);
            start1Trigger.TriggerDialogue();
        }
    }

    /// <summary>Start1 完全关闭后：仅显示玩家Sprite，然后触发Start2。</summary>
    public void OnStart1FullyClosed()
    {
        SetPlayerSpritesVisible(true);
        StartCoroutine(CoTriggerStart2());
    }

    private IEnumerator CoTriggerStart2()
    {
        if (start2Delay > 0f) yield return WaitSeconds(start2Delay);
        if (start2Trigger) start2Trigger.TriggerDialogue();
        yield break;
    }

    /// <summary>Start2 完全关闭后：恢复玩家 & 灯光 → 换脸 → 立即切场景（无音效等待）。</summary>
    public void OnStart2FullyClosed()
    {
        // 恢复刚体原约束与控制
        if (_hasRb)
        {
            _rb2d.constraints = _origConstraints;
            _rb2d.velocity = new Vector2(_rb2d.velocity.x, 0f);
        }

        if (_playerCtrl)
            _playerCtrl.EnablePlayerControl();   // 恢复移动和动画

        // 还原额外灯
        if (_extraLightsOrig != null)
        {
            for (int i = 0; i < extraLights.Count; i++)
                if (extraLights[i])
                    extraLights[i].intensity = (i < _extraLightsOrig.Length ? _extraLightsOrig[i] : 1f);
        }

        // 换脸 + 立刻切场景（这里是“正常路线”的场景）
        ApplyFaceSwaps();
        GotoNextScene();
    }

    private void ApplyFaceSwaps()
    {
        if (faceSwaps == null || faceSwaps.Count == 0) return;
        foreach (var fs in faceSwaps)
        {
            if (fs.target == null) continue;
            fs.target.sprite = fs.newSprite;
        }
    }

    private void GotoNextScene()
    {
        if (!string.IsNullOrEmpty(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
        }
        else
        {
            Debug.LogWarning("[LevelManager2_2] nextSceneName 未设置，OnStart2FullyClosed 不会切场景。");
        }
    }

    // ---------- Death Acting：公共入口 ----------
    public void DeathActing()
    {
        if (_deathRoutine != null) StopCoroutine(_deathRoutine);
        _deathRoutine = StartCoroutine(CoDeathActing());
    }

    // ---------- Death Acting：主流程 ----------
    private IEnumerator CoDeathActing()
    {
        if (verboseLog) Debug.Log("[2-2] DeathActing start");

        // 1) 禁用玩家控制（保留物理）
        if (_playerCtrl) _playerCtrl.DisablePlayerControl();
        if (_hasRb)
        {
            _rb2d.isKinematic = false;
            _rb2d.simulated = true;
            if (freezeXWhileCinematic)
                _rb2d.constraints = _origConstraints | RigidbodyConstraints2D.FreezePositionX | RigidbodyConstraints2D.FreezeRotation;
            _rb2d.velocity = new Vector2(0f, _rb2d.velocity.y);
        }

        // 2) 相机先移动到位（记录原始位置）
        _camMoveTarget = null;
        if (vcam)
            _camMoveTarget = (moveFollowIfAvailable && vcam.Follow != null) ? vcam.Follow : vcam.transform;

        if (_camMoveTarget && camMoveDuration > 0f)
        {
            if (!_hasCamOriginalPos)
            {
                _camOriginalPos = _camMoveTarget.position;   // 记录原始位置，之后恢复
                _hasCamOriginalPos = true;
            }

            if (verboseLog) Debug.Log("[2-2] Moving camera...");
            Vector3 camStart = _camMoveTarget.position;
            Vector3 camTarget = camStart + new Vector3(camMoveDeltaX, 0f, 0f);

            float tc = 0f;
            while (tc < camMoveDuration)
            {
                tc += DeltaTime();
                float s = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(tc / camMoveDuration));
                _camMoveTarget.position = Vector3.LerpUnclamped(camStart, camTarget, s);
                yield return null;
            }
            _camMoveTarget.position = camTarget; // 强制对齐终点
        }

        // 2.5) 到位后等待 1 秒（可配）
        if (waitAfterCameraSeconds > 0f) yield return WaitSeconds(waitAfterCameraSeconds);

        // 3) 灯光从无到有
        InitDeathLight();
        if (deathSpot && spotRiseDuration > 0f)
        {
            if (verboseLog) Debug.Log("[2-2] Raising light...");
            float t = 0f;
            while (t < spotRiseDuration)
            {
                t += DeltaTime();
                float s = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / spotRiseDuration));
                deathSpot.intensity = Mathf.Lerp(0f, spotTargetIntensity, s);
                deathSpot.pointLightOuterRadius = Mathf.Lerp(0f, spotTargetOuterRadius, s);
                deathSpot.pointLightInnerRadius = Mathf.Lerp(0f, spotTargetInnerRadius, s);
                yield return null;
            }
            deathSpot.intensity = spotTargetIntensity;
            deathSpot.pointLightOuterRadius = spotTargetOuterRadius;
            deathSpot.pointLightInnerRadius = spotTargetInnerRadius;
        }

        // 4) 直接禁用周叔
        if (zhoushu && zhoushu.activeSelf)
        {
            if (verboseLog) Debug.Log("[2-2] Disabling Zhoushu GameObject.");
            zhoushu.SetActive(false);
        }

        // 5) 灯光快速淡出
        if (verboseLog) Debug.Log("[2-2] Fading light...");
        yield return FadeOutDeathLight();

        // 5.5) 不再隐藏玩家 Sprite（你说 2-2 不需要主角消失）

        // 6) 触发 “Death” 对话
        if (deathTrigger)
        {
            if (verboseLog) Debug.Log("[2-2] Trigger Death dialogue");
            deathTrigger.TriggerDialogue();
        }

        if (verboseLog) Debug.Log("[2-2] DeathActing end");
        _deathRoutine = null;
    }

    // ---------- 工具 ----------
    private void InitDeathLight()
    {
        if (!deathSpot) return;

        if (!deathSpot.gameObject.activeInHierarchy) deathSpot.gameObject.SetActive(true);
        if (!deathSpot.enabled) deathSpot.enabled = true;

        var lt = UnityEngine.Rendering.Universal.Light2D.LightType.Point;
        if (deathSpot.lightType != lt) deathSpot.lightType = lt;

        deathSpot.intensity = 0f;
        deathSpot.pointLightOuterRadius = 0f;
        deathSpot.pointLightInnerRadius = 0f;

        if (spotTargetInnerRadius > spotTargetOuterRadius)
            spotTargetInnerRadius = Mathf.Max(0f, spotTargetOuterRadius - 0.01f);
    }

    private IEnumerator FadeOutDeathLight()
    {
        if (!deathSpot) yield break;

        if (!deathSpot.enabled || (deathSpot.intensity <= 0.001f && deathSpot.pointLightOuterRadius <= 0.001f))
            yield break;

        float i0 = deathSpot.intensity;
        float o0 = deathSpot.pointLightOuterRadius;
        float in0 = deathSpot.pointLightInnerRadius;

        float tf = 0f;
        float dur = Mathf.Max(0.01f, spotQuickFadeDuration);
        while (tf < dur)
        {
            tf += DeltaTime();
            float u = Mathf.Clamp01(tf / dur);
            float k = 1f - u;
            deathSpot.intensity = i0 * k;
            deathSpot.pointLightOuterRadius = o0 * k;
            deathSpot.pointLightInnerRadius = in0 * k;
            yield return null;
        }

        deathSpot.intensity = 0f;
        deathSpot.pointLightOuterRadius = 0f;
        deathSpot.pointLightInnerRadius = 0f;
        deathSpot.enabled = false;
    }

    private float DeltaTime() => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

    private IEnumerator WaitSeconds(float seconds)
    {
        if (useUnscaledTime) yield return new WaitForSecondsRealtime(seconds);
        else yield return new WaitForSeconds(seconds);
    }

    private void CachePlayerSpritesAndColliders()
    {
        _playerSprites.Clear();
        _playerCols.Clear();
        if (!_player) return;

        _playerSprites.AddRange(_player.GetComponentsInChildren<SpriteRenderer>(true));
        _playerCols.AddRange(_player.GetComponentsInChildren<Collider2D>(true));
    }

    private void SetPlayerSpritesVisible(bool visible)
    {
        foreach (var sr in _playerSprites) if (sr) sr.enabled = visible;
    }

    // 备用：只显形不解锁移动
    public void RevealPlayerNow_NoMove()
    {
        SetPlayerSpritesVisible(true);
    }

    // 直接跳到 Loop3：Level3-1，出生点=1
    // 直接跳到 Loop3：Level3-1，出生点=1（经由 ToNextLoop 播完死亡动画再跳）
    public void ToLoop3()
    {
        // 如果 DeathActing 协程还在跑，先停掉，避免后面再动 camera / light
        if (_deathRoutine != null)
        {
            StopCoroutine(_deathRoutine);
            _deathRoutine = null;
        }

        // ① 恢复刚体原来的约束 & 清速度（防止奇怪的水平锁死）
        if (_hasRb && _rb2d != null)
        {
            _rb2d.constraints = _origConstraints;
            _rb2d.velocity = Vector2.zero;
        }

        // ② 恢复相机位置，消掉 DeathActing 时加的 offset
        if (_hasCamOriginalPos && _camMoveTarget != null)
        {
            _camMoveTarget.position = _camOriginalPos;
        }

        // ③ 通过 ToNextLoop 播放统一的死亡动画 + 黑屏，再切到 3-1
        if (nextLoop != null)
        {
            nextLoop.scenename = "Level3-1";
            nextLoop.SpawnPointLocation = 1;
            nextLoop.toNextLoop();
        }
        else
        {
            // 保险：没挂 ToNextLoop 的时候，退回到“直接传送”的老逻辑
            Debug.LogWarning("[LevelManager2_2] nextLoop(ToNextLoop) 未设置，退回为直接传送。");
            if (SceneController.instance != null)
            {
                SceneController.instance.LoadSceneAndTeleport("Level3-1", 1);
            }
            else
            {
                Debug.LogError("[LevelManager2_2] SceneController.instance 为 null，无法传送到 Level3-1(1)。");
            }
        }
    }

}
