using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RotateZ : MonoBehaviour
{
    [Header("Rotation Range (Degrees)")]
    public float minZ = -30f;
    public float maxZ = 30f;


    [Header("Speed (cycles per second)")]
    [Tooltip("Average oscillation speed (cycles per second). 1 = one back-and-forth per second.")]
    public float baseCyclesPerSecond = 0.7f;

    [Tooltip("Random +/- variation added to baseCyclesPerSecond.")]
    public float cyclesPerSecondVariation = 0.25f;

    [Header("Random Timing")]
    [Tooltip("How often we pick a new random speed.")]
    public float minChangeInterval = 1.5f;
    public float maxChangeInterval = 3.5f;

    [Header("Smoothing")]
    [Tooltip("How quickly we blend to the new random speed (bigger = faster, smaller = smoother).")]
    public float speedBlendSharpness = 3f;

    private float phase;                 // radians, continuous over time
    private float currentOmega;           // radians/sec (smoothed)
    private float targetOmega;            // radians/sec (randomized target)
    private float timer;
    private float changeInterval;

    void Start()
    {
        phase = Random.Range(0f, Mathf.PI * 2f); // optional desync across instances
        PickNewTargetSpeed();
        currentOmega = targetOmega;             // start stable
    }

    void Update()
    {
        // Timer to occasionally change target speed
        timer += Time.deltaTime;
        if (timer >= changeInterval)
        {
            PickNewTargetSpeed();
        }

        // Smoothly blend current speed towards target speed (prevents jitter)
        float blend = 1f - Mathf.Exp(-speedBlendSharpness * Time.deltaTime);
        currentOmega = Mathf.Lerp(currentOmega, targetOmega, blend);

        // Advance phase continuously (this is the key to no shutter)
        phase += currentOmega * Time.deltaTime;

        // Compute smooth oscillation in [-1, 1]
        float s = Mathf.Sin(phase);

        // Convert to Z rotation between minZ and maxZ
        float center = (minZ + maxZ) * 0.5f;
        float amplitude = (maxZ - minZ) * 0.5f;
        float z = center + amplitude * s;

        transform.rotation = Quaternion.Euler(0f, 0f, z);
    }

    private void PickNewTargetSpeed()
    {
        timer = 0f;
        changeInterval = Random.Range(minChangeInterval, maxChangeInterval);

        float cps = baseCyclesPerSecond + Random.Range(-cyclesPerSecondVariation, cyclesPerSecondVariation);
        cps = Mathf.Max(0.05f, cps); // safety

        // cycles/sec -> radians/sec : omega = 2¦Ð * cps
        targetOmega = 2f * Mathf.PI * cps;
    }
}