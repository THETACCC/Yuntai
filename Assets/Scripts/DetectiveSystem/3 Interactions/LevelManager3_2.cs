using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LevelManager3_2 : MonoBehaviour
{
    [SerializeField] private bool verboseLog = false;

    private void Start()
    {
        // 用 tag 找到 DontDestroyOnLoad 的 Player
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (!player)
        {
            Debug.LogWarning("[LevelManager3_2] 没有找到 tag=Player 的对象。");
            return;
        }

        // 把她身上（以及子物体上）的所有 SpriteRenderer 打开
        SpriteRenderer[] sprites = player.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var sr in sprites)
        {
            if (sr != null)
                sr.enabled = true;
        }

        if (verboseLog)
            Debug.Log($"[LevelManager3_2] 已为 Player 重新启用 {sprites.Length} 个 SpriteRenderer。");
    }
}
