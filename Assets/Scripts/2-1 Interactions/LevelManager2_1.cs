using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using URPLight2D = UnityEngine.Rendering.Universal.Light2D;

[RequireComponent(typeof(ToNextLoop))]   // 确保同物体上有 ToNextLoop
public class LevelManager2_1 : MonoBehaviour
{
    [SerializeField] private URPLight2D mainLight;
    private Coroutine _blinkRoutine;

    [SerializeField] private int myLoop = 2;
    public bool isPlayerEscaped = false;

    // Scene Related
    public string SceneName_NotEscaped;
    public int SpawnPointLocation_NotEscaped;

    public string SceneName_Escaped;
    public int SpawnPointLocation_Escaped;

    public ScenePortal BathroomPortal;

    [SerializeField] private GameObject worker1;
    [SerializeField] private GameObject worker2;
    [SerializeField] private GameObject Trigger10;

    // —— ToNextLoop 对接 —— 
    [Header("NextLoop (放在同一物体上)")]
    [SerializeField] private ToNextLoop nextLoop;   // 可空，Start 里自动 GetComponent
    [Tooltip("2-1结束时是否播放过场（由 ToNextLoop 播放）再跳转")]
    public bool useDeathCutForExit = true;

    // Dialogue Boolean
    [HideInInspector] public bool infoPassA_1 = false;   // PassA 团
    [HideInInspector] public bool infoPassA_2 = false;   // 乞巧
    [HideInInspector] public bool infoPassA_3 = false;   // 封城
    [HideInInspector] public bool infoThree = false;     // ZhouShu服务员不过来
    [HideInInspector] public bool infoFour = false;      // 乘务员为什么站在我座椅边上。

    [HideInInspector] public bool infoFive = false;
    [HideInInspector] public bool infoSix = false;
    [HideInInspector] public bool infoSeven = false;

    [HideInInspector] public bool getFood = false; // 找乘务员拿到food
    [HideInInspector] public bool gotFood = false; // zhoushu拿到food

    // Playthrough Related
    [Header("Playthrough Related")]
    public GameObject Stewardess;
    public GameObject Stewardess_Food;
    public GameObject Stewardess_AlreadyGotFood;

    // Conversation Related
    [HideInInspector] public bool isStewardess_Conv1 = false;
    [HideInInspector] public bool isStewardess_Conv2 = false;
    [HideInInspector] public bool isStewardess_Conv3 = false;
    [HideInInspector] public bool isStewardess_Conv4 = false;
    [HideInInspector] public bool isStewardess_AllConv = false;
    [HideInInspector] public bool isStewardessSet = false;

    [HideInInspector] public bool isZhouShu = false;
    [HideInInspector] public bool isZhouShu_Conv5 = false;
    [HideInInspector] public bool isZhouShu_Conv6 = false;
    [HideInInspector] public bool isZhouShu_Conv7 = false;
    [HideInInspector] public bool isZhouShu_AllConv = false;

    [SerializeField] private GameObject ZhoushuHungry;
    [SerializeField] private GameObject ZhoushuStanding;
    [SerializeField] private GameObject ZhoushuSitting;
    [SerializeField] private UI_E zhoushuUIE;
    [SerializeField] private GameObject RightBlock;
    [SerializeField] private GameObject Bathroom_Old;
    [SerializeField] private GameObject Bathroom_Escaped;

    // —— 周Shu站起后触发的对话（优先）——
    [Header("Dialogue After Stand")]
    [SerializeField] private DialogueTrigger Dialogue8_1;

    // ★ 灯集合策略：控制所有 URP 2D 灯（无需 Tag）或仅 Tag=Light
    [Header("Light Collection")]
    [SerializeField] private bool controlAllUrp2DLights = true;
    private static readonly string LightTag = "Light";

    // ★ 统一三连闪参数（总共 3 下，匀速中等节奏）
    [Header("Uniform Triple Flicker")]
    [SerializeField, Min(1)] private int uniformFlickerCount = 3;   // 总次数，给 3
    [SerializeField, Min(0f)] private float uniformOffTime = 0.56f; // 每次熄灭时长
    [SerializeField, Min(0f)] private float uniformOnTime = 0.56f; // 每次点亮时长
    [SerializeField, Range(0f, 1f)] private float offIntensity = 0f; // 熄灭到的强度（0=全黑）

    // —— 控制器快照，用于“禁-恢复” —— 
    private struct CtrlSnap
    {
        public PlayerController ctrl;
        public Rigidbody2D rb;
        public bool wasEnabled;
        public bool wasKinematic;
    }
    private readonly List<CtrlSnap> _ctrlSnaps = new List<CtrlSnap>();

    public void SpeakZhoushu()
    {
        isZhouShu = true;
        if (BathroomPortal)
        {
            BathroomPortal.scenename = SceneName_Escaped;
            BathroomPortal.SpawnPointLocation = SpawnPointLocation_Escaped;
        }
    }

    public void Start()
    {
        // 绑定 NextLoop
        if (!nextLoop) nextLoop = GetComponent<ToNextLoop>();

        LoopTracker.I?.SetLoop(myLoop);
        if (BathroomPortal)
        {
            BathroomPortal.scenename = SceneName_NotEscaped;
            BathroomPortal.SpawnPointLocation = SpawnPointLocation_NotEscaped;
        }

        // 初始化周叔为“坐着”
        if (ZhoushuStanding) ZhoushuStanding.SetActive(false);
        if (ZhoushuSitting) ZhoushuSitting.SetActive(false);
    }

    // 全部 info 是否已收集
    private bool HaveAllInfo() => infoPassA_1 && infoPassA_2 && infoPassA_3 && infoFour;

    // 当前是否还有“可对话但未完成”的项
    private bool AnyPendingConversation() =>
        (infoPassA_1 && !isStewardess_Conv1) ||
        (infoPassA_2 && !isStewardess_Conv2) ||
        (infoPassA_3 && !isStewardess_Conv3) ||
        (infoFour && !isStewardess_Conv4);

    public void AllStewardessConversationCheck()
    {
        bool nonePending = !AnyPendingConversation();
        isStewardess_AllConv = nonePending;
        isStewardessSet = nonePending && HaveAllInfo();
    }

    public void SetInfoPass1True() { if (!infoPassA_1) { infoPassA_1 = true; isStewardess_AllConv = false; isStewardessSet = false; } AllStewardessConversationCheck(); }
    public void SetInfoPass2True() { if (!infoPassA_2) { infoPassA_2 = true; isStewardess_AllConv = false; isStewardessSet = false; } AllStewardessConversationCheck(); }
    public void SetInfoPass3True() { if (!infoPassA_3) { infoPassA_3 = true; isStewardess_AllConv = false; isStewardessSet = false; } AllStewardessConversationCheck(); }
    public void SetInfoThreeTrue() { if (!infoThree) { infoFour = true; isStewardess_AllConv = false; isStewardessSet = false; } AllStewardessConversationCheck(); } // 保留原逻辑
    public void SetInfoFourTrue() { if (!infoFour) { infoFour = true; isStewardess_AllConv = false; isStewardessSet = false; } AllStewardessConversationCheck(); }
    public void SetInfoFiveTrue() { infoFive = true; }
    public void SetInfoSixTrue() { infoSix = true; }
    public void SetInfoSevenTrue() { infoSeven = true; }

    public void SetgetFoodTrue()
    {
        getFood = true;
        if (Stewardess) Stewardess.SetActive(false);
        if (Stewardess_Food) Stewardess_Food.SetActive(true);
    }

    public void SetGotFoodTrue()
    {
        gotFood = true;
    }

    public void SetStewardessConv1() { isStewardess_Conv1 = true; AllStewardessConversationCheck(); }
    public void SetStewardessConv2() { isStewardess_Conv2 = true; AllStewardessConversationCheck(); }
    public void SetStewardessConv3() { isStewardess_Conv3 = true; AllStewardessConversationCheck(); }
    public void SetStewardessConv4() { isStewardess_Conv4 = true; AllStewardessConversationCheck(); }

    public void SetZhouShuConv5() { isZhouShu_Conv5 = true; IsAllZhouShuConv(); }
    public void SetZhouShuConv6() { isZhouShu_Conv6 = true; IsAllZhouShuConv(); }
    public void SetZhouShuConv7() { isZhouShu_Conv7 = true; IsAllZhouShuConv(); }

    public void IsAllZhouShuConv()
    {
        if (isZhouShu_Conv5 && isZhouShu_Conv6 && isZhouShu_Conv7)
            isZhouShu_AllConv = true;
    }

    public void ChangeStewardessToForFood()
    {
        if (Stewardess) Stewardess.gameObject.SetActive(false);
        if (Stewardess_Food) Stewardess_Food.gameObject.SetActive(true);
    }
    public void ChangeStewardessAndZhouShuToGotFood()
    {
        if (Stewardess_Food) Stewardess_Food.SetActive(false);
        if (Stewardess_AlreadyGotFood) Stewardess_AlreadyGotFood.SetActive(true);

        if (ZhoushuHungry) ZhoushuHungry.SetActive(false);
        if (ZhoushuSitting) ZhoushuSitting.SetActive(true);
    }

    // ===== 只做一次统一三连闪（总共 3 下）→ 周叔站立 → 对话 =====
    public void BlinkThenDim()   // 名称沿用，但已按需求改成“仅 3 下统一闪烁”
    {
        if (_blinkRoutine != null) return;
        _blinkRoutine = StartCoroutine(CoRun());

        IEnumerator CoRun()
        {
            // 收集灯：更保险（可选 Tag 过滤 / 控制全场），并强制包含 mainLight
            var lights = GetSceneLights(mainLight);

            // 1) 禁走
            FreezePlayers();

            // 2) 统一三连闪（总共 3 次，匀速中等节奏），无额外渐暗、无二段闪
            yield return UniformFlicker_All(lights, uniformFlickerCount, uniformOffTime, uniformOnTime, offIntensity);

            // 3) Zhoushu起身
            if (ZhoushuStanding) ZhoushuStanding.SetActive(true);
            if (ZhoushuSitting) ZhoushuSitting.SetActive(false);

            // 4) 关闭Zhoushu的交互
            if (zhoushuUIE) zhoushuUIE.enabled = false;

            // 5) 恢复原状态
            RestorePlayers();

            // 6) 触发对话（优先 Dialogue8_1，其次回退 zhoushuDialogue_Post）
            if (Dialogue8_1) Dialogue8_1.TriggerDialogue();

            _blinkRoutine = null;
        }
    }

    // —— 工具：整组灯的统一三连闪（或 N 连闪），每次灭到 offIntensity、亮回到各自“基准强度” —— 
    private IEnumerator UniformFlicker_All(List<URPLight2D> lights, int count, float offTime, float onTime, float offI)
    {
        if (lights == null || lights.Count == 0 || count <= 0) yield break;

        // 记录开始时各灯的基准强度
        var baseIntensity = new float[lights.Count];
        for (int i = 0; i < lights.Count; i++)
            baseIntensity[i] = lights[i] ? lights[i].intensity : 0f;

        for (int k = 0; k < count; k++)
        {
            // 熄灭到 offIntensity
            for (int i = 0; i < lights.Count; i++)
                if (lights[i]) lights[i].intensity = offI;
            if (offTime > 0f) yield return new WaitForSeconds(offTime);

            // 亮回“基准强度”
            for (int i = 0; i < lights.Count; i++)
                if (lights[i]) lights[i].intensity = baseIntensity[i];
            if (onTime > 0f) yield return new WaitForSeconds(onTime);
        }
    }

    // —— 玩家冻结/恢复 —— 
    private void FreezePlayers()
    {
        _ctrlSnaps.Clear();
        var controllers = Object.FindObjectsOfType<PlayerController>(true);
        foreach (var c in controllers)
        {
            if (!c) continue;
            var rb = c.GetComponent<Rigidbody2D>();
            _ctrlSnaps.Add(new CtrlSnap
            {
                ctrl = c,
                rb = rb,
                wasEnabled = c.enabled,
                wasKinematic = rb ? rb.isKinematic : false
            });
            if (rb) { rb.velocity = Vector2.zero; rb.isKinematic = true; }
            c.enabled = false;
        }
    }
    private void RestorePlayers()
    {
        foreach (var s in _ctrlSnaps)
        {
            if (s.rb) s.rb.isKinematic = s.wasKinematic;
            if (s.ctrl) s.ctrl.enabled = s.wasEnabled;
        }
        _ctrlSnaps.Clear();
    }

    // —— 收集 URP 2D 灯（含 inactive）。可选仅 Tag=Light；强制包含 mainLight —— 
    private List<URPLight2D> GetSceneLights(URPLight2D extraInclude = null)
    {
        var set = new HashSet<URPLight2D>();

        var all = Object.FindObjectsOfType<URPLight2D>(includeInactive: true);
        foreach (var l in all)
        {
            if (!l) continue;
            if (controlAllUrp2DLights)
            {
                set.Add(l);  // 控制所有 URP 2D 灯
            }
            else
            {
                if (l.gameObject.CompareTag(LightTag)) set.Add(l); // 只控制 Tag=Light
            }
        }
        if (extraInclude) set.Add(extraInclude);

#if UNITY_EDITOR
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[LevelManager2_1] Lights to flicker ({set.Count}) (controlAllUrp2DLights={controlAllUrp2DLights}):");
        foreach (var l in set)
        {
            if (!l) continue;
            var go = l.gameObject;
            sb.AppendLine($" - {go.name} | tag={go.tag} | activeInHierarchy={go.activeInHierarchy} | intensity={l.intensity}");
        }
        Debug.Log(sb.ToString());
#endif

        return new List<URPLight2D>(set);
    }

    public void WorkerChangeAndOpenTrigger()
    {
        worker1.SetActive(false);
        worker2.SetActive(true);
        Trigger10.SetActive(true);
        BathroomPortal.scenename = "Level2-2";
    }

    // ========= 统一：通过 ToNextLoop 切场景 =========

    public void Goto_NotEscaped_ViaNextLoop()
    {
        if (!nextLoop) return;
        nextLoop.scenename = SceneName_NotEscaped;
        nextLoop.SpawnPointLocation = SpawnPointLocation_NotEscaped;
        nextLoop.toNextLoop();
    }

    public void Goto_Escaped_ViaNextLoop()
    {
        if (!nextLoop) return;
        nextLoop.scenename = SceneName_Escaped;
        nextLoop.SpawnPointLocation = SpawnPointLocation_Escaped;
        nextLoop.toNextLoop();
    }

    public void Goto_Custom_ViaNextLoop(string scene, int spawn)
    {
        if (!nextLoop) return;
        nextLoop.scenename = scene;
        nextLoop.SpawnPointLocation = spawn;
        nextLoop.toNextLoop();
    }

    public void End2_1_And_GotoNext()
    {
        if (!nextLoop) return;

        bool canExitToNext = isStewardess_AllConv && HaveAllInfo();
        string targetScene = canExitToNext ? SceneName_Escaped : SceneName_NotEscaped;
        int targetSpawn = canExitToNext ? SpawnPointLocation_Escaped : SpawnPointLocation_NotEscaped;

        nextLoop.scenename = targetScene;
        nextLoop.SpawnPointLocation = targetSpawn;

        if (useDeathCutForExit)
            nextLoop.toNextLoop();
        else
        {
            LoopTracker.I?.IncrementLoop();
            SceneController.instance.LoadSceneAndTeleport(targetScene, targetSpawn);
        }
    }

    public void ZhouShuReadyToFight()
    {
        RightBlock.SetActive(true);
        Bathroom_Old.SetActive(false);
        Bathroom_Escaped.SetActive(true);
    }
}
