using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UI_Blink : MonoBehaviour
{
    [Header("Blink Settings")]
    public Image targetImage;          // The UI Image to blink
    public float blinkInterval = 0.5f; // Time (in seconds) between each blink
    public bool startOn = true;        // Should blinking start automatically?

    private bool isBlinking = false;
    private bool isVisible = true;
    private Color originalColor;
    private float timer = 0f;

    void Start()
    {
        if (targetImage == null)
            targetImage = GetComponent<Image>();

        if (targetImage != null)
        {
            originalColor = targetImage.color;
            if (startOn)
                StartBlink();
        }
        else
        {
            Debug.LogWarning("UI_Blink: No Image assigned or found on this GameObject.");
        }
    }

    void Update()
    {
        if (!isBlinking || targetImage == null) return;

        timer += Time.deltaTime;
        if (timer >= blinkInterval)
        {
            ToggleAlpha();
            timer = 0f;
        }
    }

    private void ToggleAlpha()
    {
        isVisible = !isVisible;
        Color newColor = originalColor;
        newColor.a = isVisible ? 1f : 0f;
        targetImage.color = newColor;
    }

    public void StartBlink()
    {
        isBlinking = true;
        timer = 0f;
    }

    public void StopBlink()
    {
        isBlinking = false;
        if (targetImage != null)
        {
            // Reset to fully visible
            Color newColor = originalColor;
            newColor.a = 1f;
            targetImage.color = newColor;
        }
    }
}