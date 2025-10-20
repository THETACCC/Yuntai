using System.Collections;
using System.Collections.Generic;
using UnityEngine;
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

    // --- runtime ---
    private GameObject _player;
    private readonly List<SpriteRenderer> _playerSprites = new();
    private readonly List<Collider2D> _playerCols = new(); // 现在不再禁用，只是保留引用
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

                // 允许自由落体（不要再 isKinematic=true）
                _rb2d.isKinematic = false;
                _rb2d.simulated = true;
                _rb2d.gravityScale = _origGravityScale; // 不改重力大小

                // 锁住水平位移，避免横向漂移；同时通常锁Z旋转
                if (freezeXWhileCinematic)
                    _rb2d.constraints = _origConstraints | RigidbodyConstraints2D.FreezePositionX | RigidbodyConstraints2D.FreezeRotation;

                // 清掉一切初始X速度，避免横向惯性
                _rb2d.velocity = new Vector2(0f, _rb2d.velocity.y);
            }

            // 只隐藏 Sprite；Collider 全程保持启用（让角色自然落地）
            if (hideSpriteOnAwake) SetPlayerSpritesVisible(false);
            // 注意：不再调用 SetPlayerCollidersEnabled(false)
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

        // 压暗其它灯，记录原值
        _extraLightsOrig = LightControl.CaptureIntensities(extraLights);
        for (int i = 0; i < extraLights.Count; i++)
            if (extraLights[i]) extraLights[i].intensity = 0f;

        // （可选）阶段
        if (Gamemanager.instance) Gamemanager.instance.phase = GamePhase.Loading;
    }

    private void Start()
    {
        StartCoroutine(Sequence_Intro());
    }

    private IEnumerator Sequence_Intro()
    {
        // 0) 纯黑停留
        if (blackHoldSeconds > 0f) yield return new WaitForSeconds(blackHoldSeconds);

        // 1) 灯光淡入
        if (globalLight)
            yield return LightControl.DimIE(globalLight, lightTargetIntensity, fadeDuration, fadeCurve);

        // 2) 触发 Start1（玩家此时可能在空中，Collider/重力会让他自然落地；Sprite 仍隐藏）
        if (start1Trigger)
        {
            if (start1Delay > 0f) yield return new WaitForSeconds(start1Delay);
            start1Trigger.TriggerDialogue();
        }
        // Start1 完成后：调用 OnStart1FullyClosed()
    }

    /// <summary>
    /// Start1 完全关闭后：只显示玩家 Sprite，然后触发 Start2。
    /// 不再做任何“对齐/吸附/位置修正”，让物理结果说了算。
    /// </summary>
    public void OnStart1FullyClosed()
    {
        SetPlayerSpritesVisible(true);      // 仅显形
        StartCoroutine(CoTriggerStart2());  // 仍然禁止移动/锁X，继续 Start2
    }

    private IEnumerator CoTriggerStart2()
    {
        if (start2Delay > 0f) yield return new WaitForSeconds(start2Delay);
        if (start2Trigger) start2Trigger.TriggerDialogue();
        // Start2 完成后：调用 OnStart2FullyClosed()
    }

    /// <summary>
    /// Start2 完全关闭后：恢复玩家移动与原始物理约束，恢复额外灯。
    /// </summary>
    public void OnStart2FullyClosed()
    {
        // 恢复刚体原约束与控制
        if (_hasRb)
        {
            _rb2d.constraints = _origConstraints; // 解锁水平位移/恢复旋转规则
            _rb2d.velocity = new Vector2(_rb2d.velocity.x, 0f); // 清 Y 轴小抖动
        }
        if (_playerCtrl) _playerCtrl.enabled = true;

        // 若有阶段枚举，请换成真实存在的“可玩”阶段；否则注释掉。
        // if (Gamemanager.instance) Gamemanager.instance.phase = GamePhase.Playing;

        // 还原额外灯
        if (_extraLightsOrig != null)
        {
            for (int i = 0; i < extraLights.Count; i++)
                if (extraLights[i])
                    extraLights[i].intensity = (i < _extraLightsOrig.Length ? _extraLightsOrig[i] : 1f);
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
        // 注意：不禁用 collider
    }

    private void SetPlayerSpritesVisible(bool visible)
    {
        foreach (var sr in _playerSprites) if (sr) sr.enabled = visible;
    }

    // 备用：如果你将来想手动显示（不解锁移动）
    public void RevealPlayerNow_NoMove()
    {
        SetPlayerSpritesVisible(true);
    }
}
