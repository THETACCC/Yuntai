using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Cinemachine;
using URPLight2D = UnityEngine.Rendering.Universal.Light2D;

public class LevelManager3_1 : MonoBehaviour
{
    [Header("Next Loop 跳转")]
    [Tooltip("下一回圈的场景名（若用 SceneController 也请填）")]
    [SerializeField] private string nextSceneName;
    [Tooltip("若使用 SceneController，则指定出生点编号")]
    [SerializeField] private int nextSpawnPointLocation = 0;
    [Tooltip("使用 SceneController.instance.LoadSceneAndTeleport() 跳转")]
    [SerializeField] private bool useSceneControllerTeleport = true;

    [Header("Spot Light 演出")]
    [SerializeField] private URPLight2D deathSpot;
    [SerializeField, Min(0f)] private float spotRiseDuration = 3f;
    [SerializeField] private float spotTargetIntensity = 1000f;
    [SerializeField] private float spotTargetOuterRadius = 7.5f;
    [SerializeField] private float spotTargetInnerRadius = 3f;
    [SerializeField] private float spotQuickFadeDuration = 0.25f;

    [Header("Actors (先切换坐->站)")]
    [SerializeField] private GameObject zhoushuSitting;   // 周叔（坐着版本）
    [SerializeField] private GameObject zhoushuStanding;  // 周叔（站着版本，后续会消失）

    [Header("Camera 设置")]
    [Tooltip("3-1 开场是否强制把相机对准玩家")]
    [SerializeField] private bool resetCameraOnStart = true;

    [Header("Timing / Debug")]
    [SerializeField] private bool useUnscaledTime = true;
    [SerializeField] private bool verboseLog = false;

    [Header("ZhouShu 失败时相关的物体")]
    [SerializeField] private List<GameObject> zhoushuFailObjects = new();
    [SerializeField] private GameObject interactableSeat;
    [SerializeField] private GameObject playerSit;   // 坐姿替身

    // runtime
    private Coroutine _escapeRoutine;
    private GameObject _player;
    private readonly List<SpriteRenderer> _playerSprites = new();
    private Rigidbody2D _playerRb;
    private PlayerController _playerCtrl;
    private CinemachineVirtualCamera _vcam;

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

        // 先缓存相机引用
#if UNITY_2023_1_OR_NEWER
        _vcam = FindFirstObjectByType<CinemachineVirtualCamera>();
#else
        _vcam = FindObjectOfType<CinemachineVirtualCamera>();
#endif

        // 这里只是拿引用，不在 Awake 里乱动 Sprite / 控制
        _player = GameObject.FindGameObjectWithTag("Player");
        if (!_player)
        {
            Debug.LogWarning("[LevelManager3_1] Awake: 未找到 tag=Player 的对象。");
        }
        else
        {
            _playerRb = _player.GetComponent<Rigidbody2D>();
            _playerCtrl = _player.GetComponent<PlayerController>();
        }
    }

    // ★★★★★ 这段是关键：开场强制把 Player 的所有 Sprite 打开，并恢复控制 + 相机 ★★★★★
    private void Start()
    {
        // 1) 确保 GameManager 处于可移动状态
        if (Gamemanager.instance != null)
            Gamemanager.instance.phase = GamePhase.Moving;

        // 2) 找 Player
        _player = GameObject.FindGameObjectWithTag("Player");
        if (_player != null)
        {
            _playerRb = _player.GetComponent<Rigidbody2D>();
            _playerCtrl = _player.GetComponent<PlayerController>();

            // 把她身上（以及子物体上）的所有 SpriteRenderer 打开 + alpha = 1
            SpriteRenderer[] sprites = _player.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var sr in sprites)
            {
                if (sr != null)
                {
                    sr.enabled = true;
                    Color c = sr.color;
                    c.a = 1f;
                    sr.color = c;
                }
            }

            // 缓存一下方便后面用
            _playerSprites.Clear();
            _playerSprites.AddRange(sprites);

            // 恢复控制
            if (_playerCtrl != null)
                _playerCtrl.EnablePlayerControl();

            if (_playerRb != null)
            {
                _playerRb.isKinematic = false;
                _playerRb.velocity = Vector2.zero;
            }

            // 坐姿替身默认关掉（如果你想开场就坐着，可以在 Inspector 里把 playerSit 设 active 然后在别处手动调用 PlayerStand）
            if (playerSit != null)
                playerSit.SetActive(false);

            // 3) 重设相机跟随，消掉 2-2 / 之前的 offset
            if (resetCameraOnStart)
            {
                var vcam = _vcam != null ? _vcam : FindObjectOfType<CinemachineVirtualCamera>();
                if (vcam != null)
                {
                    vcam.Follow = _player.transform;
                }
            }

            if (verboseLog)
                Debug.Log($"[LevelManager3_1] Start: 强制打开 Player Sprite（{_playerSprites.Count} 个）并允许移动。");
        }
        else
        {
            Debug.LogWarning("[LevelManager3_1] Start: 没找到 tag=Player 的对象。");
        }
    }

    /// <summary>触发：坐->站，打灯，上人消失，玩家消失，灯灭，跳下一回圈。</summary>
    public void ZhouShuEscape()
    {
        if (_escapeRoutine != null) StopCoroutine(_escapeRoutine);
        _escapeRoutine = StartCoroutine(CoZhouShuEscape());
    }

    private IEnumerator CoZhouShuEscape()
    {
        if (verboseLog) Debug.Log("[3-1] ZhouShuEscape start");

        // 0) 先切换对象：关闭“坐着”，启用“站着”
        SwitchZhoushuToStanding();

        // 1) 灯从无到有
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

        // 2) 让“站着”的周叔消失（整物体禁用）
        if (zhoushuStanding && zhoushuStanding.activeSelf)
            zhoushuStanding.SetActive(false);

        // 3) 主角仅隐藏 Sprite（不 Destroy、不禁用脚本/碰撞，只看不见）
        SetPlayerSpritesVisible(false);

        // 4) 灯快速淡出
        yield return FadeOutDeathLight();

        // 5) 跳到下一回圈
        GotoNextLoop();

        if (verboseLog) Debug.Log("[3-1] ZhouShuEscape end");
        _escapeRoutine = null;
    }

    // —— 工具 —— //
    private void SwitchZhoushuToStanding()
    {
        // 关闭坐着
        if (zhoushuSitting && zhoushuSitting.activeSelf)
            zhoushuSitting.SetActive(false);

        // 启用站着
        if (zhoushuStanding && !zhoushuStanding.activeSelf)
            zhoushuStanding.SetActive(true);
    }

    private void CachePlayerSprites(GameObject playerRoot)
    {
        _playerSprites.Clear();
        if (!playerRoot) return;
        _playerSprites.AddRange(playerRoot.GetComponentsInChildren<SpriteRenderer>(true));
    }

    private void SetPlayerSpritesVisible(bool visible)
    {
        // 超暴力：每次都重新取一遍，避免缓存丢失
        GameObject player = _player != null ? _player : GameObject.FindGameObjectWithTag("Player");
        if (!player) return;

        var srs = player.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var sr in srs)
        {
            if (sr != null)
            {
                sr.enabled = visible;
                if (visible)
                {
                    Color c = sr.color;
                    c.a = 1f;
                    sr.color = c;
                }
            }
        }

        _playerSprites.Clear();
        _playerSprites.AddRange(srs);
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

    public void ZhouShuFailed()
    {
        foreach (var go in zhoushuFailObjects)
        {
            if (go != null && go.activeSelf)
            {
                go.SetActive(false);
            }
        }

        if (interactableSeat != null)
            interactableSeat.SetActive(true);
    }

    // ================== 坐下 / 站起（给别的地方调用） ==================

    public void PlayerSit()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            // 隐藏 Player 所有 Sprite
            var srs = player.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var sr in srs)
                if (sr != null) sr.enabled = false;
        }

        if (playerSit != null)
            playerSit.SetActive(true);

        // 锁玩家移动
        if (_playerCtrl == null && player != null)
            _playerCtrl = player.GetComponent<PlayerController>();
        if (_playerCtrl != null)
            _playerCtrl.DisablePlayerControl();

        if (Gamemanager.instance != null)
            Gamemanager.instance.phase = GamePhase.Eventing;
    }

    public void PlayerStand()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            // ★ 强制打开所有 SpriteRenderer + alpha = 1 ★
            var srs = player.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var sr in srs)
            {
                if (sr != null)
                {
                    sr.enabled = true;
                    Color c = sr.color;
                    c.a = 1f;
                    sr.color = c;
                }
            }

            _player = player;
        }

        if (playerSit != null)
            playerSit.SetActive(false);

        // 恢复玩家移动
        if (_playerCtrl == null && player != null)
            _playerCtrl = player.GetComponent<PlayerController>();
        if (_playerCtrl != null)
            _playerCtrl.EnablePlayerControl();

        if (Gamemanager.instance != null)
            Gamemanager.instance.phase = GamePhase.Moving;

        // 确保相机重新 Follow Player
        var vcam = _vcam != null ? _vcam : FindObjectOfType<CinemachineVirtualCamera>();
        if (vcam != null && player != null)
            vcam.Follow = player.transform;
    }
}
