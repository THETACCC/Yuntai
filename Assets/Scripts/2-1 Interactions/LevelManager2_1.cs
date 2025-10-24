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
    [HideInInspector] public bool infoPassA_2 = false;  //乞巧
    [HideInInspector] public bool infoPassA_3 = false; //封城
    [HideInInspector] public bool infoThree = false; // ZhouShu服务员不过来
    [HideInInspector] public bool infoFour = false;  // 乘务员为什么站在我座椅边上。

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

    public GameObject ZhouShu;

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

    [SerializeField] private GameObject ZhoushuStanding;
    [SerializeField] private GameObject ZhoushuSitting;
    [SerializeField] private DialogueTrigger zhoushuDialogue_Post;
    [SerializeField] private UI_E zhoushuUIE;

    [HideInInspector] public bool isPassA_Conv1 = false;
    [HideInInspector] public bool isPassA_Conv2 = false;
    [HideInInspector] public bool isPassA_Conv3 = false;

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

        // 可选：初始化周叔为“坐着”
        if (ZhoushuStanding) ZhoushuStanding.SetActive(false);
        if (ZhoushuSitting) ZhoushuSitting.SetActive(true);
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

    public void SetInfoOneTrue() { if (!infoPassA_1) { infoPassA_1 = true; isStewardess_AllConv = false; isStewardessSet = false; } AllStewardessConversationCheck(); }
    public void SetInfoTwoTrue() { if (!infoPassA_2) { infoPassA_2 = true; isStewardess_AllConv = false; isStewardessSet = false; } AllStewardessConversationCheck(); }
    public void SetInfoThreeTrue() { if (!infoPassA_3) { infoPassA_3 = true; isStewardess_AllConv = false; isStewardessSet = false; } AllStewardessConversationCheck(); }
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
        // 可按需切换周叔拿到食物的表现
    }

    public void SetStewardessConv1() { isStewardess_Conv1 = true; AllStewardessConversationCheck(); }
    public void SetStewardessConv2() { isStewardess_Conv2 = true; AllStewardessConversationCheck(); }
    public void SetStewardessConv3() { isStewardess_Conv3 = true; AllStewardessConversationCheck(); }
    public void SetStewardessConv4() { isStewardess_Conv4 = true; AllStewardessConversationCheck(); }

    public void SetZhouShuConv5() { isZhouShu_Conv5 = true; IsAllZhouShuConv(); }
    public void SetZhouShuConv6() { isZhouShu_Conv6 = true; IsAllZhouShuConv(); }
    public void SetZhouShuConv7() { isZhouShu_Conv7 = true; IsAllZhouShuConv(); }

    public void SetPassAConv1() { isPassA_Conv1 = true; }
    public void SetPassAConv2() { isPassA_Conv2 = true; }
    public void SetPassAConv3() { isPassA_Conv3 = true; }

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
    public void ChangeStewardessToGotFood()
    {
        if (Stewardess_Food) Stewardess_Food.SetActive(false);
        if (Stewardess_AlreadyGotFood) Stewardess_AlreadyGotFood.SetActive(true);
    }

    // ===== 灯光流程：闪几次 → 变暗（非黑） → 切周叔站立 → 关闭UI → 恢复控制 → 触发对话 =====
    public void BlinkThenDim()
    {
        if (_blinkRoutine != null || !mainLight) return;

        var steps = new (float cycle, float on, float minI, float maxI)[]
        {
            (1.5f, 1.2f, 0.0f, 0.9f),
            (1.0f, 0.8f, 0.0f, 0.9f),
            (1.8f, 1.5f, 0.0f, 0.9f),
        };

        _blinkRoutine = StartCoroutine(CoRun());

        IEnumerator CoRun()
        {
            // 1) 禁走
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

            // 2) 灯光：闪几次 → 变暗（非黑，软边）
            yield return LightControl.BlinkThenDimIE(
                mainLight,
                steps,
                dimTarget: 0.2f,
                dimDuration: 1.0f,
                edge: 0.08f,
                minFloor: 0.02f
            );

            // 3) Zhoushu起身
            if (ZhoushuStanding) ZhoushuStanding.SetActive(true);
            if (ZhoushuSitting) ZhoushuSitting.SetActive(false);

            // 4) 关闭Zhoushu的交互
            if (zhoushuUIE) zhoushuUIE.enabled = false;

            // 5) 恢复原状态
            foreach (var s in _ctrlSnaps)
            {
                if (s.rb) s.rb.isKinematic = s.wasKinematic;
                if (s.ctrl) s.ctrl.enabled = s.wasEnabled;
            }
            _ctrlSnaps.Clear();

            // 6) 触发后续对话
            if (zhoushuDialogue_Post) zhoushuDialogue_Post.TriggerDialogue();

            _blinkRoutine = null;
        }
    }

    public void WorkerChangeAndOpenTrigger()
    {
        worker1.SetActive(false);
        worker2.SetActive(true);
        Trigger10.SetActive(true);

        // 这里仍然只是设置传送门的目标；真正切场景请用 ToNextLoop
        BathroomPortal.scenename = "Level2-2";
    }

    // ========= 统一：通过 ToNextLoop 切场景 =========

    /// <summary>
    /// 跳转到“未逃脱”版本（例如回到 2-1 后续/或去别的关卡）
    /// </summary>
    public void Goto_NotEscaped_ViaNextLoop()
    {
        if (!nextLoop) return;
        nextLoop.scenename = SceneName_NotEscaped;
        nextLoop.SpawnPointLocation = SpawnPointLocation_NotEscaped;
        nextLoop.toNextLoop();
    }

    /// <summary>
    /// 跳转到“已逃脱/去 2-2”等（按你项目设定）
    /// </summary>
    public void Goto_Escaped_ViaNextLoop()
    {
        if (!nextLoop) return;
        nextLoop.scenename = SceneName_Escaped;
        nextLoop.SpawnPointLocation = SpawnPointLocation_Escaped;
        nextLoop.toNextLoop();
    }

    /// <summary>
    /// 自定义指定场景/出生点，通过 ToNextLoop 进行（用于 Timeline / 对话回调）
    /// </summary>
    public void Goto_Custom_ViaNextLoop(string scene, int spawn)
    {
        if (!nextLoop) return;
        nextLoop.scenename = scene;
        nextLoop.SpawnPointLocation = spawn;
        nextLoop.toNextLoop();
    }

    /// <summary>
    /// 典型收尾：例如所有对话结束后，播过场再去 2-2（或 Escaped）
    /// 把这个函数从你的 Dialogue 事件里直接调用即可。
    /// </summary>
    public void End2_1_And_GotoNext()
    {
        if (!nextLoop) return;

        // 这里按你的条件决定去哪个目标（示例：收齐信息 & 全对话完成 → Escaped）
        bool canExitToNext = isStewardess_AllConv && HaveAllInfo();
        string targetScene = canExitToNext ? SceneName_Escaped : SceneName_NotEscaped;
        int targetSpawn = canExitToNext ? SpawnPointLocation_Escaped : SpawnPointLocation_NotEscaped;

        nextLoop.scenename = targetScene;
        nextLoop.SpawnPointLocation = targetSpawn;

        // 是否播过场
        if (useDeathCutForExit)
            nextLoop.toNextLoop();
        else
        {
            // 如果你想不走 VideoPlayer，直接跳（保留统一入口也行）
            LoopTracker.I?.IncrementLoop();
            SceneController.instance.LoadSceneAndTeleport(targetScene, targetSpawn);
        }
    }
}
