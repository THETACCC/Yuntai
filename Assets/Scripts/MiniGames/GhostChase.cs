using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GhostChase : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 2.5f;

    private Transform player;
    private Rigidbody2D rb;

    //Movement Controll
    public bool isAllowMove = false;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
        }
        else
        {
            Debug.LogError("Player not found. Make sure the Player has the 'Player' tag.");
        }
    }

    void FixedUpdate()
    {
        if (player == null) return;

        float directionX = player.position.x - transform.position.x;

        if(isAllowMove)
        {
            // Move only horizontally
            rb.velocity = new Vector2(Mathf.Sign(directionX) * moveSpeed, rb.velocity.y);

            // Optional: flip sprite based on direction
            if (directionX != 0)
            {
                transform.localScale = new Vector3(
                    Mathf.Sign(directionX),
                    1,
                    1
                );
            }
        }


    }
}
