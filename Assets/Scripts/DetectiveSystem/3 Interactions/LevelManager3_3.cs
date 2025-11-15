using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using URPLight2D = UnityEngine.Rendering.Universal.Light2D;

public class LevelManager3_3 : MonoBehaviour
{
    [Header("Lantern Stall")]
    [SerializeField] private GameObject LanternStall;
    [SerializeField] private GameObject LanternStall_wait;
    [SerializeField] private GameObject LanternStall_wrong;
    [SerializeField] private GameObject LanternStall_correct;

    [SerializeField] private GameObject LanternPile;
    [SerializeField] private GameObject Lanterns;   // 挂好了的那一排 lantern 组

    [Header("Puzzle")]
    [SerializeField] private PuzzleLanternManager puzzleLanternManager;
    [SerializeField] private Collider2D puzzleLanternCollider;

    private void Awake()
    {
        // 兜底：如果忘记拖引用，就自动找场上的 PuzzleLanternManager
        if (!puzzleLanternManager)
            puzzleLanternManager = FindObjectOfType<PuzzleLanternManager>();
    }

    private void Start()
    {
        // 可以根据需要初始化摊位显示状态
        // 比如：最开始只显示 LanternStall，其它关掉
        // LanternStall.SetActive(true);
        // LanternStall_wait.SetActive(false);
        // LanternStall_wrong.SetActive(false);
        // LanternStall_correct.SetActive(false);
    }

    /// <summary>
    /// 比如玩家拿到灯笼之后，改成等待状态
    /// </summary>
    public void ChangeLanternToWait()
    {
        LanternStall.SetActive(false);
        LanternStall_wait.SetActive(true);
        LanternStall_wrong.SetActive(false);
        LanternStall_correct.SetActive(false);
    }

    /// <summary>
    /// 被 PuzzleLantern 调用：每次挂/取灯笼之后检查一次
    /// </summary>
    public void CheckIfLanternCorrect()
    {
        if (!puzzleLanternManager)
        {
            Debug.LogError("[LevelManager3_3] puzzleLanternManager is NULL, cannot check.", this);
            return;
        }

        // 为了避免同一帧里顺序问题，先强制重算一次
        puzzleLanternManager.ForceRecheck();
        Debug.Log($"[LevelManager3_3] CheckIfLanternCorrect, Solved = {puzzleLanternManager.Solved}");

        if (puzzleLanternManager.Solved)
        {
            Debug.Log("[LevelManager3_3] Lantern puzzle SOLVED!");

            LanternStall.SetActive(false);
            LanternStall_wait.SetActive(false);
            LanternStall_wrong.SetActive(false);
            LanternStall_correct.SetActive(true);

            if (puzzleLanternCollider)
                puzzleLanternCollider.enabled = false;
        }
        else
        {
            Debug.Log("[LevelManager3_3] Lantern puzzle NOT solved!");

            LanternStall.SetActive(false);
            LanternStall_wait.SetActive(false);
            LanternStall_correct.SetActive(false);
            LanternStall_wrong.SetActive(true);
        }
    }

    /// <summary>
    /// 灯笼 pile 消失，真正的灯笼出现
    /// （可以在解出谜题 / 某个对话之后调用）
    /// </summary>
    public void LanternPileDisappear()
    {
        if (LanternPile) LanternPile.SetActive(false);
        if (Lanterns) Lanterns.SetActive(true);
    }

}
