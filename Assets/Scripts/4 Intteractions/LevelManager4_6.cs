using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static AudioManager;
using URPLight2D = UnityEngine.Rendering.Universal.Light2D;

public class LevelManager4_6 : BaseLevelManager
{
    [Header("Explosion Light")]
    [SerializeField] private URPLight2D explosionLight;
    [SerializeField, Min(0.01f)] private float expandDuration = 0.45f;
    [SerializeField] private float targetIntensity = 1500f;
    [SerializeField] private float targetOuterRadius = 35f;
    [SerializeField] private float targetInnerRadius = 18f;

    [Header("Camera Shake")]
    [SerializeField] private CameraShake cameraShake;
    [SerializeField] private float shakeAmplitude = 8f;
    [SerializeField] private float shakeFrequency = 14f;
    [SerializeField] private float shakeDuration = 0.35f;

    [Header("Scene Transition")]
    [SerializeField] private string nextSceneName;
    [SerializeField] private int nextSpawnPointLocation = 0;
    [SerializeField] private float extraWaitBeforeLoad = 0.05f;

    [Header("Timing")]
    [SerializeField] private bool useUnscaledTime = true;

    private Coroutine explodeRoutine;
    private bool hasTriggered = false;

    protected override void Awake()
    {
        base.Awake();
        InitExplosionLight();
    }

    public void ExplosionTransition()
    {
        if (hasTriggered) return;

        hasTriggered = true;

        if (explodeRoutine != null)
            StopCoroutine(explodeRoutine);

        explodeRoutine = StartCoroutine(CoExplosionTransition());
    }

    private IEnumerator CoExplosionTransition()
    {
        if (cameraShake != null)
            cameraShake.Shake(shakeAmplitude, shakeFrequency, shakeDuration);

        InitExplosionLight();

        if (explosionLight != null)
        {
            float t = 0f;

            while (t < expandDuration)
            {
                t += DeltaTime();
                float u = Mathf.Clamp01(t / expandDuration);
                float s = Mathf.SmoothStep(0f, 1f, u);

                explosionLight.intensity = Mathf.Lerp(0f, targetIntensity, s);
                explosionLight.pointLightOuterRadius = Mathf.Lerp(0f, targetOuterRadius, s);
                explosionLight.pointLightInnerRadius = Mathf.Lerp(0f, targetInnerRadius, s);

                yield return null;
            }

            explosionLight.intensity = targetIntensity;
            explosionLight.pointLightOuterRadius = targetOuterRadius;
            explosionLight.pointLightInnerRadius = targetInnerRadius;
        }

        if (extraWaitBeforeLoad > 0f)
        {
            float wait = 0f;
            while (wait < extraWaitBeforeLoad)
            {
                wait += DeltaTime();
                yield return null;
            }
        }

        if (SceneController.instance != null && !string.IsNullOrEmpty(nextSceneName))
        {
            SceneController.instance.LoadSceneAndTeleport(nextSceneName, nextSpawnPointLocation);
        }

        explodeRoutine = null;
    }

    private void InitExplosionLight()
    {
        if (explosionLight == null) return;

        if (!explosionLight.gameObject.activeInHierarchy)
            explosionLight.gameObject.SetActive(true);

        if (!explosionLight.enabled)
            explosionLight.enabled = true;

        var pointType = UnityEngine.Rendering.Universal.Light2D.LightType.Point;
        if (explosionLight.lightType != pointType)
            explosionLight.lightType = pointType;

        explosionLight.intensity = 0f;
        explosionLight.pointLightOuterRadius = 0f;
        explosionLight.pointLightInnerRadius = 0f;

        if (targetInnerRadius > targetOuterRadius)
            targetInnerRadius = Mathf.Max(0f, targetOuterRadius - 0.01f);
    }

    private float DeltaTime()
    {
        return useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
    }
}