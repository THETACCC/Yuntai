using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FollowPlayer : MonoBehaviour
{
    [Header("Follow Settings")]
    public float smoothSpeed = 5f;   // How smoothly the object follows
    public Vector3 offset;           // Optional offset (e.g., camera distance)

    private Transform playerTransform;

    void Start()
    {
        // Find the player object by tag
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }
        else
        {
            Debug.LogWarning("FollowPlayer: No GameObject with tag 'Player' found in the scene.");
        }
    }

    void LateUpdate()
    {
        if (playerTransform == null) return;

        // Desired position (player position + offset)
        Vector3 targetPosition = playerTransform.position + offset;

        transform.position = targetPosition;
        // Smoothly interpolate to the target position
        //transform.position = Vector3.Lerp(transform.position, targetPosition, smoothSpeed * Time.deltaTime);
    }
}
