using UnityEngine;

/// <summary>
/// 挂在 2D Trigger Collider 上：
/// 玩家进入时，如果 LevelManager3_3.NeedToPlayLanternAnim 为 true，
/// 就播放一次恐怖花灯演出（绿闪 + 野兽 + 抖屏 + 黑屏）。
/// 该标记会在演出内部被清空，下次错误再由对话重新设置为 true。
/// </summary>
public class LanternHorrorTrigger : MonoBehaviour
{
    [SerializeField] private string playerTag = "Player";

    private LevelManager3_3 levelManager;

    private void Awake()
    {
        levelManager = FindObjectOfType<LevelManager3_3>();
        if (!levelManager)
        {
            Debug.LogError("[LanternHorrorTrigger] Cannot find LevelManager3_3 in scene.", this);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;
        if (!levelManager) return;

        if (levelManager.NeedToPlayLanternAnim)
        {
            levelManager.PlayLanternHorrorSequence();
        }
    }
}
