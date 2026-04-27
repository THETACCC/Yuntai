using UnityEngine;

/// <summary>
/// 灭火器互动对话：三段递进 + 后续在 dialogue2 / dialogue3 间循环。
/// 进度全局共享（跨场景、跨实例），通过 static 字段持久化在内存中。
/// 若需要写入存档，由 SaveManager 读写 <see cref="GlobalProgress"/>。
/// </summary>
public class FireExtinguisherDialogue : MonoBehaviour
{
    [Header("Dialogue Files")]
    public TextAsset dialogue1;
    public TextAsset dialogue2;
    public TextAsset dialogue3;

    [Header("UI")]
    [Tooltip("玩家进入触发范围时显示的“按 E 互动”提示子物体")]
    public GameObject InteractIndicator;

    /// <summary>
    /// 全局进度（所有灭火器共享）。
    /// 0: 还没看过 dialogue1
    /// 1: 看过 1，下次播 2
    /// 2: 看过 2，下次播 3
    /// 3+: 三段都看过，进入循环（在 2/3 之间切换）
    /// </summary>
    public static int GlobalProgress { get; set; } = 0;

    private static bool loopShowDialogue3 = true;

    private bool isReadyToTrigger = false;

    private void Start()
    {
        if (InteractIndicator != null)
            InteractIndicator.SetActive(false);
    }

    private void Update()
    {
        if (isReadyToTrigger && Input.GetKeyDown(KeyCode.E))
        {
            TriggerDialogue();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isReadyToTrigger = true;
            if (InteractIndicator != null)
                InteractIndicator.SetActive(true);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isReadyToTrigger = false;
            if (InteractIndicator != null)
                InteractIndicator.SetActive(false);
        }
    }

    private void TriggerDialogue()
    {
        if (DialogueController.instance.isDialogueActive) return;

        TextAsset toPlay = PickDialogue();
        if (toPlay == null)
        {
            Debug.LogWarning($"[FireExtinguisherDialogue] No dialogue assigned for progress={GlobalProgress} on {gameObject.name}");
            return;
        }

        if (InteractIndicator != null)
            InteractIndicator.SetActive(false);

        Gamemanager.instance?.StartDialogue();
        DialogueController.instance.LoadDialogueFromFile(toPlay);
        DialogueController.instance.StartDialogue();

        AdvanceProgress();
    }

    private TextAsset PickDialogue()
    {
        switch (GlobalProgress)
        {
            case 0: return dialogue1;
            case 1: return dialogue2;
            case 2: return dialogue3;
            default:
                // 循环阶段：在 dialogue2 / dialogue3 之间切换
                return loopShowDialogue3 ? dialogue3 : dialogue2;
        }
    }

    private void AdvanceProgress()
    {
        if (GlobalProgress < 3)
            GlobalProgress++;
        else
            loopShowDialogue3 = !loopShowDialogue3;
    }
}
