using Cinemachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static AudioManager;

public class PlayerController : MonoBehaviour
{
    // ✅ 全局唯一 Player 入口
    public static PlayerController Instance { get; private set; }

    [Header("Movement")]
    public float horizontal;
    public float acceleration;
    public float max_hspeed;
    public float velocity;
    public float speed;

    private bool isFacingRight = true;

    [Header("Components")]
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Transform groundcheck;
    [SerializeField] private LayerMask groundLayer;

    [SerializeField] private CinemachineConfiner2D confiner; // Reference to the CinemachineConfiner2D component
    private PolygonCollider2D boundingArea;                  // Reference to the PolygonCollider2D component of the bounding area

    [SerializeField] private Animator anim;
    private static readonly int HashIsWalking = Animator.StringToHash("IsWalking");
    private static readonly int HashIsRunning = Animator.StringToHash("IsRunning");

    [Header("Anim Selector (auto find)")]
    [SerializeField] private PlayerAnimControllerSelector animSelector;

    [Header("Footstep Audio")]
    [SerializeField] private AudioSource footstepSource;   // 挂在 Player 上，Loop = true
    [SerializeField] private bool onlyPlayOnGround = true; // 只在地面时播放
    private bool wasWalking = false;

    // ★★★ 统一控制锁：演出 / 死亡动画时，锁住它 ★★★
    private bool controlLocked = false;

    // ----------------- Unity Lifecycle ----------------- //

    private void Awake()
    {
        // ✅ 单例：保证全局只存在一个 PlayerController
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[PlayerController] Duplicate Player detected, destroying {gameObject.name} in scene {gameObject.scene.name}");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);   // ✅ Player 在场景切换中保留

        // 组件兜底
        if (!anim) anim = GetComponent<Animator>();
        if (!rb) rb = GetComponent<Rigidbody2D>();
        if (!animSelector) animSelector = GetComponent<PlayerAnimControllerSelector>();
    }

    private void Start()
    {
        // 这里现在可以留空，或者再防御性获取一次（可选）
        if (!anim) anim = GetComponent<Animator>();
        if (!rb) rb = GetComponent<Rigidbody2D>();
        if (!animSelector) animSelector = GetComponent<PlayerAnimControllerSelector>();
    }

    private void Update()
    {
        if (confiner != null)
            boundingArea = confiner.m_BoundingShape2D as PolygonCollider2D;

        // ✅ Dark 场景判定：Shift 完全无效（不变速、不切跑步动画）
        bool pressShift = Input.GetKey(KeyCode.LeftShift);
        bool isDark = (animSelector != null && animSelector.IsDarkActive);
        bool wantsRun = pressShift && !isDark;

        var gm = Gamemanager.instance;
        bool phaseMoving = (gm != null && gm.phase == GamePhase.Moving);
        bool canMove = phaseMoving && !controlLocked;

        if (canMove)
        {
            //Debug.Log("Player Can Move");

            // 只读 A / D
            horizontal = 0f;
            bool pressA = Input.GetKey(KeyCode.A);
            bool pressD = Input.GetKey(KeyCode.D);

            if (pressA && !pressD)
            {
                horizontal = -1f;
                speed += velocity * acceleration * Time.deltaTime;
            }
            else if (pressD && !pressA)
            {
                horizontal = 1f;
                speed += velocity * acceleration * Time.deltaTime;
            }
            else
            {
                // 松键或同时按 → 减速
                speed -= velocity * acceleration * Time.deltaTime;
            }

            // ✅ Clamp：只有非Dark且按Shift才加速上限
            float maxSpeedNow = wantsRun ? (max_hspeed * 1.75f) : max_hspeed;
            speed = Mathf.Clamp(speed, 0f, maxSpeedNow);

            //Sound Related (留空也没问题)
            if ((horizontal != 0) && (!wantsRun))
            {
                // ...
            }
        }
        else
        {
            // 不允许移动时：强制停下
            horizontal = 0f;
            speed = 0f;
        }

        // 动画参数
        bool isWalkingNow = canMove && Mathf.Abs(horizontal) > 0.01f && speed > 0.01f;
        bool isRunningNow = isWalkingNow && wantsRun;

        if (anim)
        {
            anim.SetBool(HashIsWalking, isWalkingNow);
            anim.SetBool(HashIsRunning, isRunningNow);
        }

        // 脚步声逻辑（你现在逻辑是：跑步也算走路，会继续播脚步声）
        bool shouldPlayFootstep = isWalkingNow;
        if (onlyPlayOnGround)
            shouldPlayFootstep = shouldPlayFootstep && IsGrounded();

        if (footstepSource != null)
        {
            // 刚开始走路
            if (shouldPlayFootstep && !wasWalking)
            {
                if (!footstepSource.isPlaying)
                    footstepSource.Play();
            }
            // 停止走路
            else if (!shouldPlayFootstep && wasWalking)
            {
                if (footstepSource.isPlaying)
                    footstepSource.Stop();
            }
        }

        wasWalking = shouldPlayFootstep;

        Flip();
        ConfinePlayerToBoundingArea();
    }

    private void FixedUpdate()
    {
        if (rb == null) return;
        rb.velocity = new Vector2(horizontal * speed, rb.velocity.y);
    }

    // ----------------- 公共接口：统一给 Manager / ToNextLoop 调用 ----------------- //

    /// <summary>
    /// 锁住玩家控制：不能移动、不能播放走路/跑步动画、脚步声停。
    /// （用在演出 / 死亡动画期间）
    /// </summary>
    public void DisablePlayerControl()
    {
        controlLocked = true;
        horizontal = 0f;
        speed = 0f;

        if (rb != null)
            rb.velocity = new Vector2(0f, rb.velocity.y);

        if (anim != null)
        {
            anim.SetBool(HashIsWalking, false);
            anim.SetBool(HashIsRunning, false);
        }

        if (footstepSource != null && footstepSource.isPlaying)
            footstepSource.Stop();
    }

    /// <summary>
    /// 解除锁定：恢复到正常，由 GamePhase 决定能不能动。
    /// （一般在演出 / 死亡动画结束 / 新场景开始时调用）
    /// </summary>
    public void EnablePlayerControl()
    {
        controlLocked = false;
    }

    // ----------------- 工具函数 ----------------- //

    private bool IsGrounded()
    {
        if (!groundcheck) return false;
        return Physics2D.OverlapCircle(groundcheck.position, 0.3f, groundLayer);
    }

    private void Flip()
    {
        if (isFacingRight && horizontal < 0f || !isFacingRight && horizontal > 0f)
        {
            isFacingRight = !isFacingRight;
            Vector3 localScale = transform.localScale;
            localScale.x *= -1f;
            transform.localScale = localScale;
        }
    }

    private void ConfinePlayerToBoundingArea()
    {
        if (boundingArea == null) return;

        Vector2 playerPosition = transform.position;

        if (!boundingArea.OverlapPoint(playerPosition))
        {
            Vector2 closestPoint = boundingArea.ClosestPoint(playerPosition);
            transform.position = closestPoint;
        }
    }

    //Audio Related
    public void PlayFootStepSounds()
    {
        AudioManager.Play("Sound Effects/Henk/sndPlayerFootStep", AudioGroup.SFX);
        AudioManager.SetVolume("Sound Effects/Henk/sndPlayerFootStep", 0.4f);
    }

    public void PlayFootStepSoundsType2()
    {
        AudioManager.Play("Sound Effects/Henk/sndPlayerFootStep2", AudioGroup.SFX);
        AudioManager.SetVolume("Sound Effects/Henk/sndPlayerFootStep2", 0.4f);
    }
}