using UnityEngine;
using URPLight2D = UnityEngine.Rendering.Universal.Light2D;

[RequireComponent(typeof(URPLight2D))]
public class Light2DFlicker : MonoBehaviour
{
    [Header("Base Settings")]
    [SerializeField] private float baseIntensity = 1f;       // Default light intensity
    [SerializeField] private float intensityAmplitude = 0.3f; // How strong the flicker is (0–1)
    [SerializeField] private float noiseSpeed = 2f;          // How fast the flicker changes

    [Header("Optional: Slight Color Flicker")]
    [SerializeField] private bool useColorFlicker = false;
    [SerializeField] private Color baseColor = Color.white;
    [SerializeField] private float colorAmplitude = 0.15f;   // How much the saturation changes

    private URPLight2D light2D;
    private float noiseOffset; // Unique offset so each light flickers differently

    private void Awake()
    {
        light2D = GetComponent<URPLight2D>();

        // Use the current light values as defaults if not set in Inspector
        if (baseIntensity <= 0f)
            baseIntensity = light2D.intensity;

        if (baseColor == Color.white)
            baseColor = light2D.color;

        // Each light gets a different Perlin noise starting point
        noiseOffset = Random.value * 100f;
    }

    private void Update()
    {
        // Sample Perlin noise in [0,1]
        float t = Time.time * noiseSpeed + noiseOffset;
        float n = Mathf.PerlinNoise(t, 0f);     // 0..1
        float centered = n * 2f - 1f;           // Map to -1..1

        // Apply flicker to intensity
        float multiplier = 1f + centered * intensityAmplitude;
        multiplier = Mathf.Max(0f, multiplier); // Avoid negative intensity
        light2D.intensity = baseIntensity * multiplier;

        if (useColorFlicker)
        {
            Color.RGBToHSV(baseColor, out float h, out float s, out float v);

            // Reduce saturation a bit based on noise
            float sScale = 1f - Mathf.Abs(centered) * colorAmplitude;
            s *= sScale;

            Color c = Color.HSVToRGB(h, s, v);
            light2D.color = c;
        }
    }
}
