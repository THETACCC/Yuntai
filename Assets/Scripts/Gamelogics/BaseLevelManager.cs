using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 所有关卡 Manager 的基类：统一处理 Player 的查找 / 隐藏 / 锁移动。
/// </summary>
public class BaseLevelManager : MonoBehaviour
{
    [Header("Player Scene Helper")]
    [Tooltip("进入场景时是否自动隐藏玩家 Sprite")]
    [SerializeField] protected bool hidePlayerOnSceneStart = false;

    [Tooltip("进入场景时是否锁住玩家控制（Gamemanager.phase=Loading）")]
    [SerializeField] protected bool lockPlayerOnSceneStart = false;

    // 统一缓存：给子类用
    protected GameObject playerObject;
    protected PlayerController playerCtrl;
    protected Rigidbody2D playerRb;
    protected readonly List<SpriteRenderer> _playerSprites = new();

    /// <summary>
    /// 基类 Awake：自动查找 PlayerController.Instance，按设置隐藏 / 锁定。
    /// 子类如果重写 Awake，一定要记得 base.Awake().
    /// </summary>
    protected virtual void Awake()
    {
        // 1) 锁 GamePhase（统一开场禁止随便乱走）
        if (Gamemanager.instance && lockPlayerOnSceneStart)
        {
            Gamemanager.instance.phase = GamePhase.Loading;
        }

        // 2) 用单例找 Player（避免场景里有奇怪的重复 prefab 被误拿）
        playerCtrl = PlayerController.Instance;
        if (playerCtrl == null)
        {
            Debug.LogWarning("[BaseLevelManager] PlayerController.Instance is null, player not found.");
            return;
        }

        playerObject = playerCtrl.gameObject;
        playerRb = playerCtrl.GetComponent<Rigidbody2D>();

        CachePlayerSprites();

        if (hidePlayerOnSceneStart)
            SetPlayerSpritesVisible(false);

        if (lockPlayerOnSceneStart)
            playerCtrl.DisablePlayerControl();
    }

    /// <summary>重新找一遍 Player（比如某些特殊场景你想手动刷新）</summary>
    protected void RefreshPlayerReference(bool recacheSprites = true)
    {
        playerCtrl = PlayerController.Instance;
        if (playerCtrl == null)
        {
            Debug.LogWarning("[BaseLevelManager] RefreshPlayerReference: PlayerController.Instance is null.");
            return;
        }

        playerObject = playerCtrl.gameObject;
        playerRb = playerCtrl.GetComponent<Rigidbody2D>();

        if (recacheSprites)
            CachePlayerSprites();
    }

    // ===== Sprite 相关工具 =====
    protected void CachePlayerSprites()
    {
        _playerSprites.Clear();
        if (!playerObject) return;

        _playerSprites.AddRange(playerObject.GetComponentsInChildren<SpriteRenderer>(true));
    }

    protected void SetPlayerSpritesVisible(bool visible)
    {
        foreach (var sr in _playerSprites)
        {
            if (sr != null)
                sr.enabled = visible;
        }
    }

    /// <summary>给 Fungus / Timeline 用：让玩家出现</summary>
    public void RevealPlayerSprites()
    {
        SetPlayerSpritesVisible(true);
    }

    /// <summary>给 Fungus / Timeline 用：再次隐藏玩家</summary>
    public void HidePlayerSprites()
    {
        SetPlayerSpritesVisible(false);
    }

    // ===== 移动锁定相关 =====
    /// <summary>锁住玩家移动（控制+GamePhase 都锁）</summary>
    public void DisablePlayerMovement()
    {
        if (Gamemanager.instance)
            Gamemanager.instance.phase = GamePhase.Loading;

        if (playerCtrl != null)
            playerCtrl.DisablePlayerControl();
    }

    /// <summary>解锁玩家移动（GamePhase=Moving，控制打开）</summary>
    public void EnablePlayerMovement()
    {
        if (Gamemanager.instance)
            Gamemanager.instance.phase = GamePhase.Moving;

        if (playerCtrl != null)
            playerCtrl.EnablePlayerControl();
    }

    public void ShowPlayerAndAllowMove()
    {
        RevealPlayerSprites();
        EnablePlayerMovement();
    }
}
