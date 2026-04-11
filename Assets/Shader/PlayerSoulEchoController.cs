using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Multi-direction soul echo controller, keeping the same name as before.
/// It spawns multiple ghost copies of the player sprite in front and behind,
/// animates them flying outward (knock-out) and then back + fade (return).
[RequireComponent(typeof(SpriteRenderer))]
public class PlayerSoulEchoController : MonoBehaviour
{
    [Header("Source (animated) SpriteRenderer")]
    [Tooltip("If left empty, this script will try to use the parent SpriteRenderer first, then its own.")]
    public SpriteRenderer sourceRenderer;

    [Header("Soul Material (Chromatic Aberration)")]
    [Tooltip("Material using ShaderLab/PlayerChromaticAberration. " +
             "If null, we clone sourceRenderer.sharedMaterial.")]
    public Material soulMaterial;

    [Header("Echo layout")]
    [Tooltip("Number of echoes on EACH side (front and back).")]
    public int echoesPerSide = 3;

    [Tooltip("Distance between each echo along the burst direction (world units).")]
    public float echoSpacing = 0.35f;

    [Tooltip("Overall burst direction in world space (will be normalized).")]
    public Vector2 knockDirection = new Vector2(1f, 0.2f);

    [Header("Timing")]
    [Tooltip("Time for echoes to fly outward.")]
    public float knockDuration = 0.6f;

    [Tooltip("Time for echoes to come back and fade out.")]
    public float returnDuration = 0.6f;

    [Tooltip("Position curve for knock-out (0→1).")]
    public AnimationCurve knockCurve =
        AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Tooltip("Position curve for return (1→0).")]
    public AnimationCurve returnCurve =
        AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Sorting")]
    [Tooltip("SortingOrder offset relative to the source sprite.")]
    public int sortingOrderOffset = 1;

    private class Echo
    {
        public Transform tf;
        public SpriteRenderer sr;
        public float sideSign;
        public int index;
    }

    private readonly List<Echo> _echoes = new List<Echo>();
    private Vector3 _basePosition;
    private Coroutine _knockCo;
    private Coroutine _returnCo;

    private static readonly int PhaseID = Shader.PropertyToID("_Phase");

    private void Reset()
    {
        AutoResolveSourceRenderer();
    }

    private void Awake()
    {
        AutoResolveSourceRenderer();
    }

    private void AutoResolveSourceRenderer()
    {
        if (sourceRenderer != null) return;

        // Prefer parent's SpriteRenderer first
        if (transform.parent != null)
        {
            sourceRenderer = transform.parent.GetComponent<SpriteRenderer>();
            if (sourceRenderer != null) return;
        }

        // Fallback to self
        sourceRenderer = GetComponent<SpriteRenderer>();
    }

    public void StartKnockOut()
    {
        AutoResolveSourceRenderer();

        if (sourceRenderer == null)
        {
            Debug.LogWarning("[PlayerSoulEchoController] StartKnockOut: sourceRenderer is null.", this);
            return;
        }

        ClearEchoes();

        Sprite sprite = sourceRenderer.sprite;
        if (sprite == null)
        {
            Debug.LogWarning("[PlayerSoulEchoController] StartKnockOut: source sprite is null.", this);
            return;
        }

        _basePosition = sourceRenderer.transform.position;
        bool flipX = sourceRenderer.flipX;
        bool flipY = sourceRenderer.flipY;
        int baseLayer = sourceRenderer.sortingLayerID;
        int baseOrder = sourceRenderer.sortingOrder;
        Transform srcTf = sourceRenderer.transform;

        Material baseMat = soulMaterial != null ? soulMaterial : sourceRenderer.sharedMaterial;

        for (int side = -1; side <= 1; side += 2)
        {
            float sideSign = side;

            for (int i = 1; i <= echoesPerSide; i++)
            {
                GameObject echoGO = new GameObject($"SoulEcho_{sideSign}_{i}");
                echoGO.transform.position = _basePosition;
                echoGO.transform.rotation = srcTf.rotation;
                echoGO.transform.localScale = srcTf.lossyScale;

                var sr = echoGO.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.flipX = flipX;
                sr.flipY = flipY;
                sr.sortingLayerID = baseLayer;
                sr.sortingOrder = baseOrder + sortingOrderOffset;

                if (baseMat != null)
                    sr.material = new Material(baseMat);

                var c = sr.color;
                c.a = 1f;
                sr.color = c;

                if (sr.material != null && sr.material.HasProperty(PhaseID))
                    sr.material.SetFloat(PhaseID, 0f);

                _echoes.Add(new Echo
                {
                    tf = echoGO.transform,
                    sr = sr,
                    sideSign = sideSign,
                    index = i
                });
            }
        }

        if (_knockCo != null) StopCoroutine(_knockCo);
        _knockCo = StartCoroutine(CoKnockOut());
    }

    public void StartReturn()
    {
        if (_echoes.Count == 0)
        {
            Debug.LogWarning("[PlayerSoulEchoController] StartReturn: no echoes exist.", this);
            return;
        }

        if (_returnCo != null) StopCoroutine(_returnCo);
        _returnCo = StartCoroutine(CoReturn());
    }

    private IEnumerator CoKnockOut()
    {
        if (_echoes.Count == 0 || knockDuration <= 0f)
            yield break;

        Vector2 dir = knockDirection.sqrMagnitude > 1e-4f
            ? knockDirection.normalized
            : Vector2.right;

        float t = 0f;
        while (t < knockDuration)
        {
            float k = knockCurve != null
                ? knockCurve.Evaluate(Mathf.Clamp01(t / knockDuration))
                : Mathf.Clamp01(t / knockDuration);

            foreach (var e in _echoes)
            {
                float dist = echoSpacing * e.index;
                Vector3 offset = (Vector3)(dir * dist * e.sideSign * k);
                e.tf.position = _basePosition + offset;

                if (e.sr != null && e.sr.material != null && e.sr.material.HasProperty(PhaseID))
                    e.sr.material.SetFloat(PhaseID, k);
            }

            t += Time.deltaTime;
            yield return null;
        }

        foreach (var e in _echoes)
        {
            if (e.tf == null) continue;

            float dist = echoSpacing * e.index;
            Vector3 offset = (Vector3)(dir * dist * e.sideSign);
            e.tf.position = _basePosition + offset;

            if (e.sr != null && e.sr.material != null && e.sr.material.HasProperty(PhaseID))
                e.sr.material.SetFloat(PhaseID, 1f);
        }

        _knockCo = null;
    }

    private IEnumerator CoReturn()
    {
        if (_echoes.Count == 0 || returnDuration <= 0f)
        {
            ClearEchoes();
            yield break;
        }

        Vector2 dir = knockDirection.sqrMagnitude > 1e-4f
            ? knockDirection.normalized
            : Vector2.right;

        float t = 0f;
        while (t < returnDuration)
        {
            float k = returnCurve != null
                ? returnCurve.Evaluate(Mathf.Clamp01(t / returnDuration))
                : Mathf.Clamp01(t / returnDuration);

            float backFactor = 1f - k;
            float alpha = backFactor;

            foreach (var e in _echoes)
            {
                if (e.tf == null || e.sr == null) continue;

                float dist = echoSpacing * e.index;
                Vector3 offset = (Vector3)(dir * dist * e.sideSign * backFactor);
                e.tf.position = _basePosition + offset;

                var c = e.sr.color;
                c.a = alpha;
                e.sr.color = c;

                if (e.sr.material != null && e.sr.material.HasProperty(PhaseID))
                    e.sr.material.SetFloat(PhaseID, backFactor);
            }

            t += Time.deltaTime;
            yield return null;
        }

        ClearEchoes();
        _returnCo = null;
    }

    private void ClearEchoes()
    {
        foreach (var e in _echoes)
        {
            if (e != null && e.tf != null)
                Destroy(e.tf.gameObject);
        }
        _echoes.Clear();
    }
}