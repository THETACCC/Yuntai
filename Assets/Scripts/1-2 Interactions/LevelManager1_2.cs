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

    [Header("闪烁节奏")]
    public List<BlinkStep> blinkPattern = new()
    {
        new BlinkStep{ cycle=1.0f, onTime=0.20f, minIntensity=0.25f, maxIntensity=0.9f },
        new BlinkStep{ cycle=0.5f, onTime=0.08f, minIntensity=0.25f, maxIntensity=0.9f },
        new BlinkStep{ cycle=1.0f, onTime=0.20f, minIntensity=0.25f, maxIntensity=0.9f },
    };

    [Header("变暗参数（intensity）")]
    [SerializeField, Min(0f)] private float dimTargetIntensity = 0.2f;
    [SerializeField, Min(0f)] private float dimDuration = 1.0f;

    [Header("红灯")]
    [SerializeField] private GameObject redLightObject;

    [Header("对话触发器")]
    [SerializeField] private DialogueTrigger dialogueTrigger;
    [SerializeField] private bool autoTriggerDialogueAfterPlayer = false;
    [SerializeField, Min(0f)] private float dialogueDelayAfterPlayer = 0f;

    [Header("音效")]
    [SerializeField] private AudioSource snd_toilet;  

    // runtime
    private GameObject playerObject;
    private readonly List<SpriteRenderer> _playerSprites = new();

    private void Awake()
    {
        // 开场禁止移动
        if (Gamemanager.instance) Gamemanager.instance.phase = GamePhase.Loading;

        // 找 Player 并先隐藏所有 Sprite
        playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject)
        {
            CachePlayerSprites();
            SetPlayerSpritesVisible(false);

        }
        else
        {
            Debug.LogWarning("[LevelManager1_2] 没找到 tag=Player 的激活对象。");
        }

        if (!dialogueTrigger)
            Debug.Log("No dialogue trigger!!");
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
        // —— 0) 黑屏 2.5 秒 ——
        lightToBlinkAndDim.intensity = 0f;
        yield return new WaitForSeconds(2.5f);

        // —— 1) 连续闪烁 4~5 秒 ——
        var step = (blinkPattern != null && blinkPattern.Count > 0)
            ? blinkPattern[0]
            : new BlinkStep { cycle = 1.0f, onTime = 0.20f, minIntensity = 0.25f, maxIntensity = 0.9f };

        LightControl.StartBlink(lightToBlinkAndDim, step.cycle, step.onTime, step.minIntensity, step.maxIntensity);
        yield return new WaitForSeconds(Random.Range(4f, 5f));
        LightControl.StopBlink(lightToBlinkAndDim);

        // —— 2) global light Dim ——
        LightControl.Dim(lightToBlinkAndDim, dimTargetIntensity, dimDuration);
        yield return new WaitForSeconds(dimDuration);

        yield return new WaitForSeconds(1.3f);

        // —— 3) 红光亮 —— 
        if (redLightObject) redLightObject.SetActive(true);

        // —— 4) 等 2 秒 —— 
        yield return new WaitForSeconds(2f);

        // —— 5) 播放厕所音效，等它播完 —— 
        if (snd_toilet)
        {
            snd_toilet.Play();
            if (snd_toilet.clip) yield return new WaitForSeconds(snd_toilet.clip.length);
            else while (snd_toilet.isPlaying) yield return null;
        }

        // —— 6) 再等 2 秒 —— 
        yield return new WaitForSeconds(2f);

        // —— 7) 角色出现—— 
        SetPlayerSpritesVisible(true);

        // —— 8) 等 0.7 秒 → 对话框 —— 
        yield return new WaitForSeconds(0.7f);
        dialogueTrigger?.TriggerDialogue();    
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
