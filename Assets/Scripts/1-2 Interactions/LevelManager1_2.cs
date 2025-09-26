using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using URPLight2D = UnityEngine.Rendering.Universal.Light2D;

public class LevelManager1_2 : MonoBehaviour
{
    [SerializeField] private int myLoop = 1;

    [Header("闪烁并随后变暗的灯")]
    [SerializeField] private URPLight2D lightToBlinkAndDim;

    [System.Serializable]
    public struct BlinkStep
    {
        [Min(0.05f)] public float cycle;
        [Min(0.01f)] public float onTime;
        [Min(0f)] public float minIntensity;
        [Min(0f)] public float maxIntensity;
    }

    [Header("闪烁节奏（例：长-短-长）")]
    public List<BlinkStep> blinkPattern = new()
    {
        new BlinkStep{ cycle=1.0f, onTime=0.20f, minIntensity=0.25f, maxIntensity=0.9f },
        new BlinkStep{ cycle=0.5f, onTime=0.08f, minIntensity=0.25f, maxIntensity=0.9f },
        new BlinkStep{ cycle=1.0f, onTime=0.20f, minIntensity=0.25f, maxIntensity=0.9f },
    };

    [Header("变暗参数（intensity）")]
    [SerializeField, Min(0f)] private float dimTargetIntensity = 0.2f;
    [SerializeField, Min(0f)] private float dimDuration = 1.0f;

    [Header("红灯（在变暗结束后激活）")]
    [SerializeField] private GameObject redLightObject;

    [Header("红灯亮起到显示玩家 Sprite 的延迟(秒)")]
    [SerializeField, Min(0f)] private float revealDelayAfterRed = 1.0f;

    [Header("对话触发器（可选；不填就去 Player 身上找）")]
    [SerializeField] private DialogueTrigger dialogueTrigger;
    [SerializeField] private bool autoTriggerDialogueAfterPlayer = false;
    [SerializeField, Min(0f)] private float dialogueDelayAfterPlayer = 0f;

    // runtime
    private GameObject playerObject;
    private readonly List<SpriteRenderer> _playerSprites = new();

    private void Awake()
    {
        // 一进场就把游戏置为 Loading，禁走动
        if (Gamemanager.instance) Gamemanager.instance.phase = GamePhase.Loading;

        // 找 Player，并先隐藏所有 Sprite
        playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject)
        {
            CachePlayerSprites();
            SetPlayerSpritesVisible(false);

            if (!dialogueTrigger)
                dialogueTrigger = playerObject.GetComponentInChildren<DialogueTrigger>(true);
        }
        else
        {
            Debug.LogWarning("[LevelManager1_2] 没找到 tag=Player 的激活对象。");
        }
    }

    private void Start()
    {
        LoopTracker.I?.SetLoop(myLoop);

        if (!lightToBlinkAndDim)
        {
            Debug.LogWarning("[LevelManager1_2] 未设置 lightToBlinkAndDim，灯光流程不会开始。");
            return;
        }

        StartCoroutine(RunLightsThenRevealPlayer());
    }

    private IEnumerator RunLightsThenRevealPlayer()
    {
        // 闪烁（按步）
        foreach (var step in blinkPattern)
        {
            LightControl.StartBlink(lightToBlinkAndDim, step.cycle, step.onTime, step.minIntensity, step.maxIntensity);
            yield return new WaitForSeconds(step.cycle);
        }

        // 停止闪烁 → 渐暗
        LightControl.StopBlink(lightToBlinkAndDim);
        LightControl.Dim(lightToBlinkAndDim, dimTargetIntensity, dimDuration);
        yield return new WaitForSeconds(dimDuration);

        // 红灯亮
        if (redLightObject) redLightObject.SetActive(true);

        // 再等 1 秒
        if (revealDelayAfterRed > 0f)
            yield return new WaitForSeconds(revealDelayAfterRed);

        // 显示 Player 的 Sprites
        SetPlayerSpritesVisible(true);

        yield return new WaitForSeconds(0.7f);

        // 触发对话
        if (autoTriggerDialogueAfterPlayer && dialogueTrigger)
        {
            if (dialogueDelayAfterPlayer > 0f)
                yield return new WaitForSeconds(dialogueDelayAfterPlayer);

            dialogueTrigger.TriggerDialogue();
        }
    }

    private void CachePlayerSprites()
    {
        _playerSprites.Clear();
        if (!playerObject) return;
        _playerSprites.AddRange(playerObject.GetComponentsInChildren<SpriteRenderer>(true));
    }

    private void SetPlayerSpritesVisible(bool visible)
    {
        foreach (var sr in _playerSprites)
            if (sr) sr.enabled = visible;
    }

}
