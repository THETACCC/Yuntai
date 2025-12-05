using UnityEngine;

public class SpriteEdgeGlitchGroup : MonoBehaviour
{
    [Header("Overlay Material")]
    [SerializeField] private Material edgeWaveMaterial;

    private bool includeInactive = true;

    private static readonly int RandomSeedID = Shader.PropertyToID("_RandomSeed");

    private void Awake()
    {
        if (edgeWaveMaterial == null)
        {
            Debug.LogError("Please assign the edgeWaveMaterial.");
            enabled = false;
            return;
        }

        var baseRenderers = GetComponentsInChildren<SpriteRenderer>(includeInactive);

        foreach (var baseSr in baseRenderers)
        {
            if (baseSr == null) continue;

            // Create overlay child object
            GameObject overlayObj = new GameObject(baseSr.gameObject.name + "_EdgeWaveOverlay");
            overlayObj.transform.SetParent(baseSr.transform, worldPositionStays: false); //Parents it to the original sprite’s transform
            //Resets local transform so it perfectly overlaps
            overlayObj.transform.localPosition = Vector3.zero;
            overlayObj.transform.localRotation = Quaternion.identity;
            overlayObj.transform.localScale = Vector3.one;

            var overlaySr = overlayObj.AddComponent<SpriteRenderer>();
            overlaySr.sprite = baseSr.sprite;
            overlaySr.flipX = baseSr.flipX;
            overlaySr.flipY = baseSr.flipY;
            overlaySr.sortingLayerID = baseSr.sortingLayerID;
            overlaySr.sortingOrder = baseSr.sortingOrder + 1;
            overlaySr.sharedMaterial = edgeWaveMaterial;
            overlaySr.color = Color.white;

            // Give each overlay its own random seed
            var block = new MaterialPropertyBlock();
            overlaySr.GetPropertyBlock(block);

            float seed = Random.Range(0f, 1000f);
            block.SetFloat(RandomSeedID, seed);

            overlaySr.SetPropertyBlock(block);
        }
    }
}
