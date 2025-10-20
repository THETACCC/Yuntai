using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
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

    // 放在“主角可见性/移动”后面，或任意序列化字段区
    [Header("Audio")]
    [SerializeField] private AudioSource assignedAudioSource;  // 仅用于Inspector指派引用，不在换脸时播放
    public AudioSource AssignedAudioSource => assignedAudioSource; // 如果别的脚本需要拿到它


    // ========= 人物换脸 & 跳场景（无音效） =========
    [System.Serializable]
    private struct FaceSwap
    {
        public SpriteRenderer target;  // 要换的渲染器
        public Sprite newSprite;       // 换成的Sprite
    }

    [Header("2-2 换脸设置（和 1-2 同逻辑）")]
    [SerializeField] private List<FaceSwap> faceSwaps = new();

    [Header("下一个 Loop 的场景名")]
    [SerializeField] private string nextSceneName;

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

    private void Awake()
    {
        LoopTracker.I?.SetLoop(myLoop);

        // 找 Player（禁止手动指派）
        _player = GameObject.FindGameObjectWithTag("Player");
        if (_player)
        {
            CachePlayerSpritesAndColliders();

            // 关闭玩家控制但保留物理下落
            _playerCtrl = _player.GetComponent<PlayerController>();
            if (_playerCtrl) _playerCtrl.enabled = false;

            _rb2d = _player.GetComponent<Rigidbody2D>();
            _hasRb = _rb2d != null;
            if (_hasRb)
            {
                // 记录原设置
                _origConstraints = _rb2d.constraints;
                _origGravityScale = _rb2d.gravityScale;

                // 自由落体
                _rb2d.isKinematic = false;
                _rb2d.simulated = true;
                _rb2d.gravityScale = _origGravityScale;

                // 锁水平位移与Z旋转
                if (freezeXWhileCinematic)
                    _rb2d.constraints = _origConstraints | RigidbodyConstraints2D.FreezePositionX | RigidbodyConstraints2D.FreezeRotation;

                // 清初始X速度
                _rb2d.velocity = new Vector2(0f, _rb2d.velocity.y);
            }

            // 只隐藏Sprite，不动Collider
            if (hideSpriteOnAwake) SetPlayerSpritesVisible(false);
        }
        else
        {
            Debug.LogWarning("[LevelManager2_2] 未找到 tag=Player 的对象。");
        }

        // 灯控全黑
        if (!globalLight)
            Debug.LogError("[LevelManager2_2] 请指定 Global Light 2D。");
        else
            globalLight.intensity = 0f;

        // 压暗其它灯，并记录原值
        _extraLightsOrig = LightControl.CaptureIntensities(extraLights);
        for (int i = 0; i < extraLights.Count; i++)
            if (extraLights[i]) extraLights[i].intensity = 0f;

        if (Gamemanager.instance) Gamemanager.instance.phase = GamePhase.Loading;
    }

    private void Start()
    {
        StartCoroutine(Sequence_Intro());
    }

    private IEnumerator Sequence_Intro()
    {
        if (blackHoldSeconds > 0f) yield return new WaitForSeconds(blackHoldSeconds);

        if (globalLight)
            yield return LightControl.DimIE(globalLight, lightTargetIntensity, fadeDuration, fadeCurve);

        if (start1Trigger)
        {
            if (start1Delay > 0f) yield return new WaitForSeconds(start1Delay);
            start1Trigger.TriggerDialogue();
        }
        // Start1 完成后外部调用 OnStart1FullyClosed()
    }

    /// <summary>
    /// Start1 完全关闭后：仅显示玩家Sprite，然后触发Start2。
    /// </summary>
    public void OnStart1FullyClosed()
    {
        SetPlayerSpritesVisible(true);
        StartCoroutine(CoTriggerStart2());
    }

    private IEnumerator CoTriggerStart2()
    {
        if (start2Delay > 0f) yield return new WaitForSeconds(start2Delay);
        if (start2Trigger) start2Trigger.TriggerDialogue();
        // Start2 完成后外部调用 OnStart2FullyClosed()
        yield break;
    }

    /// <summary>
    /// Start2 完全关闭后：恢复玩家 & 灯光 → 换脸 → 立即切场景（无音效等待）。
    /// </summary>
    public void OnStart2FullyClosed()
    {
        // 恢复刚体原约束与控制
        if (_hasRb)
        {
            _rb2d.constraints = _origConstraints;
            _rb2d.velocity = new Vector2(_rb2d.velocity.x, 0f);
        }
        if (_playerCtrl) _playerCtrl.enabled = true;

        // 还原额外灯
        if (_extraLightsOrig != null)
        {
            for (int i = 0; i < extraLights.Count; i++)
                if (extraLights[i])
                    extraLights[i].intensity = (i < _extraLightsOrig.Length ? _extraLightsOrig[i] : 1f);
        }

        // 换脸 + 立刻切场景
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
            Debug.LogWarning("[LevelManager2_2] nextSceneName 未设置，无法切场景。可以改为调用 LoopTracker 的接口。");
            // 例如：
            // LoopTracker.I?.GotoNextLoop();
        }
    }

    // ---------- 工具 ----------
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
}
