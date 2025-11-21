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

    [SerializeField] private CinemachineConfiner2D confiner; // Reference to the CinemachineConfiner2D component
    private PolygonCollider2D boundingArea; // Reference to the PolygonCollider2D component of the bounding area

    [SerializeField] private Animator anim;   
    private static readonly int HashIsWalking = Animator.StringToHash("IsWalking");

    [Header("Footstep Audio")]
    [SerializeField] private AudioSource footstepSource;   // 挂在 Player 上，Loop = true
    [SerializeField] private bool onlyPlayOnGround = true; // 只在地面时播放
    private bool wasWalking = false;


    // Start is called before the first frame update
    void Start()
    {
        if (anim == null) anim = GetComponent<Animator>();
    }

    void Update()
    {
        boundingArea = confiner.m_BoundingShape2D as PolygonCollider2D;

        if (Gamemanager.instance.phase == GamePhase.Moving)
        {
            // 1) Read input ONLY from A / D
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
                // no movement keys or both pressed → slow down
                speed -= velocity * acceleration * Time.deltaTime;
            }

            // 2) Clamp speed so it never goes negative
            speed = Mathf.Clamp(speed, 0f, max_hspeed);
        }
        else
        {
            horizontal = 0f;
            speed = 0f;
        }

        bool isWalkingNow = (Gamemanager.instance.phase == GamePhase.Moving)
                            && Mathf.Abs(horizontal) > 0.01f
                            && speed > 0.01f;
        if (anim) anim.SetBool(HashIsWalking, isWalkingNow);

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


    private void ConfinePlayerToBoundingArea()
    {
        if (boundingArea == null) return;

        Vector2 playerPosition = transform.position;

        // Debugging: Log the player's position and the closest point
        //Debug.Log("Player Position: " + playerPosition);

        // Check if the player's position is inside the bounding area
        if (!boundingArea.OverlapPoint(playerPosition))
        {
            // If the player is outside, find the closest point on the bounding area
            Vector2 closestPoint = boundingArea.ClosestPoint(playerPosition);

            // Debugging: Log the closest point
            //Debug.Log("Closest Point: " + closestPoint);

            // Move the player to the closest point
            transform.position = closestPoint;
        }
    }

    private void FixedUpdate()
    {
        rb.velocity = new Vector2(horizontal * speed, rb.velocity.y);
    }

    private bool IsGrounded()
    {
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
}
