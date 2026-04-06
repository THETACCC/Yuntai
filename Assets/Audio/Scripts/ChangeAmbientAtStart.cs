using UnityEngine;

public class ChangeAmbientAtStart : MonoBehaviour
{
    [Header("Scene Start Ambient")]
    [SerializeField] private AudioClip clip;          // 本场景想要的BGM
    [SerializeField, Range(0f, 1f)] private float volume = 0.6f;
    [SerializeField, Min(0f)] private float crossfadeSeconds = 1.0f;
    [SerializeField] private bool loop = true;

    private void Awake()
    {
        // 确保常驻管理器存在（若首场景没手动放，也能自动创建）
        AmbientSound.EnsureInstance();
    }

    private void Start()
    {
        if (clip)
        {
            // 关键：开场时直接交叉淡入到本场景音乐
            AmbientSound.I.SetClipAndPlay(clip, volume, crossfadeSeconds, loop);
        }
        // 如果 clip 为空：什么都不做，沿用上个场景的音乐
    }
}
