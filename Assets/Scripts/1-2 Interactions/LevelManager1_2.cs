using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.VisualScripting;
using URPLight2D = UnityEngine.Rendering.Universal.Light2D;

public class LevelManager1_2 : BaseLevelManager
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
    [SerializeField] private List<URPLight2D> sceneLights = new();    // 需要一起闪烁的灯（可为空则用 lightToBlinkAndDim）

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
    [SerializeField] private AudioSource snd_JumpScareSound;
    [SerializeField] private AudioSource snd_HorrorTextureOneShot;
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

    // runtime
    private Coroutine _eventRoutine;
    private static float s_MinOnIntensityOverride = 0f;

    // ---------------- Awake / Start ----------------

    protected override void Awake()
    {
        // 这个关卡：一进来就把玩家隐藏+锁住
        hidePlayerOnSceneStart = true;
        lockPlayerOnSceneStart = true;

        base.Awake();   // ⭐ 一定要保留

        if (!dialogueTrigger)
            Debug.LogWarning("[LevelManager1_2] No dialogue trigger!!");
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

    // ---------------- 开场灯光 + 厕所门 ----------------

    private IEnumerator RunLightsThenRevealPlayer()
    {
        // 0) 黑屏 2.5 秒
        lightToBlinkAndDim.intensity = 0f;
        yield return new WaitForSeconds(2.5f);

        // 1) 连续闪烁 4~5 秒
        var step = (blinkPattern != null && blinkPattern.Count > 0)
            ? blinkPattern[0]
            : new BlinkStep { cycle = 1.0f, onTime = 0.20f, minIntensity = 0.25f, maxIntensity = 0.9f };

        LightControl.StartBlink(lightToBlinkAndDim, step.cycle, step.onTime, step.minIntensity, step.maxIntensity);
        yield return new WaitForSeconds(Random.Range(4f, 5f));
        LightControl.StopBlink(lightToBlinkAndDim);

        // 2) global light Dim
        LightControl.Dim(lightToBlinkAndDim, dimTargetIntensity, dimDuration);
        yield return new WaitForSeconds(dimDuration);

        yield return new WaitForSeconds(1.3f);

        // 3) 红光亮
        var sr = BathroomSign ? BathroomSign.GetComponent<SpriteRenderer>() : null;
        if (sr && Sign_on) sr.sprite = Sign_on;
        if (redLightObject) redLightObject.SetActive(true);
        if (snd_RedLight) snd_RedLight.Play();

        // 4) 等 2 秒
        yield return new WaitForSeconds(2f);

        // 5) 厕所音效
        if (snd_toilet)
        {
            snd_toilet.Play();
            if (snd_toilet.clip) yield return new WaitForSeconds(snd_toilet.clip.length);
            else while (snd_toilet.isPlaying) yield return null;
        }

        // 6) 再等 2 秒
        yield return new WaitForSeconds(2f);

        // 7) 角色出现
        if (snd_toiletDoor) snd_toiletDoor.Play();
        RevealPlayerSprites();   // ⭐ 用 BaseLevelManager 的接口

        // 8) 等 0.7 秒 → 对话框
        yield return new WaitForSeconds(0.7f);

        if (objectToReveal && !objectToReveal.activeSelf)
            objectToReveal.SetActive(true);

        if (dialogueTrigger)
            dialogueTrigger.TriggerDialogue();

        // 如果你想对话完后就能动，可以：
        // EnablePlayerMovement();
    }

    // ---------------- 5A 演绎 ----------------

    public void DoSpecialThingWhenInBound1()
    {
        if (_eventRoutine != null) return;   // 防重入
        _eventRoutine = StartCoroutine(EventSequence_BoyBlocksAndSmile());
    }

    private IEnumerator EventSequence_BoyBlocksAndSmile()
    {
        if (Gamemanager.instance) Gamemanager.instance.phase = GamePhase.Eventing;

        // 停掉玩家控制
        DisablePlayerMovement();   // ⭐ 调 BaseLevelManager，一起锁 phase + control

        // 再加上刚体锁（防止飘动）
        if (playerRb)
        {
            playerRb.velocity = Vector2.zero;
            playerRb.isKinematic = true;
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
        if (dialogueTrigger2)
            dialogueTrigger2.TriggerDialogue();

        _eventRoutine = null;
    }

    // ---------------- 5B 演绎 ----------------

    public void DoSequence5B()
    {
        if (_eventRoutine != null) return;   // 防重入
        _eventRoutine = StartCoroutine(EventSequence_5B());
    }

    private IEnumerator EventSequence_5B()
    {
        if (Gamemanager.instance) Gamemanager.instance.phase = GamePhase.Eventing;

        // 禁用玩家控制
        DisablePlayerMovement();

        // 清速度 + kinematic
        if (playerRb)
        {
            playerRb.velocity = Vector2.zero;
            playerRb.isKinematic = true;
        }

        // 瞬移：女主放到左侧站位点的 X
        if (playerObject && heroineStandLeftX)
        {
            var p = playerObject.transform.position;
            var snap = new Vector3(heroineStandLeftX.position.x, p.y, p.z);
            playerObject.transform.position = snap;
            if (playerRb)
            {
                playerRb.position = snap;
                playerRb.velocity = Vector2.zero;
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

        // 让 5B 的“亮回去”至少有可见强度
        s_MinOnIntensityOverride = minVisibleIntensity5B;

        // Step 1
        if (snd_breakBones) snd_breakBones.Play();
        if (snd_JumpScareSound) snd_JumpScareSound.Play();
        
        yield return PulseOnce(
            lights, original,
            flickerOffTime, flickerOnTime,
            () =>
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
            flickerOffTime, flickerOnTime,
            () =>
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
            flickerOffTime, flickerOnTime,
            () =>
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

        // 统一转头微笑
        if (faceDelayAfterLastOn > 0f) yield return new WaitForSeconds(faceDelayAfterLastOn);
        for (int i = 0; i < lights.Count; i++)
            if (lights[i]) lights[i].intensity = Mathf.Max(original[i], minVisibleIntensity5B);

        for (int i = 0; i < faceTargets.Count; i++)
        {
            var ft = faceTargets[i];
            if (ft.renderer && ft.faceFrontSmile)
                ft.renderer.sprite = ft.faceFrontSmile;
        }

        // 保持亮一小段时间
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
            if(snd_HorrorTextureOneShot) snd_HorrorTextureOneShot.Play();
            if (snd_breakBones3.clip) yield return new WaitForSeconds(snd_breakBones3.clip.length);
            else while (snd_breakBones3.isPlaying) yield return null;
        }

        if (finalBlackExtraHold > 0f) yield return new WaitForSeconds(finalBlackExtraHold);

        RestorePlayerControl();
        _eventRoutine = null;

        s_MinOnIntensityOverride = 0f;   // 还原覆盖

        if (nextLoop)
            nextLoop.toNextLoop();
    }

    // ---------------- 工具：抛物线跳跃 ----------------

    private IEnumerator JumpObjectTo(Transform t, Vector3 targetPos, float duration, float height, AnimationCurve arc)
    {
        if (!t) yield break;

        Vector3 start = t.position;
        float fixedZ = start.z;
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

    // ---------------- 工具：闪灯 + 换脸（5A） ----------------

    private IEnumerator FlickerLightsThenFace()
    {
        var lights = new List<URPLight2D>();
        if (sceneLights != null && sceneLights.Count > 0) lights.AddRange(sceneLights);
        else if (lightToBlinkAndDim) lights.Add(lightToBlinkAndDim);
        if (lights.Count == 0) yield break;

        var original = new float[lights.Count];
        for (int i = 0; i < lights.Count; i++) original[i] = lights[i] ? lights[i].intensity : 1f;

        float prevOverride = s_MinOnIntensityOverride;
        s_MinOnIntensityOverride = 0f; // 5A 不强制最小亮度

        for (int k = 0; k < flickerCount; k++)
        {
            for (int i = 0; i < lights.Count; i++) if (lights[i]) lights[i].intensity = 0f;
            yield return new WaitForSeconds(flickerOffTime);

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

    // ---------------- 工具：一次黑→亮的脉冲（5B） ----------------

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

            // 黑的瞬间执行回调
            onWhileDark?.Invoke();

            // 保持黑
            if (offTime > 0f) yield return new WaitForSeconds(offTime);

            // 亮（保证最小亮度）
            for (int i = 0; i < lights.Count; i++)
                if (lights[i]) lights[i].intensity = Mathf.Max(original[i], s_MinOnIntensityOverride);

            // 保持亮
            if (onTime > 0f) yield return new WaitForSeconds(onTime);
        }
        else
        {
            onWhileDark?.Invoke();
            if (offTime > 0f) yield return new WaitForSeconds(offTime);
            if (onTime > 0f) yield return new WaitForSeconds(onTime);
        }
    }

    // ---------------- 工具：只沿 X 轴移动 ----------------

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
            if (playerRb) playerRb.MovePosition(next);
            else t.position = next;

            yield return null;
        }

        if (playerRb) playerRb.MovePosition(target);
        else t.position = target;
    }

    // ---------------- 工具：恢复玩家控制 ----------------

    private void RestorePlayerControl()
    {
        EnablePlayerMovement();   // ⭐ 调用 BaseLevelManager：phase=Moving + Enable 控制

        if (playerRb)
        {
            playerRb.isKinematic = false;
            playerRb.velocity = Vector2.zero;
        }
    }

    public void ShowPlayerAndAllowMove1_2()
    {
        RevealPlayerSprites();
        EnablePlayerMovement();
    }

}
