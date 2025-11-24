using Cinemachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float horizontal;
    public float acceleration;
    public float max_hspeed;
    public float velocity;
    public float speed;

    private bool isFacingRight = true;

    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Transform groundcheck;
    [SerializeField] private LayerMask groundLayer;

    [SerializeField] private CinemachineConfiner2D confiner;
    private PolygonCollider2D boundingArea;

    [SerializeField] private Animator anim;
    private static readonly int HashIsWalking = Animator.StringToHash("IsWalking");

    [Header("Footstep Audio")]
    [SerializeField] private AudioSource footstepSource;
    [SerializeField] private bool onlyPlayOnGround = true;
    private bool wasWalking = false;

    // 统一锁定开关
    private bool controlLocked = false;

    void Start()
    {
        if (anim == null) anim = GetComponent<Animator>();
    }

    void Update()
    {
        if (confiner)
            boundingArea = confiner.m_BoundingShape2D as PolygonCollider2D;

        bool canMove =
            Gamemanager.instance != null &&
            Gamemanager.instance.phase == GamePhase.Moving &&
            !controlLocked;

        if (canMove)
        {
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
                speed -= velocity * acceleration * Time.deltaTime;
            }

            speed = Mathf.Clamp(speed, 0f, max_hspeed);
        }
        else
        {
            horizontal = 0f;
            speed = 0f;
        }

        bool isWalkingNow = canMove &&
                            Mathf.Abs(horizontal) > 0.01f &&
                            speed > 0.01f;

        if (anim) anim.SetBool(HashIsWalking, isWalkingNow);

        bool shouldPlayFootstep = isWalkingNow;
        if (onlyPlayOnGround)
            shouldPlayFootstep = shouldPlayFootstep && IsGrounded();

        if (footstepSource)
        {
            if (shouldPlayFootstep && !wasWalking)
            {
                if (!footstepSource.isPlaying) footstepSource.Play();
            }
            else if (!shouldPlayFootstep && wasWalking)
            {
                if (footstepSource.isPlaying) footstepSource.Stop();
            }
        }

        wasWalking = shouldPlayFootstep;

        Flip();
        ConfinePlayerToBoundingArea();
    }

    private void FixedUpdate()
    {
        if (!rb) return;
        rb.velocity = new Vector2(horizontal * speed, rb.velocity.y);
    }

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

    // ===== 对外接口 =====

    public void DisablePlayerControl()
    {
        controlLocked = true;

        horizontal = 0f;
        speed = 0f;

        if (rb)
            rb.velocity = new Vector2(0f, rb.velocity.y);

        if (anim)
            anim.SetBool(HashIsWalking, false);

        if (footstepSource && footstepSource.isPlaying)
            footstepSource.Stop();
    }

    public void EnablePlayerControl()
    {
        controlLocked = false;
    }

    // 兼容旧名字
    public void LockControl() => DisablePlayerControl();
    public void UnlockControl() => EnablePlayerControl();
}
