using UnityEngine;

public class _1_1_PlayerSitUp : MonoBehaviour
{
    private GameObject player;

    private SpriteRenderer playerSprite;

    private SpriteRenderer _selfSR;

    private void Awake()
    {
        _selfSR = GetComponent<SpriteRenderer>();

        if (player == null)
        {
            player = GameObject.FindGameObjectWithTag("Player");
#if UNITY_EDITOR
            if (player == null)
                Debug.LogWarning("[_1_1_PlayerSitUp] 没找到带有 'Player' 标签的对象。");
#endif
        }

        if (playerSprite == null && player != null)
        {
            // 优先取 Player 根节点上的 SR，没有就从子物体中抓一个
            playerSprite = player.GetComponent<SpriteRenderer>();
            if (playerSprite == null)
                playerSprite = player.GetComponentInChildren<SpriteRenderer>(true);
#if UNITY_EDITOR
            if (playerSprite == null)
                Debug.LogWarning("[_1_1_PlayerSitUp] 没在 Player 上找到 SpriteRenderer。");
#endif
        }
    }

    private void Start()
    {
        // 开场让主角“透明”，而不是 SetActive(false)
        if (playerSprite != null)
        {
            Color c = playerSprite.color;
            c.a = 0f;
            playerSprite.color = c;
        }
    }

    /// <summary>
    /// 让主角变得“不透明”（Alpha=1）。
    /// 可从动画事件、按钮、或其他脚本调用。
    /// </summary>
    public void MakePlayerOpaque()
    {
        if (playerSprite == null) return;
        Color c = playerSprite.color;
        c.a = 1f;
        playerSprite.color = c;
    }

    /// <summary>
    /// （可选）把本脚本挂载对象自身的精灵设为透明。
    /// </summary>
    public void SetSelfAlphaZero()
    {
        if (_selfSR == null) return;
        Color c = _selfSR.color;
        c.a = 0f;
        _selfSR.color = c;
    }
}
