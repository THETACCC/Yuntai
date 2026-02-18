using System.Collections;
using UnityEngine;

/// <summary>
/// Persistent ambient sound manager. Survives scene loads,
/// accepts a serialized AudioSource, supports fade/cross-fade.
/// </summary>
[DisallowMultipleComponent]
public class AmbientSound : MonoBehaviour
{
    public static AmbientSound I { get; private set; }

    [Header("Assign (optional)")]
    [Tooltip("If left empty, an AudioSource will be added automatically.")]
    [SerializeField] private AudioSource source;

    [Header("Defaults")]
    [SerializeField] private AudioClip startClip;
    [SerializeField, Range(0f, 1f)] private float startVolume = 0.6f;
    [SerializeField] private bool playOnAwake = true;
    [SerializeField] private bool loop = true;
    [SerializeField, Min(0f)] private float fadeInSeconds = 1.0f;

    private Coroutine fadeRoutine;

    private void Awake()
    {
        // Singleton: keep the first, destroy any duplicates
        if (I && I != this)
        {
            Destroy(gameObject);
            return;
        }
        I = this;
        DontDestroyOnLoad(gameObject);

        // Ensure we have an AudioSource
        if (!source) source = GetComponent<AudioSource>();
        if (!source) source = gameObject.AddComponent<AudioSource>();

        // Configure default 2D ambient settings
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0f; // 2D ambient
        source.volume = 0f;       // will fade in
        if (startClip) source.clip = startClip;

        if (playOnAwake && source.clip)
        {
            source.Stop();
            source.Play();
            FadeTo(startVolume, fadeInSeconds);
        }

        /*
        if (AudioManager.instance != null && AudioManager.instance.musicGroup != null)
        {
            source.outputAudioMixerGroup = AudioManager.instance.musicGroup;
        }
        */
    }

    private void OnValidate()
    {
        if (source)
        {
            source.loop = loop;
            source.spatialBlend = 0f; // keep ambient as 2D
        }
    }

    /// <summary>Instantly stop playback.</summary>
    public void StopImmediate()
    {
        if (!source) return;
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        source.Stop();
    }

    /// <summary>Fade current clip to a target volume.</summary>
    public void FadeTo(float targetVolume, float seconds = 0.5f)
    {
        if (!source) return;
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeVolumeRoutine(targetVolume, Mathf.Max(0f, seconds)));
    }

    /// <summary>Fade out and stop.</summary>
    public void FadeOut(float seconds = 0.5f)
    {
        if (!source) return;
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeOutRoutine(Mathf.Max(0f, seconds)));
    }

    /// <summary>
    /// Cross-fade to a new clip (or just switch with optional fade).
    /// </summary>
    public void SetClipAndPlay(AudioClip newClip, float targetVolume = 0.6f, float crossfadeSeconds = 1.0f, bool loopNew = true)
    {
        if (!source) return;
        if (!newClip)
        {
            Debug.LogWarning("[AmbientSound] SetClipAndPlay called with null clip.");
            return;
        }
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(CrossfadeRoutine(newClip, targetVolume, Mathf.Max(0f, crossfadeSeconds), loopNew));
    }

    /// <summary>
    /// Ensures the singleton exists; if not, creates a new GameObject with AmbientSound attached.
    /// Returns the instance.
    /// </summary>
    public static AmbientSound EnsureInstance()
    {
        if (I) return I;
        var go = new GameObject("~AmbientSound");
        I = go.AddComponent<AmbientSound>();
        return I;
    }

    // ----------------- Coroutines -----------------

    private IEnumerator FadeVolumeRoutine(float target, float seconds)
    {
        float start = source.volume;
        if (seconds <= 0f)
        {
            source.volume = target;
            yield break;
        }

        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            source.volume = Mathf.Lerp(start, target, t / seconds);
            yield return null;
        }
        source.volume = target;
        fadeRoutine = null;
    }

    private IEnumerator FadeOutRoutine(float seconds)
    {
        float start = source.volume;
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            source.volume = Mathf.Lerp(start, 0f, t / seconds);
            yield return null;
        }
        source.volume = 0f;
        source.Stop();
        fadeRoutine = null;
    }

    private IEnumerator CrossfadeRoutine(AudioClip newClip, float targetVolume, float seconds, bool newLoop)
    {
        // If nothing is playing, just set and fade in
        if (!source.isPlaying || !source.clip)
        {
            source.loop = newLoop;
            source.clip = newClip;
            source.volume = 0f;
            source.Play();
            yield return FadeVolumeRoutine(targetVolume, seconds);
            yield break;
        }

        float half = seconds * 0.5f;

        // Fade out current
        float startVol = source.volume;
        float t = 0f;
        while (t < half)
        {
            t += Time.unscaledDeltaTime;
            source.volume = Mathf.Lerp(startVol, 0f, (half <= 0f) ? 1f : (t / half));
            yield return null;
        }

        // Switch clip
        source.loop = newLoop;
        source.clip = newClip;
        if (!source.isPlaying) source.Play();

        // Fade in new
        t = 0f;
        while (t < half)
        {
            t += Time.unscaledDeltaTime;
            source.volume = Mathf.Lerp(0f, targetVolume, (half <= 0f) ? 1f : (t / half));
            yield return null;
        }
        source.volume = targetVolume;
        fadeRoutine = null;
    }
}
