using UnityEngine;

public class BlackAndWhiteFlicker : MonoBehaviour
{
    [Tooltip("使用 ShaderLab/BlackAndWhite 的后处理材质（Renderer Feature 用的那个）。")]
    public Material bwMaterial;

    [Range(0f, 1f)]
    public float defaultIntensity = 0f; // 默认先关掉

    private static readonly int EffectIntensityID = Shader.PropertyToID("_EffectIntensity");

    private void Awake()
    {
        if (bwMaterial != null)
        {
            bwMaterial.SetFloat(EffectIntensityID, Mathf.Clamp01(defaultIntensity));
        }
        else
        {
            Debug.LogWarning("[BlackAndWhiteFlicker] bwMaterial is null, please assign.", this);
        }
    }

    /// <summary>设置黑白效果强度：0=完全关闭，1=完全黑白。</summary>
    public void SetIntensity(float value)
    {
        if (!bwMaterial) return;
        bwMaterial.SetFloat(EffectIntensityID, Mathf.Clamp01(value));
    }
}
