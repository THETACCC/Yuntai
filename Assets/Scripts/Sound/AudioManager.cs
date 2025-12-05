using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    public static AudioManager instance { get; private set; }

    [Header("Audio Mixer Groups")]
    public AudioMixerGroup musicGroup;
    public AudioMixerGroup sfxGroup;
    public AudioMixerGroup uiGroup;

    [Header("Mixer")]
    public AudioMixer mainMixer;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // 启动时自动分配所有现有的AudioSource
        AssignAllAudioSources();
    }

    // 在场景加载后调用
    void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        // 场景加载后自动分配
        AssignAllAudioSources();
    }

    // 自动给所有AudioSource分配MixerGroup
    public void AssignAllAudioSources()
    {
        AudioSource[] allAudioSources = FindObjectsOfType<AudioSource>(true); // 包括未激活的

        foreach (AudioSource audioSource in allAudioSources)
        {
            // 如果已经有MixerGroup就跳过
            if (audioSource.outputAudioMixerGroup != null) continue;

            // 根据tag或名字自动分配（你可以自定义规则）
            if (audioSource.CompareTag("Audio_Music"))
            {
                audioSource.outputAudioMixerGroup = musicGroup;
            }
            else if (audioSource.CompareTag("Audio_UI"))
            {
                audioSource.outputAudioMixerGroup = uiGroup;
            }
            else if (audioSource.CompareTag("Audio_SFX"))
            {
                audioSource.outputAudioMixerGroup = sfxGroup;
            } 
            else
            {
                audioSource.outputAudioMixerGroup = mainMixer.outputAudioMixerGroup; // 默认
            }
        }
    }

    // 调整音量的便捷方法
    public void SetMasterVolume(float volume)
    {
        // 如果slider是0-100，先转换成0-1
        float normalizedVolume = volume / 100f;

        // 防止log10(0)错误，设置最小值
        float dbValue = normalizedVolume > 0.0001f ? Mathf.Log10(normalizedVolume) * 20 : -80f;
        mainMixer.SetFloat("MasterVolume", dbValue);
    }

    public void SetMusicVolume(float volume)
    {
        // 如果slider是0-100，先转换成0-1
        float normalizedVolume = volume / 100f;

        // 防止log10(0)错误，设置最小值
        float dbValue = normalizedVolume > 0.0001f ? Mathf.Log10(normalizedVolume) * 20 : -80f;
        mainMixer.SetFloat("MusicVolume", dbValue);
        //mainMixer.SetFloat("MusicVolume", Mathf.Log10(volume) * 20);
    }

    public void SetSFXVolume(float volume)
    {
        // 如果slider是0-100，先转换成0-1
        float normalizedVolume = volume / 100f;

        // 防止log10(0)错误，设置最小值
        float dbValue = normalizedVolume > 0.0001f ? Mathf.Log10(normalizedVolume) * 20 : -80f;
        mainMixer.SetFloat("SFXVolume", dbValue);
        //mainMixer.SetFloat("SFXVolume", Mathf.Log10(volume) * 20);
    }
}