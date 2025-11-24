using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using URPLight2D = UnityEngine.Rendering.Universal.Light2D;

public class LevelManager1_2 : MonoBehaviour
{
    [SerializeField] private int myLoop = 1;

    [Header("闪烁并随后变暗的灯")]
    [SerializeField] private URPLight2D lightToBlinkAndDim;

    [System.Serializable]
    public struct BlinkStep
    {
        [Min(0.05f)] public float cycle;
        [Min(0.01f)] public float onTime;
        [Min(0f)] public float minIntensity;
        [Min(0f)] public float maxIntensity;
    }

    [Header("闪烁节奏")]
    public List<BlinkStep> blinkPattern = new()
    {
        new BlinkStep{ cycle=1.0f, onTime=0.20f, minIntensity=0.25f, maxIntensity=0.9f },
        new BlinkStep{ cycle=0.5f, onTime=0.08f, minIntensity=0.25f, maxIntensity=0.9f },
        new BlinkStep{ cycle=1.0f, onTime=0.20f, minIntensity=0.25f, maxIntensity=0.9f },
    };

    [Header("变暗参数（intensity）")]
    [SerializeField, Min(0f)] private float dimTargetIntensity = 0.2f;
    [SerializeField, Min(0f)] private float dimDuration = 1.0f;

    [Header("红灯")]
    [SerializeField] private GameObject BathroomSign;
    [SerializeField] private Sprite Sign_on;
    [SerializeField] private GameObject redLightObject;

    [Header("对话触发器")]
    [SerializeField] private DialogueTrigger dialogueTrigger;
    [SerializeField] private bool autoTriggerDialogueAfterPlayer = false;
    [SerializeField, Min(0f)] private float dialogueDelayAfterPlayer = 0f;

    [Header("音效")]
    [SerializeField] private AudioSource snd_toilet;
    [SerializeField] private AudioSource snd_RedLight;
    [SerializeField] private AudioSource snd_toiletDoor;

    // ========= 演绎5A =========
    [Header("【演绎】玩家自动移动")]
    [SerializeField] private Transform playerAutoTarget;              // 玩家要走到的目标点（放在右侧某处）
    [SerializeField, Min(0.1f)] private float playerAutoSpeed = 50f; // 玩家自动移动速度（单位/秒）
    [SerializeField] private bool freezeYWhileAuto = true;            // 只在X轴移动（保持当前Y）

    [Header("【演绎】小男孩跳下")]
    [SerializeField] private Transform boy;                           // 小男孩（含 SpriteRenderer / Collider2D）
    [SerializeField] private Transform boyJumpTarget;                 // 小男孩落点（会挡住左路）
    [SerializeField] private Transform boyJumpTarget5B;
    [SerializeField, Min(0.1f)] private float boyJumpDuration = 0.7f; // 跳跃时间
    [SerializeField] private AnimationCurve boyJumpArc = AnimationCurve.EaseInOut(0, 0, 1, 1); // 抛物线曲线(0~1)
    [SerializeField] private float boyJumpHeight = 1.2f;              // 跳跃最高点抬高量

    [Header("【演绎】闪灯")]
    [SerializeField, Min(1)] private int flickerCount = 3;            // 闪烁次数（最后一次点亮后转头）
    [SerializeField, Min(0f)] private float flickerOffTime = 0.12f;   // 熄灭时长
    [SerializeField, Min(0f)] private float flickerOnTime = 0.18f;    // 点亮时长
    [SerializeField] private List<URPLight2D> sceneLights = new();     // 需要一起闪烁的灯（可为空则用 lightToBlinkAndDim）

    [Header("【演绎】换脸后触发对话5-a")]
    [SerializeField] private DialogueTrigger dialogueTrigger2;
    [SerializeField] private bool triggerDialogueAfterFaces = true;    // 开/关
    [SerializeField, Min(0f)] private float dialogueAfterFacesDelay = 2f; // 换脸后等待时长（秒）

    // —— 音效（5B 用）——
    [Header("【演绎5B】音效")]
    [SerializeField] private AudioSource snd_breakBones;
    [SerializeField] private AudioSource snd_breakBones2;
    [SerializeField] private AudioSource snd_breakBones3;
    [SerializeField] private AudioSource snd_boyJump;

    // —— 乘务员扭头图 —— 
    [Header("【演绎5B】乘务员扭头 Sprite")]
    [SerializeField] private SpriteRenderer stewardessRenderer;
    [SerializeField] private Sprite stewardessHeadTurn1;
    [SerializeField] private Sprite stewardessHeadTurn2;

    // —— 5B 开始前女主预站位（只改 X）——
    [Header("【演绎5B】女主预站位")]
    [SerializeField] private Transform heroineStandLeftX;     // 放一个空物体当“站位点”，只用它的 X
    [SerializeField, Min(0.1f)] private float heroineMoveSpeed5B = 50f; // 5B 的移动速度

    [Header("【演绎5B】收尾")]
    [SerializeField] private ToNextLoop nextLoop;          // 拖 death trigger 上的 ToNextLoop
    [SerializeField, Min(0f)] private float finalBlackExtraHold = 0f; // 额外黑屏时长（可为 0）

    [System.Serializable]
    public struct FaceTarget
    {
        public SpriteRenderer renderer;   // 要换脸的角色
        public Sprite faceFrontSmile;     // “正对镜头的笑脸”sprite
    }

    [Header("【演绎】统一转头微笑")]
    [SerializeField] private List<FaceTarget> faceTargets = new();     // 乘客、小男孩、乘务员等
    [SerializeField, Min(0f)] private float faceDelayAfterLastOn = 0f; // 最后一次点亮到换脸的延迟

    [Header("厕所关闭互动")]
    [SerializeField] private GameObject objectToReveal;
    [SerializeField] private bool autoDeactivateOnStart = true;

    // ★★★★★ 5B 可见度控制（新增） ★★★★★
    [Header("【演绎5B】可见度与停留（新增）")]
    [SerializeField, Range(0f, 1f)] private float minVisibleIntensity5B = 0.75f; // 换脸时至少这么亮
    [SerializeField, Min(0f)] private float faceHoldLitSeconds5B = 0.75f;        // 换脸后保持点亮的时间

    // —— runtime 缓存 ——（每个 Manager 用 tag 找主角）
    private Rigidbody2D _playerRb;
    private PlayerController _playerCtrl;
    private GameObject playerObject;
    private readonly List<SpriteRenderer> _playerSprites = new();

    private void Awake()
    {
        // 开场禁止移动
        if (Gamemanager.instance) Gamemanager.instance.phase = GamePhase.Loading;

        // ★ 用 tag 找 Player 并缓存组件（所有 manager 都建议这样做）
        playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject)
        {
            CachePlayerSprites();
            SetPlayerSpritesVisible(false);

            _playerRb = playerObject.GetComponent<Rigidbody2D>();
            _playerCtrl = playerObject.GetComponent<PlayerController>();
        }
        else
        {
            Debug.LogWarning("[LevelManager1_2] 没找到 tag=Player 的激活对象。");
        }

        if (!dialogueTrigger)
            Debug.Log("No dialogue trigger!!");
    }

    private void Start()
    {
        LoopTracker.I?.SetLoop(myLoop);

        if (!lightToBlinkAndDim)
        {
            Debug.LogWarning("[LevelManager1_2] 未设置 lightToBlinkAndDim，灯光流程不会开始。");
            return;
        }

        if (autoDeactivateOnStart && objectToReveal && objectToReveal.activeSelf)
            objectToReveal.SetActive(false);

        StartCoroutine(RunLightsThenRevealPlayer());
    }

    private IEnumerator RunLightsThenRevealPlayer()
    {
        // —— 0) 黑屏 2.5 秒 ——
        lightToBlinkAndDim.intensity = 0f;
        yield return new WaitForSeconds(2.5f);

        // —— 1) 连续闪烁 4~5 秒 ——
        var step = (blinkPattern != null && blinkPattern.Count > 0)
            ? blinkPattern[0]
            : new BlinkStep { cycle = 1.0f, onTime = 0.20f, minIntensity = 0.25f, maxIntensity = 0.9f };

        LightControl.StartBlink(lightToBlinkAndDim, step.cycle, step.onTime, step.minIntensity, step.maxIntensity);
        yield return new WaitForSeconds(Random.Range(4f, 5f));
        LightControl.StopBlink(lightToBlinkAndDim);

        // —— 2) global light Dim ——
        LightControl.Dim(lightToBlinkAndDim, dimTargetIntensity, dimDuration);
        yield return new WaitForSeconds(dimDuration);

        yield return new WaitForSeconds(1.3f);

        // —— 3) 红光亮 ——
        var sr = BathroomSign ? BathroomSign.GetComponent<SpriteRenderer>() : null;
        if (sr && Sign_on) sr.sprite = Sign_on;
        if (redLightObject) redLightObject.SetActive(true);
        snd_RedLight.Play();

        // —— 4) 等 2 秒 ——
        yield return new WaitForSeconds(2f);

        // —— 5) 播放厕所音效，等它播完 ——
        if (snd_toilet)
        {
            snd_toilet.Play();
            if (snd_toilet.clip) yield return new WaitForSeconds(snd_toilet.clip.length);
            else while (snd_toilet.isPlaying) yield return null;
        }

        // —— 6) 再等 2 秒 ——
        yield return new WaitForSeconds(2f);

        // —— 7) 角色出现——
        snd_toiletDoor.Play();
        SetPlayerSpritesVisible(true);

        // —— 8) 等 0.7 秒 → 对话框 ——
        yield return new WaitForSeconds(0.7f);

        if (objectToReveal && !objectToReveal.activeSelf)
            objectToReveal.SetActive(true);

        dialogueTrigger?.TriggerDialogue();
    }

    private void CachePlayerSprites()
    {
        _playerSprites.Clear();
        if (!playerObject) return;
        _playerSprites.AddRange(playerObject.GetComponentsInChildren<SpriteRenderer>(true));
    }

    private void SetPlayerSpritesVisible(bool visible)
    {
        foreach (var sr in _playerSprites)
            if (sr) sr.enabled = visible;
    }

    // 5A演绎
    public void DoSpecialThingWhenInBound1()
    {
        if (_eventRoutine != null) return;   // 防重入
        _eventRoutine = StartCoroutine(EventSequence_BoyBlocksAndSmile());
    }

    // 5B入口
    public void DoSequence5B()
    {
        if (_eventRoutine != null) return;   // 防重入
        _eventRoutine = StartCoroutine(EventSequence_5B());
    }

    private Coroutine _eventRoutine;

    // ========= 5A演绎主流程 =========
    private IEnumerator EventSequence_BoyBlocksAndSmile()
    {
        if (Gamemanager.instance) Gamemanager.instance.phase = GamePhase.Eventing;

        // ★ 停掉玩家控制 + 走路动画 + 脚步声
        if (_playerCtrl)
            _playerCtrl.DisablePlayerControl();

        bool hadKinematic = false;
        if (_playerRb)
        {
            hadKinematic = _playerRb.isKinematic;
            _playerRb.velocity = Vector2.zero;
            _playerRb.isKinematic = true;
        }

        // 自动向右移动到目标
        if (playerObject && playerAutoTarget)
            yield return MovePlayerXOverSeconds(playerAutoTarget.position.x, 0.5f);

        // 小男孩跳下
        if (boy && boyJumpTarget)
            yield return JumpObjectTo(boy, boyJumpTarget.position, boyJumpDuration, boyJumpHeight, boyJumpArc);

        // 闪灯 + 换脸
        yield return FlickerLightsThenFace();

        if (dialogueAfterFacesDelay > 0f)
            yield return new WaitForSeconds(dialogueAfterFacesDelay);

        // 恢复玩家可动 + 对话
        RestorePlayerControl();
        dialogueTrigger2?.TriggerDialogue();

        _eventRoutine = null;
    }

    // ========= 5B演绎主流程（修正版） =========
    private IEnumerator EventSequence_5B()
    {
        if (Gamemanager.instance) Gamemanager.instance.phase = GamePhase.Eventing;

        // ★ 禁用玩家控制
        if (_playerCtrl) _playerCtrl.DisablePlayerControl();

        // ★ 清速度并设成 kinematic，方便我们自己 MovePosition / 瞬移
        if (_playerRb)
        {
            _playerRb.velocity = Vector2.zero;
            _playerRb.isKinematic = true;
        }

        // 瞬移：女主放到左侧站位点的 X
        if (playerObject && heroineStandLeftX)
        {
            var p = playerObject.transform.position;
            var snap = new Vector3(heroineStandLeftX.position.x, p.y, p.z);
            playerObject.transform.position = snap;
            if (_playerRb)
            {
                _playerRb.position = snap;
                _playerRb.velocity = Vector2.zero;
            }
        }
        Debug.Log("[5B] heroine snapped, go lights...");

        // 选灯
        var lights = new List<URPLight2D>();
        if (sceneLights != null && sceneLights.Count > 0) lights.AddRange(sceneLights);
        else if (lightToBlinkAndDim) lights.Add(lightToBlinkAndDim);

        if (lights.Count == 0)
            Debug.LogWarning("[LevelManager1_2][5B] 没有可操作的灯光，流程继续但不会闪光。");

        // 记录原强度
        var original = new float[lights.Count];
        for (int i = 0; i < lights.Count; i++) original[i] = lights[i] ? lights[i].intensity : 1f;

        // ★ 让 5B 的“亮回去”至少有可见强度
        s_MinOnIntensityOverride = minVisibleIntensity5B;

        // Step 1
        if (snd_breakBones) snd_breakBones.Play();
        yield return PulseOnce(
            lights, original,
            offTime: flickerOffTime,
            onTime: flickerOnTime,
            onWhileDark: () =>
            {
                if (stewardessRenderer && stewardessHeadTurn1)
                    stewardessRenderer.sprite = stewardessHeadTurn1;
            }
        );
        yield return new WaitForSeconds(0.05f);

        // Step 2
        if (snd_breakBones2) snd_breakBones2.Play();
        yield return PulseOnce(
            lights, original,
            offTime: flickerOffTime,
            onTime: flickerOnTime,
            onWhileDark: () =>
            {
                if (stewardessRenderer && stewardessHeadTurn2)
                    stewardessRenderer.sprite = stewardessHeadTurn2;
            }
        );
        yield return new WaitForSeconds(0.05f);

        // Step 3（跳跃）
        Coroutine jumpC = null;
        if (snd_boyJump) snd_boyJump.Play();
        yield return PulseOnce(
            lights, original,
            offTime: flickerOffTime,
            onTime: flickerOnTime,
            onWhileDark: () =>
            {
                var targetTf = boyJumpTarget5B ? boyJumpTarget5B : boyJumpTarget;
                if (boy && targetTf)
                {
                    jumpC = StartCoroutine(JumpObjectTo(
                        boy,
                        targetTf.position,
                        boyJumpDuration,
                        boyJumpHeight,
                        boyJumpArc
                    ));
                }
                else
                {
                    Debug.LogWarning("[5B] 未设置 boy 或 5B/默认落点，无法执行跳跃。");
                }
            }
        );
        if (jumpC != null) yield return jumpC;

        // 统一转头微笑：先确保现在是亮的且“够亮”
        if (faceDelayAfterLastOn > 0f) yield return new WaitForSeconds(faceDelayAfterLastOn);
        for (int i = 0; i < lights.Count; i++)
            if (lights[i]) lights[i].intensity = Mathf.Max(original[i], minVisibleIntensity5B);

        for (int i = 0; i < faceTargets.Count; i++)
        {
            var ft = faceTargets[i];
            if (ft.renderer && ft.faceFrontSmile)
                ft.renderer.sprite = ft.faceFrontSmile;
        }

        // ★ 换完脸后，保持点亮一小段时间给玩家看清楚
        if (faceHoldLitSeconds5B > 0f)
            yield return new WaitForSeconds(faceHoldLitSeconds5B);

        // 收尾：全黑 + breakBones3 + 跳下一轮
        var lights2 = new List<URPLight2D>();
        if (sceneLights != null && sceneLights.Count > 0) lights2.AddRange(sceneLights);
        else if (lightToBlinkAndDim) lights2.Add(lightToBlinkAndDim);

        if (lights2.Count > 0)
        {
            for (int i = 0; i < lights2.Count; i++)
                if (lights2[i]) lights2[i].intensity = 0f;
        }

        if (snd_breakBones3)
        {
            snd_breakBones3.Play();
            if (snd_breakBones3.clip) yield return new WaitForSeconds(snd_breakBones3.clip.length);
            else while (snd_breakBones3.isPlaying) yield return null;
        }

        if (finalBlackExtraHold > 0f) yield return new WaitForSeconds(finalBlackExtraHold);

        RestorePlayerControl();
        _eventRoutine = null;

        // ★ 还原覆盖，避免影响其它流程
        s_MinOnIntensityOverride = 0f;

        nextLoop?.toNextLoop();
        yield break;
    }

    // ========= 工具：抛物线跳跃 =========
    private IEnumerator JumpObjectTo(Transform t, Vector3 targetPos, float duration, float height, AnimationCurve arc)
    {
        if (!t) yield break;

        Vector3 start = t.position;
        float fixedZ = start.z;                           // 记录起始 Z
        Vector3 targetNoZ = new Vector3(targetPos.x, targetPos.y, fixedZ);
        float timer = 0f;
        if (arc == null || arc.length == 0) arc = AnimationCurve.EaseInOut(0, 0, 1, 1);

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float u = Mathf.Clamp01(timer / duration);

            Vector3 pos = Vector3.Lerp(start, targetNoZ, u);
            float yOffset = arc.Evaluate(u) * height;
            pos.y = Mathf.Lerp(start.y, targetNoZ.y, u) + yOffset;
            pos.z = fixedZ;

            t.position = pos;
            yield return null;
        }

        t.position = new Vector3(targetNoZ.x, targetNoZ.y, fixedZ);
    }

    // ★★★★★ 最小亮度覆盖（新增支持） ★★★★★
    private static float s_MinOnIntensityOverride = 0f;

    // ========= 工具：闪灯 + 统一换脸（5A 用） =========
    private IEnumerator FlickerLightsThenFace()
    {
        var lights = new List<URPLight2D>();
        if (sceneLights != null && sceneLights.Count > 0) lights.AddRange(sceneLights);
        else if (lightToBlinkAndDim) lights.Add(lightToBlinkAndDim);
        if (lights.Count == 0) yield break;

        var original = new float[lights.Count];
        for (int i = 0; i < lights.Count; i++) original[i] = lights[i] ? lights[i].intensity : 1f;

        // 这里不强制最小亮度，保持 5A 原节奏
        float prevOverride = s_MinOnIntensityOverride;
        s_MinOnIntensityOverride = 0f;

        for (int k = 0; k < flickerCount; k++)
        {
            // off
            for (int i = 0; i < lights.Count; i++) if (lights[i]) lights[i].intensity = 0f;
            yield return new WaitForSeconds(flickerOffTime);

            // on（5A 不覆盖）
            for (int i = 0; i < lights.Count; i++) if (lights[i]) lights[i].intensity = original[i];
            yield return new WaitForSeconds(flickerOnTime);
        }

        if (faceDelayAfterLastOn > 0f) yield return new WaitForSeconds(faceDelayAfterLastOn);

        for (int i = 0; i < faceTargets.Count; i++)
        {
            var ft = faceTargets[i];
            if (ft.renderer && ft.faceFrontSmile)
                ft.renderer.sprite = ft.faceFrontSmile;
        }

        s_MinOnIntensityOverride = prevOverride;
    }

    // ========= 工具：一次黑→亮的脉冲（支持最小亮度覆盖，5B 会用） =========
    private IEnumerator PulseOnce(
        List<URPLight2D> lights, float[] original,
        float offTime, float onTime,
        System.Action onWhileDark)
    {
        if (lights != null && lights.Count > 0)
        {
            // 黑
            for (int i = 0; i < lights.Count; i++)
                if (lights[i]) lights[i].intensity = 0f;

            // 黑的瞬间执行回调（换脸/跳跃等）
            onWhileDark?.Invoke();

            // 保持黑
            if (offTime > 0f) yield return new WaitForSeconds(offTime);

            // 亮（保证一个最小可见强度）
            for (int i = 0; i < lights.Count; i++)
                if (lights[i]) lights[i].intensity = Mathf.Max(original[i], s_MinOnIntensityOverride);

            // 保持亮片刻
            if (onTime > 0f) yield return new WaitForSeconds(onTime);
        }
        else
        {
            // 没灯可控：只做回调与等待，保证流程不断
            onWhileDark?.Invoke();
            if (offTime > 0f) yield return new WaitForSeconds(offTime);
            if (onTime > 0f) yield return new WaitForSeconds(onTime);
        }
    }

    // 只沿 X 轴在固定时长内移动到 targetX（Y/Z 不变）
    private IEnumerator MovePlayerXOverSeconds(float targetX, float duration)
    {
        if (!playerObject) yield break;

        var t = playerObject.transform;
        Vector3 start = t.position;
        Vector3 target = new Vector3(targetX, start.y, start.z);

        var conf = FindObjectOfType<Cinemachine.CinemachineConfiner2D>();
        var poly = conf ? conf.m_BoundingShape2D as PolygonCollider2D : null;
        if (poly) target = poly.ClosestPoint(target);

        float timer = 0f;
        duration = Mathf.Max(0.0001f, duration);

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float u = Mathf.Clamp01(timer / duration);

            Vector3 next = Vector3.Lerp(start, target, u);
            if (_playerRb) _playerRb.MovePosition(next);
            else t.position = next;

            yield return null;
        }

        if (_playerRb) _playerRb.MovePosition(target);
        else t.position = target;
    }

    // ★ 恢复玩家（用 PlayerController 的 Enable 接口）
    private void RestorePlayerControl()
    {
        if (_playerCtrl)
            _playerCtrl.EnablePlayerControl();

        if (_playerRb)
        {
            _playerRb.isKinematic = false;
            _playerRb.velocity = Vector2.zero;
        }
    }
}
