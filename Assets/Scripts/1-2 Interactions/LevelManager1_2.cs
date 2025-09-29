using System.Collections;
using System.Collections.Generic;
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
    [SerializeField] private GameObject redLightObject;

    [Header("对话触发器")]
    [SerializeField] private DialogueTrigger dialogueTrigger;
    [SerializeField] private bool autoTriggerDialogueAfterPlayer = false;
    [SerializeField, Min(0f)] private float dialogueDelayAfterPlayer = 0f;

    [Header("音效")]
    [SerializeField] private AudioSource snd_toilet;
    [SerializeField] private AudioSource snd_RedLight;

    // ========= 演绎5A =========
    [Header("【演绎】玩家自动移动")]
    [SerializeField] private Transform playerAutoTarget;              // 玩家要走到的目标点（放在右侧某处）
    [SerializeField, Min(0.1f)] private float playerAutoSpeed = 3.5f; // 玩家自动移动速度（单位/秒）
    [SerializeField] private bool freezeYWhileAuto = true;            // 只在X轴移动（保持当前Y）

    [Header("【演绎】小男孩跳下")]
    [SerializeField] private Transform boy;                           // 小男孩（含 SpriteRenderer / Collider2D）
    [SerializeField] private Transform boyJumpTarget;                 // 小男孩落点（会挡住左路）
    [SerializeField, Min(0.1f)] private float boyJumpDuration = 0.7f; // 跳跃时间
    [SerializeField] private AnimationCurve boyJumpArc = AnimationCurve.EaseInOut(0, 0, 1, 1); // 抛物线曲线(0~1)
    [SerializeField] private float boyJumpHeight = 1.2f;              // 跳跃最高点抬高量

    [Header("【演绎】闪灯")]
    [SerializeField, Min(1)] private int flickerCount = 3;            // 闪烁次数（最后一次点亮后转头）
    [SerializeField, Min(0f)] private float flickerOffTime = 0.12f;   // 熄灭时长
    [SerializeField, Min(0f)] private float flickerOnTime = 0.18f;   // 点亮时长
    [SerializeField] private List<URPLight2D> sceneLights = new();     // 需要一起闪烁的灯（可为空则用 lightToBlinkAndDim）

    [Header("【演绎】换脸后触发对话5-a")]
    [SerializeField] private DialogueTrigger dialogueTrigger2;
    [SerializeField] private bool triggerDialogueAfterFaces = true;    // 开/关
    [SerializeField, Min(0f)] private float dialogueAfterFacesDelay = 2f; // 换脸后等待时长（秒）

    [System.Serializable]
    public struct FaceTarget
    {
        public SpriteRenderer renderer;   // 要换脸的角色
        public Sprite faceFrontSmile;     // “正对镜头的笑脸”sprite
    }

    [Header("【演绎】统一转头微笑")]
    [SerializeField] private List<FaceTarget> faceTargets = new();     // 乘客、小男孩、乘务员等
    [SerializeField, Min(0f)] private float faceDelayAfterLastOn = 0f; // 最后一次点亮到换脸的延迟

    // —— runtime 缓存 ——
    private Rigidbody2D _playerRb;
    private PlayerController _playerCtrl;
    private GameObject playerObject;
    private readonly List<SpriteRenderer> _playerSprites = new();

    private void Awake()
    {
        // 开场禁止移动
        if (Gamemanager.instance) Gamemanager.instance.phase = GamePhase.Loading;

        // 找 Player 并先隐藏所有 Sprite
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
        SetPlayerSpritesVisible(true);

        // —— 8) 等 0.7 秒 → 对话框 ——
        yield return new WaitForSeconds(0.7f);
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
    // ========= 对外接口：从 changebound 的事件里调用 =========
    public void DoSpecialThingWhenInBound1()
    {
        // 防重入：如果正在进行演绎，就不要重复开始
        if (_eventRoutine != null) return;
        _eventRoutine = StartCoroutine(EventSequence_BoyBlocksAndSmile());
    }

    private Coroutine _eventRoutine;

    // ========= 演绎主流程 =========
    private IEnumerator EventSequence_BoyBlocksAndSmile()
    {
        // 1) 关玩家输入 + 禁用 PlayerController（避免它的 Confine/速度写入跟你打架）
        if (Gamemanager.instance) Gamemanager.instance.phase = GamePhase.Eventing;

        bool hadController = false;
        if (_playerCtrl)
        {
            hadController = _playerCtrl.enabled;
            _playerCtrl.enabled = false;
        }

        // 临时钳制刚体，避免外力干扰
        bool hadKinematic = false;
        if (_playerRb)
        {
            hadKinematic = _playerRb.isKinematic;
            _playerRb.velocity = Vector2.zero;
            _playerRb.isKinematic = true;
        }

        // 2) 自动向右移动到目标
        if (playerObject && playerAutoTarget)
            yield return MoveObjectTo(playerObject.transform, playerAutoTarget.position, playerAutoSpeed, freezeYWhileAuto);

        // 3) 小男孩跳下（抛物线）
        if (boy && boyJumpTarget)
            yield return JumpObjectTo(boy, boyJumpTarget.position, boyJumpDuration, boyJumpHeight, boyJumpArc);

        // 4) 闪灯 + 换脸
        yield return FlickerLightsThenFace();

        if (dialogueAfterFacesDelay > 0f)
            yield return new WaitForSeconds(dialogueAfterFacesDelay);

        dialogueTrigger2?.TriggerDialogue();

        _eventRoutine = null;
    }

    // ========= 工具：移动到目标点 =========
    private IEnumerator MoveObjectTo(Transform t, Vector3 targetPos, float speed, bool lockY)
    {
        if (!t) yield break;

        // 保持 Y 不变（可选）
        if (lockY) targetPos.y = t.position.y;

        // 如果有 confiner 且是 PolygonCollider2D，先把目标点夹进边界
        PolygonCollider2D poly = null;
        var conf = FindObjectOfType<Cinemachine.CinemachineConfiner2D>();
        if (conf) poly = conf.m_BoundingShape2D as PolygonCollider2D;
        if (poly) targetPos = poly.ClosestPoint(targetPos);

        // 循环推进
        while ((t.position - targetPos).sqrMagnitude > 0.0004f)
        {
            // 计算下一步位置
            Vector3 next = Vector3.MoveTowards(t.position, targetPos, speed * Time.deltaTime);

            // 每一步也 clamp 一下，防止穿出边界被别的脚本拉回
            if (poly) next = (Vector3)poly.ClosestPoint(next);

            if (_playerRb) _playerRb.MovePosition(next);   // ★ 推荐：刚体移动
            else t.position = next;                        // 兜底：直接改 transform

            yield return null;
        }

        if (_playerRb) _playerRb.MovePosition(targetPos);
        else t.position = targetPos;
    }

    // ========= 工具：抛物线跳跃 =========
    private IEnumerator JumpObjectTo(Transform t, Vector3 targetPos, float duration, float height, AnimationCurve arc)
    {
        if (!t) yield break;

        Vector3 start = t.position;
        float timer = 0f;
        if (arc == null || arc.length == 0) arc = AnimationCurve.EaseInOut(0, 0, 1, 1);

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float u = Mathf.Clamp01(timer / duration);

            // 水平插值
            Vector3 pos = Vector3.Lerp(start, targetPos, u);
            // 垂直抬升（抛物线）
            float yOffset = arc.Evaluate(u) * height;
            pos.y = Mathf.Lerp(start.y, targetPos.y, u) + yOffset;

            t.position = pos;
            yield return null;
        }
        t.position = targetPos;
    }

    // ========= 工具：闪灯 + 统一换脸 =========
    private IEnumerator FlickerLightsThenFace()
    {
        // 用 sceneLights；如果没填，就用主 lightToBlinkAndDim
        var lights = new List<URPLight2D>();
        if (sceneLights != null && sceneLights.Count > 0) lights.AddRange(sceneLights);
        else if (lightToBlinkAndDim) lights.Add(lightToBlinkAndDim);

        if (lights.Count == 0) yield break;

        // 记录原强度
        var original = new float[lights.Count];
        for (int i = 0; i < lights.Count; i++) original[i] = lights[i] ? lights[i].intensity : 1f;

        for (int k = 0; k < flickerCount; k++)
        {
            // off
            for (int i = 0; i < lights.Count; i++) if (lights[i]) lights[i].intensity = 0f;
            yield return new WaitForSeconds(flickerOffTime);

            // on
            for (int i = 0; i < lights.Count; i++) if (lights[i]) lights[i].intensity = original[i];
            yield return new WaitForSeconds(flickerOnTime);
        }

        // 最后一次亮起后延迟 → 换脸
        if (faceDelayAfterLastOn > 0f) yield return new WaitForSeconds(faceDelayAfterLastOn);

        for (int i = 0; i < faceTargets.Count; i++)
        {
            var ft = faceTargets[i];
            if (ft.renderer && ft.faceFrontSmile)
            {
                ft.renderer.sprite = ft.faceFrontSmile;
            }
        }
    }
}
