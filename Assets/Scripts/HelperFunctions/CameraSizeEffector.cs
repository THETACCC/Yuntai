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
    public float screenX;

    public bool useScreenY;
    [SKConditionalField("useScreenY", true)]
    public float screenY;

    [HideInInspector]
    public CinemachineVirtualCamera cam;

    private Coroutine sizeRoutine;

    void Start()
    {
        // Automatically locate the virtual camera by tag if not assigned
        if (cam == null)
        {
            GameObject myCam = GameObject.FindGameObjectWithTag("VirtualCam");
            if (myCam != null)
            {
                cam = myCam.GetComponent<CinemachineVirtualCamera>();
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") && cam != null)
        {
            if (sizeRoutine != null)
                StopCoroutine(sizeRoutine);

            sizeRoutine = StartCoroutine(SmoothResize(orthographicSize, transitionTime));
        }
    }

    private System.Collections.IEnumerator SmoothResize(float targetSize, float duration)
    {
        float startSize = cam.m_Lens.OrthographicSize;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // Apply a quadratic ease-in-out curve
            // (Accelerates, then decelerates smoothly)
            float smoothT = t < 0.5f
                ? 2f * t * t                    // ease-in
                : -1f + (4f - 2f * t) * t;      // ease-out

            cam.m_Lens.OrthographicSize = Mathf.Lerp(startSize, targetSize, smoothT);
            yield return null;
        }

        cam.m_Lens.OrthographicSize = targetSize;
    }
}