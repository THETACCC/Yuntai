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

    // Start is called before the first frame update
    void Start()
    {

    }

    void Update()
    {
        boundingArea = confiner.m_BoundingShape2D as PolygonCollider2D;


        speed = Mathf.Clamp(speed, 0f, max_hspeed);
        horizontal = Input.GetAxisRaw("Horizontal");
        if ((Input.GetKey(KeyCode.A)) && !(Input.GetKey(KeyCode.D)))
        {
            speed += velocity * acceleration* Time.deltaTime;
        }
        else if ((Input.GetKey(KeyCode.D)) && !(Input.GetKey(KeyCode.A)))
        {
            speed += velocity * acceleration * Time.deltaTime;
        }
        else if ((Input.GetKey(KeyCode.A)) && (Input.GetKey(KeyCode.D)))
        {
            speed -= velocity * acceleration * Time.deltaTime;
        }
        else
        {
            speed -= velocity * acceleration * Time.deltaTime;
        }
        Flip();
        ConfinePlayerToBoundingArea();
        //Physics2D.IgnoreLayerCollision(7, 8);
    }

    private void ConfinePlayerToBoundingArea()
    {
        if (boundingArea == null) return;

        Vector2 playerPosition = transform.position;

        // Debugging: Log the player's position and the closest point
        Debug.Log("Player Position: " + playerPosition);

        // Check if the player's position is inside the bounding area
        if (!boundingArea.OverlapPoint(playerPosition))
        {
            // If the player is outside, find the closest point on the bounding area
            Vector2 closestPoint = boundingArea.ClosestPoint(playerPosition);

            // Debugging: Log the closest point
            Debug.Log("Closest Point: " + closestPoint);

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
