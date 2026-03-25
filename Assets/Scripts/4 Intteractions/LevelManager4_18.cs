using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LevelManager4_18 : BaseLevelManager
{
    protected override void Awake()
    {
        hidePlayerOnSceneStart = true;
        lockPlayerOnSceneStart = true;

        base.Awake();
    }

    private void Start()
    {
        StartCoroutine(BeginMusicGameIntro());
    }

    private IEnumerator BeginMusicGameIntro()
    {
        // 这里先做你的开场演出
        yield return new WaitForSeconds(1f);

        // 到你想让主角出现的时候再放出来
        RevealPlayerSprites();
        EnablePlayerMovement();
    }
}