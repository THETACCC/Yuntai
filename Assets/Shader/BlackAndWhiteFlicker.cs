using UnityEngine;

public class BlackAndWhiteFlicker : MonoBehaviour
{
    [Tooltip("ShaderLab/BlackAndWhite")]
    public Material bwMaterial;

    [Range(0f, 1f)]
    public float defaultIntensity = 0f; //default off

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

    //0=fully closed，1=black and white
    public void SetIntensity(float value)
    {
        if (!bwMaterial) return;
        bwMaterial.SetFloat(EffectIntensityID, Mathf.Clamp01(value));
    }
}
