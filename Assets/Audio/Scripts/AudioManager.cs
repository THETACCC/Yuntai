using System.Collections.Generic;
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

    private static Dictionary<string, AudioClip> audioClipDict = new Dictionary<string, AudioClip>();
    private static Dictionary<string, AudioSource> audioSourceDict = new Dictionary<string, AudioSource>();

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

    /// <summary>
    /// Play a clip for one time
    /// </summary>
    /// <param name="clipName"></param>
    public static void Play(string clipName)
    {
        //check if there is a audio source object
        AudioSource audioSource;

        if (!audioSourceDict.ContainsKey(clipName))
        {
            audioSource = new GameObject("Audio_" +clipName).AddComponent<AudioSource>();
            audioSourceDict.Add(clipName, audioSource);
        }
        audioSource = audioSourceDict[clipName];

        //check if clip has been loaded
        AudioClip clip;

        if (!audioClipDict.ContainsKey(clipName))
        {
            clip = Resources.Load<AudioClip>(clipName);
            if (clip == null)
            {
                Debug.LogError("Clip <" + clipName + "> cannot be found in Resources folder");
            }
            audioClipDict.Add(clipName, clip);
        }
        clip = audioClipDict[clipName];

        audioSource.clip = clip;
        audioSource.Play();
    }

    public static void Stop(string clipName)
    {
        if (CheckClipExistence(clipName))
        {
            AudioSource audioSource = audioSourceDict[clipName];
            audioSource.Stop();
        }
    }

    public static void SetVolume(string clipName, float volume)
    {
        if (CheckClipExistence(clipName))
        {
            AudioSource audioSource = audioSourceDict[clipName];
            audioSource.volume = volume;
        }
    }

    private static bool CheckClipExistence(string clipName)
    {
        //check if there is a audio source object
        AudioSource audioSource;

        if (!audioSourceDict.ContainsKey(clipName))
        {
            Debug.LogError("AudioSource GameObject <Audio_" + clipName + "> cannot be found in current scene");
            return false;
        }
        audioSource = audioSourceDict[clipName];

        //check if clip has been loaded
        if (!audioClipDict.ContainsKey(clipName))
        {
            Debug.LogError("AudioClip <" + clipName + "> has not been loaded.");
            return false;
        }

        if (audioSource.clip.name != clipName)
        {
            Debug.LogError("AudioClip <" + clipName + "> cannot be found in AudioSource GameObject < Audio_" + clipName + " >");
            return false;
        }

        return true;
    }


    #region OldAudioManager
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

    #endregion
}