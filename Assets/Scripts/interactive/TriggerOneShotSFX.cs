using UnityEngine;

/// <summary>
/// 通用 "播一次音效" 组件——挂在任意物体上，由 UnityEvent / Animation Event / 别的脚本
/// 调用 Play() 即可。clipName 是 Resources 下的相对路径，跟 AudioManager 约定一致。
/// </summary>
public class TriggerOneShotSFX : MonoBehaviour
{
    [Tooltip("Resources 下的相对路径，例如 Sound Effects/Chapter3/sndShadowVanish")]
    [SerializeField] private string clipName;

    [SerializeField] private AudioManager.AudioGroup group = AudioManager.AudioGroup.SFX;

    [Tooltip("是否在 Start 时预热（首次 Resources.Load 的磁盘读取放在场景加载时一次性完成，避免触发瞬间 hitch）")]
    [SerializeField] private bool preloadOnStart = true;

    private void Start()
    {
        if (preloadOnStart && !string.IsNullOrEmpty(clipName))
            AudioManager.Preload(clipName);
    }

    /// <summary>UnityEvent 默认调用入口。</summary>
    public void Play()
    {
        if (string.IsNullOrEmpty(clipName)) return;
        AudioManager.PlayOneShot(clipName, group);
    }

    /// <summary>临时覆盖 clipName 的入口，需要从代码动态指定时使用。</summary>
    public void PlayWithClip(string overrideClipName)
    {
        if (string.IsNullOrEmpty(overrideClipName)) return;
        AudioManager.PlayOneShot(overrideClipName, group);
    }
}
