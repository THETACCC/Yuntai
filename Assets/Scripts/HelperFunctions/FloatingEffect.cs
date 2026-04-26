using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FloatingEffect : MonoBehaviour
{
    [Header("Movement Settings")]
    public float amplitude = 0.5f;   // How far it moves up/down
    public float speed = 2f;         // How fast it moves

    private Vector3 startPos;

    void Start()
    {
        startPos = transform.position;
    }

    void Update()
    {
        float newY = startPos.y + Mathf.Sin(Time.time * speed) * amplitude;
        transform.position = new Vector3(startPos.x, newY, startPos.z);
    }
}
