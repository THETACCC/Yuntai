using Cinemachine;
using SKCell;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraSizeEffector : MonoBehaviour
{
    public float transitionTime = 1f;
    public float orthographicSize;

    public bool useScreenX;
    [SKConditionalField("useScreenX", true)]
    [Range(0f, 1f)]
    public float screenX = 0.5f;

    public bool useScreenY;
    [SKConditionalField("useScreenY", true)]
    [Range(0f, 1f)]
    public float screenY = 0.5f;

    [HideInInspector]
    public CinemachineVirtualCamera cam;

    private Coroutine sizeRoutine;

    void Start()
    {
        // Find the virtual camera automatically by tag
        if (cam == null)
        {
            GameObject myCam = GameObject.FindGameObjectWithTag("VirtualCam");
            if (myCam != null)
                cam = myCam.GetComponent<CinemachineVirtualCamera>();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") && cam != null)
        {
            if (sizeRoutine != null)
                StopCoroutine(sizeRoutine);

            sizeRoutine = StartCoroutine(SmoothTransition(orthographicSize, screenX, screenY, transitionTime));
        }
    }

    private System.Collections.IEnumerator SmoothTransition(float targetSize, float targetScreenX, float targetScreenY, float duration)
    {
        float startSize = cam.m_Lens.OrthographicSize;
        var framing = cam.GetCinemachineComponent<CinemachineFramingTransposer>();

        float startScreenX = framing != null ? framing.m_ScreenX : 0.5f;
        float startScreenY = framing != null ? framing.m_ScreenY : 0.5f;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // Quadratic ease-in-out
            float smoothT = t < 0.5f
                ? 2f * t * t
                : -1f + (4f - 2f * t) * t;

            // Smoothly interpolate orthographic size
            cam.m_Lens.OrthographicSize = Mathf.Lerp(startSize, targetSize, smoothT);

            // Smoothly interpolate framing offsets if applicable
            if (framing != null)
            {
                if (useScreenX)
                    framing.m_ScreenX = Mathf.Lerp(startScreenX, targetScreenX, smoothT);

                if (useScreenY)
                    framing.m_ScreenY = Mathf.Lerp(startScreenY, targetScreenY, smoothT);
            }

            yield return null;
        }

        cam.m_Lens.OrthographicSize = targetSize;

        if (framing != null)
        {
            if (useScreenX) framing.m_ScreenX = targetScreenX;
            if (useScreenY) framing.m_ScreenY = targetScreenY;
        }
    }
}