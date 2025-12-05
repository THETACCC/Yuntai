using UnityEngine;

/// Drives the _BuildProgress parameter on the SpiderWeb material, so the web "grows" from the center outward.
public class SpiderWebAnimator : MonoBehaviour
{
    public Material spiderWebMaterial;

    public float buildDuration = 3f;

    [Tooltip("Softness of the build edge (matches shader _BuildFeather)")]
    [Range(0.0f, 0.25f)]
    public float buildFeather = 0.03f;

    public bool playOnStart = true;

    private float elapsed = 0f;
    private bool isPlaying = false;

    private static int PropBuildProgress = Shader.PropertyToID("_BuildProgress");
    private static int PropBuildFeather = Shader.PropertyToID("_BuildFeather");

    private void Start()
    {
        if (spiderWebMaterial != null)
        {
            spiderWebMaterial.SetFloat(PropBuildProgress, 0f);
            spiderWebMaterial.SetFloat(PropBuildFeather, buildFeather);
        }

        if (playOnStart)
            Play();
    }

    /// start building the web
    public void Play()
    {
        if (spiderWebMaterial == null)
        {
            Debug.LogWarning("no material assigned");
            return;
        }

        elapsed = 0f;
        isPlaying = true;
        spiderWebMaterial.SetFloat(PropBuildProgress, 0f);
    }

    private void Update()
    {
        if (!isPlaying || spiderWebMaterial == null)
            return;

        elapsed += Time.deltaTime;
        float t = buildDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / buildDuration);

        spiderWebMaterial.SetFloat(PropBuildProgress, t);
        spiderWebMaterial.SetFloat(PropBuildFeather, buildFeather);

        if (t >= 1f)
            isPlaying = false;
    }
}
