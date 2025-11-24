using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using URPLight2D = UnityEngine.Rendering.Universal.Light2D;

public class LevelManager3_1 : MonoBehaviour
{
    [Header("Next Loop 跳转")]
    [SerializeField] private string nextSceneName;
    [SerializeField] private int nextSpawnPointLocation = 0;
    [SerializeField] private bool useSceneControllerTeleport = true;

    [Header("Spot Light 演出")]
    [SerializeField] private URPLight2D deathSpot;
    [SerializeField, Min(0f)] private float spotRiseDuration = 3f;
    [SerializeField] private float spotTargetIntensity = 1000f;
    [SerializeField] private float spotTargetOuterRadius = 7.5f;
    [SerializeField] private float spotTargetInnerRadius = 3f;
    [SerializeField] private float spotQuickFadeDuration = 0.25f;

    [Header("Actors (先切换坐->站)")]
    [SerializeField] private GameObject zhoushuSitting;
    [SerializeField] private GameObject zhoushuStanding;

    [Header("Timing / Debug")]
    [SerializeField] private bool useUnscaledTime = true;
    [SerializeField] private bool verboseLog = false;

    private Coroutine _escapeRoutine;

    // player 相关
    private GameObject _player;
    private PlayerController _playerCtrl;
    private readonly List<SpriteRenderer> _playerSprites = new();

    private void Awake()
    {
        // 初始化灯
        if (deathSpot)
        {
            if (!deathSpot.gameObject.activeInHierarchy) deathSpot.gameObject.SetActive(true);
            if (!deathSpot.enabled) deathSpot.enabled = true;
            var lt = UnityEngine.Rendering.Universal.Light2D.LightType.Point;
            if (deathSpot.lightType != lt) deathSpot.lightType = lt;
            deathSpot.intensity = 0f;
            deathSpot.pointLightOuterRadius = 0f;
            deathSpot.pointLightInnerRadius = 0f;
        }

        // 找主角
        _player = GameObject.FindGameObjectWithTag("Player");
        if (_player)
        {
            _playerSprites.Clear();
            _playerSprites.AddRange(_player.GetComponentsInChildren<SpriteRenderer>(true));
            foreach (var sr in _playerSprites)
                if (sr) sr.enabled = true;

            _playerCtrl = _player.GetComponent<PlayerController>();
            if (_playerCtrl) _playerCtrl.EnablePlayerControl();
        }
        else
        {
            Debug.LogWarning("[LevelManager3_1] 未找到 tag=Player 的对象。");
        }

        if (Gamemanager.instance)
            Gamemanager.instance.phase = GamePhase.Moving;
    }

    // public：外面调用开始 ZhouShuEscape（比如对话回调）
    public void ZhouShuEscape()
    {
        if (_escapeRoutine != null) StopCoroutine(_escapeRoutine);
        _escapeRoutine = StartCoroutine(CoZhouShuEscape());
    }

    private IEnumerator CoZhouShuEscape()
    {
        if (verboseLog) Debug.Log("[3-1] ZhouShuEscape start");

        // 表演开始时可以锁一下玩家（防止乱动），反正后面要跳场景
        if (_playerCtrl) _playerCtrl.DisablePlayerControl();

        // 0) 坐 -> 站
        SwitchZhoushuToStanding();

        // 1) 灯变亮
        InitDeathLight();
        if (deathSpot && spotRiseDuration > 0f)
        {
            float t = 0f;
            while (t < spotRiseDuration)
            {
                t += DeltaTime();
                float s = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / spotRiseDuration));
                deathSpot.intensity = Mathf.Lerp(0f, spotTargetIntensity, s);
                deathSpot.pointLightOuterRadius = Mathf.Lerp(0f, spotTargetOuterRadius, s);
                deathSpot.pointLightInnerRadius = Mathf.Lerp(0f, spotTargetInnerRadius, s);
                yield return null;
            }
            deathSpot.intensity = spotTargetIntensity;
            deathSpot.pointLightOuterRadius = spotTargetOuterRadius;
            deathSpot.pointLightInnerRadius = spotTargetInnerRadius;
        }

        // 2) 周叔消失
        if (zhoushuStanding && zhoushuStanding.activeSelf)
            zhoushuStanding.SetActive(false);

        // 3) 玩家只隐藏 sprite（如果你想要一起消失，把下面这一行打开）
        // SetPlayerSpritesVisible(false);

        // 4) 灯快速淡出
        yield return FadeOutDeathLight();

        // 5) 跳到下一回圈
        GotoNextLoop();

        if (verboseLog) Debug.Log("[3-1] ZhouShuEscape end");
        _escapeRoutine = null;
    }

    private void SwitchZhoushuToStanding()
    {
        if (zhoushuSitting && zhoushuSitting.activeSelf)
            zhoushuSitting.SetActive(false);

        if (zhoushuStanding && !zhoushuStanding.activeSelf)
            zhoushuStanding.SetActive(true);
    }

    private void SetPlayerSpritesVisible(bool visible)
    {
        foreach (var sr in _playerSprites)
            if (sr) sr.enabled = visible;
    }

    private void InitDeathLight()
    {
        if (!deathSpot) return;
        if (!deathSpot.gameObject.activeInHierarchy) deathSpot.gameObject.SetActive(true);
        if (!deathSpot.enabled) deathSpot.enabled = true;
        var lt = UnityEngine.Rendering.Universal.Light2D.LightType.Point;
        if (deathSpot.lightType != lt) deathSpot.lightType = lt;
        deathSpot.intensity = 0f;
        deathSpot.pointLightOuterRadius = 0f;
        deathSpot.pointLightInnerRadius = 0f;
        if (spotTargetInnerRadius > spotTargetOuterRadius)
            spotTargetInnerRadius = Mathf.Max(0f, spotTargetOuterRadius - 0.01f);
    }

    private IEnumerator FadeOutDeathLight()
    {
        if (!deathSpot) yield break;
        if (!deathSpot.enabled || (deathSpot.intensity <= 0.001f && deathSpot.pointLightOuterRadius <= 0.001f))
            yield break;

        float i0 = deathSpot.intensity;
        float o0 = deathSpot.pointLightOuterRadius;
        float in0 = deathSpot.pointLightInnerRadius;

        float tf = 0f;
        float dur = Mathf.Max(0.01f, spotQuickFadeDuration);
        while (tf < dur)
        {
            tf += DeltaTime();
            float u = Mathf.Clamp01(tf / dur);
            float k = 1f - u;
            deathSpot.intensity = i0 * k;
            deathSpot.pointLightOuterRadius = o0 * k;
            deathSpot.pointLightInnerRadius = in0 * k;
            yield return null;
        }

        deathSpot.intensity = 0f;
        deathSpot.pointLightOuterRadius = 0f;
        deathSpot.pointLightInnerRadius = 0f;
        deathSpot.enabled = false;
    }

    private void GotoNextLoop()
    {
        if (useSceneControllerTeleport && SceneController.instance != null)
        {
            if (!string.IsNullOrEmpty(nextSceneName))
                SceneController.instance.LoadSceneAndTeleport(nextSceneName, nextSpawnPointLocation);
            else
                Debug.LogWarning("[LevelManager3_1] 未配置 nextSceneName，无法通过 SceneController 跳转。");
        }
        else if (!string.IsNullOrEmpty(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
        }
        else
        {
            Debug.LogWarning("[LevelManager3_1] 未配置下一回圈跳转：请填 nextSceneName 或开启 useSceneControllerTeleport。");
        }
    }

    private float DeltaTime() => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
}
