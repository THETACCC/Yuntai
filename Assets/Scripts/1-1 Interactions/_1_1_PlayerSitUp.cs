using UnityEngine;

public class _1_1_PlayerSitUp : MonoBehaviour
{
    private SpriteRenderer _selfSR;

    private void Awake()
    {
        _selfSR = GetComponent<SpriteRenderer>();

        
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
