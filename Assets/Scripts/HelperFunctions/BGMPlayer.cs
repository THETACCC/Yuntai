using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BGMPlayer : MonoBehaviour
{
    public static BGMPlayer Instance;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip bgmClip;

    [Header("Scene Settings")]
    [SerializeField] private string[] playSceneNames;
    [SerializeField] private string[] stopSceneNames;

    private void Awake()
    {

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        audioSource.clip = bgmClip;
        audioSource.loop = true;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        string sceneName = scene.name;

        if (SceneNameExists(sceneName, playSceneNames))
        {
            PlayMusic();
        }
        else if (SceneNameExists(sceneName, stopSceneNames))
        {
            StopMusic();
        }
    }

    private bool SceneNameExists(string sceneName, string[] sceneList)
    {
        foreach (string name in sceneList)
        {
            if (sceneName == name)
                return true;
        }

        return false;
    }

    public void PlayMusic()
    {
        if (!audioSource.isPlaying)
            audioSource.Play();
    }

    public void StopMusic()
    {
        if (audioSource.isPlaying)
            audioSource.Stop();
    }
}
