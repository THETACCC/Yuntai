using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using URPLight2D = UnityEngine.Rendering.Universal.Light2D;

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

    // Dialogue Boolean
    [HideInInspector] public bool infoOne = false; // PassA
    [HideInInspector] public bool infoTwo = false; // 上班族
    [HideInInspector] public bool infoThree = false; // ZhouShu
    [HideInInspector] public bool infoFour = false; // 乘务员为什么站在我座椅边上。
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
    private bool HaveAllInfo() => infoOne && infoTwo && infoThree && infoFour;

    // 当前是否还有“可对话但未完成”的项
    private bool AnyPendingConversation() =>
        (infoOne && !isStewardess_Conv1) ||
        (infoTwo && !isStewardess_Conv2) ||
        (infoThree && !isStewardess_Conv3) ||
        (infoFour && !isStewardess_Conv4);

    public void AllStewardessConversationCheck()
    {
        bool nonePending = !AnyPendingConversation();
        isStewardess_AllConv = nonePending;
        isStewardessSet = nonePending && HaveAllInfo();
    }

    public void SetInfoOneTrue() { if (!infoOne) { infoOne = true; isStewardess_AllConv = false; isStewardessSet = false; } AllStewardessConversationCheck(); }
    public void SetInfoTwoTrue() { if (!infoTwo) { infoTwo = true; isStewardess_AllConv = false; isStewardessSet = false; } AllStewardessConversationCheck(); }
    public void SetInfoThreeTrue() { if (!infoThree) { infoThree = true; isStewardess_AllConv = false; isStewardessSet = false; } AllStewardessConversationCheck(); }
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
        if (Stewardess_Food) Stewardess_Food.gameObject.SetActive(false);
        if (Stewardess_AlreadyGotFood) Stewardess_AlreadyGotFood.gameObject.SetActive(true);
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
    }
}
