using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RhythmConductor : MonoBehaviour
{
    [Header("Audio")]
    public AudioSource music;

    [Header("Refs (UI)")]
    public RectTransform noteParent;
    public NoteView notePrefab;

    [Header("Lane Anchors (size must be 6)")]
    public RectTransform[] laneAnchors; // index 0..5

    [Header("Timing")]
    public double preSpawnTime = 1.2;

    [Tooltip("Good window (seconds). If delta <= hitWindow => at least Good.")]
    public double hitWindow = 0.18;

    [Tooltip("Perfect window (seconds). If delta <= perfectWindow => Perfect. Must be <= hitWindow.")]
    public double perfectWindow = 0.06;

    [Header("Fail")]
    public int maxMiss = 10;
    public bool emptyPressCountsAsMiss = false;

    [Header("Timing Calibration")]
    public double globalOffsetSeconds = 0.0;

    [Header("Result Dialogue")]
    [SerializeField] private DialogueTrigger resultDialogueTrigger;
    [SerializeField] private TextAsset lostDialogueFile;
    [SerializeField] private TextAsset normalDialogueFile;
    [SerializeField] private float resultDelayAfterLastNote = 6.617f;

    [System.Serializable]
    public class TimedUIShake
    {
        public float time;
        public float strength = 20f;
        public float duration = 0.35f;
    }

    [System.Serializable]
    public class SustainedUIShake
    {
        public float startTime;
        public float endTime;
        public float strength = 6f;
    }

    [System.Serializable]
    public class TimedAnimatorSwitch
    {
        public float time;
        public string stateName;
    }

    [Header("UI Shake")]
    [SerializeField] private RectTransform uiShakeTarget;

    [Header("Miss Feedback")]
    [SerializeField] private bool shakeOnMiss = true;
    [SerializeField] private float missShakeStrength = 14f;
    [SerializeField] private float missShakeDuration = 0.18f;
    [SerializeField] private GameObject missFlashObject;
    [SerializeField] private float missFlashDuration = 0.2f;

    [Header("Timed Animator Switch")]
    [SerializeField] private Animator targetAnimator;

    private readonly TimedAnimatorSwitch[] timedAnimatorSwitches = new TimedAnimatorSwitch[]
    {
        new TimedAnimatorSwitch
        {
            time = 28.000f,
            stateName = "Crowd_SmallSmile"
        },
        new TimedAnimatorSwitch
        {
            time = 59.780f,
            stateName = "Crowd_SmileBig"
        }
    };

    private readonly TimedUIShake[] timedShakes = new TimedUIShake[]
    {
        /*
        new TimedUIShake
        {
            time = 5f,
            strength = 3f,
            duration = 6f
        },
        */
        new TimedUIShake
        {
            time = 44.071f,
            strength = 17f,
            duration = 0.4f
        },
        new TimedUIShake
        {
            time = 61.980f,   // 1m 01.980
            strength = 17f,
            duration = 0.4f
        }
    };

    private readonly SustainedUIShake[] sustainedShakes = new SustainedUIShake[]
    {
        new SustainedUIShake
        {
            startTime = 73.895f, // 1m 13.895
            endTime   = 79.675f, // 1m 19.675
            strength  = 2.5f
        }
    };

    private int nextShakeIndex;
    private int nextSustainedShakeIndex;
    private int nextAnimatorSwitchIndex;

    private bool isSustainedShaking;
    private SustainedUIShake currentSustainedShake;

    private Coroutine uiShakeRoutine;
    private Coroutine missFlashRoutine;
    private Vector2 uiShakeBasePos;

    static readonly NoteEvent[] CHART = new[]
    {
        new NoteEvent { time = 11.018, lane = 1 },
        new NoteEvent { time = 11.859, lane = 2 },
        new NoteEvent { time = 12.735, lane = 3 },
        new NoteEvent { time = 13.631, lane = 4 },
        new NoteEvent { time = 14.416, lane = 5 },

        new NoteEvent { time = 16.042, lane = 2 },
        new NoteEvent { time = 17.778, lane = 3 },
        new NoteEvent { time = 19.350, lane = 4 },

        new NoteEvent { time = 21.140, lane = 1 },
        new NoteEvent { time = 21.743, lane = 3 },
        new NoteEvent { time = 22.035, lane = 4 },

        new NoteEvent { time = 23.479, lane = 3 },
        new NoteEvent { time = 23.698, lane = 4 },

        new NoteEvent { time = 24.502, lane = 1 },
        new NoteEvent { time = 25.160, lane = 3 },
        new NoteEvent { time = 25.397, lane = 4 },

        new NoteEvent { time = 26.220, lane = 1 },
        new NoteEvent { time = 26.877, lane = 3 },
        new NoteEvent { time = 27.078, lane = 4 },

        new NoteEvent { time = 31.244, lane = 0 },
        new NoteEvent { time = 32.121, lane = 1 },
        new NoteEvent { time = 32.980, lane = 2 },
        new NoteEvent { time = 33.784, lane = 3 },
        new NoteEvent { time = 34.625, lane = 4 },
        new NoteEvent { time = 35.483, lane = 5 },

        new NoteEvent { time = 36.306, lane = 0 },
        new NoteEvent { time = 37.125, lane = 1 },
        new NoteEvent { time = 37.987, lane = 2 },
        new NoteEvent { time = 38.864, lane = 3 },
        new NoteEvent { time = 39.704, lane = 4 },
        new NoteEvent { time = 40.563, lane = 5 },

        new NoteEvent { time = 46.501, lane = 1 },
        new NoteEvent { time = 47.049, lane = 3 },
        new NoteEvent { time = 47.305, lane = 4 },
        new NoteEvent { time = 47.488, lane = 5 },

        new NoteEvent { time = 48.145, lane = 1 },
        new NoteEvent { time = 48.748, lane = 3 },
        new NoteEvent { time = 49.077, lane = 4 },
        new NoteEvent { time = 49.242, lane = 5 },

        new NoteEvent { time = 49.735, lane = 1 },
        new NoteEvent { time = 50.567, lane = 3 },
        new NoteEvent { time = 50.740, lane = 4 },
        new NoteEvent { time = 50.886, lane = 5 },

        new NoteEvent { time = 51.544, lane = 1 },
        new NoteEvent { time = 52.384, lane = 3 },
        new NoteEvent { time = 52.585, lane = 4 },
        new NoteEvent { time = 52.823, lane = 5 },

        new NoteEvent { time = 53.261, lane = 1 },
        new NoteEvent { time = 53.846, lane = 3 },
        new NoteEvent { time = 54.084, lane = 4 },
        new NoteEvent { time = 54.321, lane = 5 },

        new NoteEvent { time = 54.942, lane = 1 },
        new NoteEvent { time = 55.564, lane = 3 },
        new NoteEvent { time = 55.763, lane = 4 },
        new NoteEvent { time = 56.039, lane = 5 },

        new NoteEvent { time = 56.587, lane = 1 },
        new NoteEvent { time = 57.172, lane = 3 },
        new NoteEvent { time = 57.446, lane = 4 },
        new NoteEvent { time = 57.720, lane = 5 },

        new NoteEvent { time = 58.286, lane = 1 },
        new NoteEvent { time = 58.926, lane = 3 },
        new NoteEvent { time = 59.163, lane = 4 },
        new NoteEvent { time = 59.437, lane = 5 },

        new NoteEvent { time = 66.719, lane = 1 },
        new NoteEvent { time = 67.053, lane = 2 },
        new NoteEvent { time = 67.553, lane = 4 },
        new NoteEvent { time = 67.971, lane = 5 },

        new NoteEvent { time = 68.360, lane = 1 },
        new NoteEvent { time = 68.860, lane = 2 },
        new NoteEvent { time = 69.277, lane = 4 },
        new NoteEvent { time = 69.639, lane = 5 },

        new NoteEvent { time = 70.084, lane = 1 },
        new NoteEvent { time = 70.528, lane = 2 },
        new NoteEvent { time = 70.973, lane = 4 },
        new NoteEvent { time = 71.418, lane = 5 },

        new NoteEvent { time = 71.807, lane = 1 },
        new NoteEvent { time = 72.196, lane = 2 },
        new NoteEvent { time = 72.641, lane = 4 },
        new NoteEvent { time = 73.058, lane = 5 },
    };

    readonly List<NoteEvent> notes = new();
    readonly List<NoteView> active = new();

    int spawnIndex;
    int missCount;
    int perfectCount;
    int goodCount;

    bool isPlaying;
    bool hasEnded;
    bool resultCountdownStarted;

    float resultCountdownTimer;
    double songDspStart;

    void Start()
    {
        BuildNotes();

        if (uiShakeTarget == null)
            print("No UI Shake assigned!!!");

        if (uiShakeTarget != null)
            uiShakeBasePos = uiShakeTarget.anchoredPosition;

        if (missFlashObject != null)
            missFlashObject.SetActive(false);
    }

    public void BeginGame()
    {
        spawnIndex = 0;
        missCount = 0;
        perfectCount = 0;
        goodCount = 0;

        nextShakeIndex = 0;
        nextSustainedShakeIndex = 0;
        nextAnimatorSwitchIndex = 0;

        isSustainedShaking = false;
        currentSustainedShake = null;

        hasEnded = false;
        isPlaying = true;
        resultCountdownStarted = false;
        resultCountdownTimer = 0f;

        for (int i = active.Count - 1; i >= 0; i--)
            if (active[i] != null) Destroy(active[i].gameObject);
        active.Clear();

        StopUIShake();
        StopMissFlash();

        if (music == null)
        {
            Debug.LogError("[RhythmConductor] music is NULL.");
            isPlaying = false;
            return;
        }

        if (laneAnchors == null || laneAnchors.Length != 6)
        {
            Debug.LogError("[RhythmConductor] laneAnchors must have 6 elements (0..5).");
            isPlaying = false;
            return;
        }

        if (perfectWindow > hitWindow) perfectWindow = hitWindow;

        music.Stop();
        music.time = 0f;

        songDspStart = AudioSettings.dspTime + 0.05;
        music.PlayScheduled(songDspStart);
    }

    void Update()
    {
        if (!isPlaying || hasEnded) return;

        double now = AudioSettings.dspTime;

        CheckTimedAnimatorSwitch(now);
        CheckTimedUIShake(now);
        CheckSustainedUIShake(now);

        while (spawnIndex < notes.Count)
        {
            var e = notes[spawnIndex];

            double noteTime = songDspStart + (e.time + globalOffsetSeconds);
            double spawnTime = noteTime - preSpawnTime;

            if (now >= spawnTime)
            {
                SpawnOne(e, noteTime, spawnTime);
                spawnIndex++;
            }
            else break;
        }

        if (Input.GetKeyDown(KeyCode.Space))
            TryHit(now);

        active.RemoveAll(n => n == null || n.IsJudged);

        CheckResultCountdown();
    }

    void CheckTimedAnimatorSwitch(double nowDsp)
    {
        if (targetAnimator == null || timedAnimatorSwitches == null || timedAnimatorSwitches.Length == 0)
            return;

        while (nextAnimatorSwitchIndex < timedAnimatorSwitches.Length)
        {
            var s = timedAnimatorSwitches[nextAnimatorSwitchIndex];
            double switchDspTime = songDspStart + s.time + globalOffsetSeconds;

            if (nowDsp >= switchDspTime)
            {
                if (!string.IsNullOrEmpty(s.stateName))
                    targetAnimator.Play(s.stateName, 0, 0f);

                nextAnimatorSwitchIndex++;
            }
            else
            {
                break;
            }
        }
    }

    void CheckTimedUIShake(double nowDsp)
    {
        if (uiShakeTarget == null || timedShakes == null || timedShakes.Length == 0)
            return;

        while (nextShakeIndex < timedShakes.Length)
        {
            var s = timedShakes[nextShakeIndex];
            double shakeDspTime = songDspStart + s.time + globalOffsetSeconds;

            if (nowDsp >= shakeDspTime)
            {
                StartOneShotUIShake(s.strength, s.duration);
                nextShakeIndex++;
            }
            else
            {
                break;
            }
        }
    }

    void CheckSustainedUIShake(double nowDsp)
    {
        if (uiShakeTarget == null || sustainedShakes == null || sustainedShakes.Length == 0)
            return;

        if (isSustainedShaking && currentSustainedShake != null)
        {
            double endDspTime = songDspStart + currentSustainedShake.endTime + globalOffsetSeconds;

            if (nowDsp >= endDspTime)
            {
                StopUIShake();
                isSustainedShaking = false;
                currentSustainedShake = null;
            }
        }

        if (!isSustainedShaking && nextSustainedShakeIndex < sustainedShakes.Length)
        {
            var s = sustainedShakes[nextSustainedShakeIndex];
            double startDspTime = songDspStart + s.startTime + globalOffsetSeconds;
            double endDspTime = songDspStart + s.endTime + globalOffsetSeconds;

            if (nowDsp >= startDspTime && nowDsp < endDspTime)
            {
                StartSustainedUIShake(s.strength);
                currentSustainedShake = s;
                isSustainedShaking = true;
                nextSustainedShakeIndex++;
            }
        }
    }

    void StartOneShotUIShake(float strength, float duration)
    {
        if (uiShakeTarget == null) return;

        if (uiShakeRoutine != null)
            StopCoroutine(uiShakeRoutine);

        uiShakeRoutine = StartCoroutine(CoOneShotUIShake(strength, duration));
    }

    void StartSustainedUIShake(float strength)
    {
        if (uiShakeTarget == null) return;

        if (uiShakeRoutine != null)
            StopCoroutine(uiShakeRoutine);

        uiShakeRoutine = StartCoroutine(CoSustainedUIShake(strength));
    }

    void StopUIShake()
    {
        if (uiShakeRoutine != null)
            StopCoroutine(uiShakeRoutine);

        uiShakeRoutine = null;

        if (uiShakeTarget != null)
            uiShakeTarget.anchoredPosition = uiShakeBasePos;
    }

    void TriggerMissFeedback()
    {
        if (shakeOnMiss)
            StartOneShotUIShake(missShakeStrength, missShakeDuration);

        if (missFlashObject != null)
        {
            if (missFlashRoutine != null)
                StopCoroutine(missFlashRoutine);

            missFlashRoutine = StartCoroutine(CoMissFlash());
        }
    }

    void StopMissFlash()
    {
        if (missFlashRoutine != null)
            StopCoroutine(missFlashRoutine);

        missFlashRoutine = null;

        if (missFlashObject != null)
            missFlashObject.SetActive(false);
    }

    IEnumerator CoMissFlash()
    {
        missFlashObject.SetActive(true);
        yield return new WaitForSeconds(missFlashDuration);
        missFlashObject.SetActive(false);
        missFlashRoutine = null;
    }

    IEnumerator CoOneShotUIShake(float strength, float duration)
    {
        uiShakeBasePos = uiShakeTarget.anchoredPosition;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            Vector2 offset = Random.insideUnitCircle * strength;
            uiShakeTarget.anchoredPosition = uiShakeBasePos + offset;
            yield return null;
        }

        uiShakeTarget.anchoredPosition = uiShakeBasePos;
        uiShakeRoutine = null;
    }

    IEnumerator CoSustainedUIShake(float strength)
    {
        uiShakeBasePos = uiShakeTarget.anchoredPosition;

        while (true)
        {
            Vector2 offset = Random.insideUnitCircle * strength;
            uiShakeTarget.anchoredPosition = uiShakeBasePos + offset;
            yield return null;
        }
    }

    void SpawnOne(NoteEvent e, double noteTime, double spawnTime)
    {
        int lane = Mathf.Clamp(e.lane, 0, 5);
        var anchor = laneAnchors[lane];
        if (anchor == null) return;

        var n = Instantiate(notePrefab, noteParent);
        n.Init(noteTime, spawnTime, preSpawnTime, hitWindow);

        n.SetAnchoredPosition(laneAnchors[lane].anchoredPosition);

        n.OnMiss = HandleMiss;
        n.OnJudged = HandleJudged;

        active.Add(n);
    }

    void TryHit(double now)
    {
        NoteView next = null;
        double nextTime = double.MaxValue;

        for (int i = 0; i < active.Count; i++)
        {
            var n = active[i];
            if (n == null || n.IsJudged) continue;
            if (n.NoteTime < nextTime)
            {
                nextTime = n.NoteTime;
                next = n;
            }
        }

        if (next == null)
        {
            HandleEmptyPress();
            return;
        }

        double delta = System.Math.Abs(now - next.NoteTime);

        if (delta <= hitWindow)
        {
            var judge = (delta <= perfectWindow) ? HitJudgement.Perfect : HitJudgement.Good;
            next.RegisterHit(now, judge);
        }
        else
        {
            HandleEmptyPress();
        }
    }

    void HandleJudged(NoteView _, HitJudgement j)
    {
        if (j == HitJudgement.Perfect)
        {
            perfectCount++;
            Debug.Log($"PERFECT  (P:{perfectCount} G:{goodCount} M:{missCount}/{maxMiss})");
        }
        else if (j == HitJudgement.Good)
        {
            goodCount++;
            Debug.Log($"GOOD     (P:{perfectCount} G:{goodCount} M:{missCount}/{maxMiss})");
        }
    }

    void HandleEmptyPress()
    {
        if (!emptyPressCountsAsMiss) return;

        missCount++;
        TriggerMissFeedback();
        Debug.Log($"EMPTY -> MISS {missCount}/{maxMiss}");
        if (missCount >= maxMiss) GameOver();
    }

    void HandleMiss(NoteView _)
    {
        missCount++;
        TriggerMissFeedback();
        Debug.Log($"MISS {missCount}/{maxMiss}");
        if (missCount >= maxMiss) GameOver();
    }

    void GameOver()
    {
        if (hasEnded) return;

        hasEnded = true;
        isPlaying = false;
        resultCountdownStarted = false;

        if (music != null) music.Stop();
        StopUIShake();
        StopMissFlash();

        Debug.Log("GAME OVER");
        PlayResultDialogue(lostDialogueFile);
    }

    void CheckResultCountdown()
    {
        if (hasEnded) return;

        bool allSpawned = spawnIndex >= notes.Count;

        bool noActiveNotes = true;
        for (int i = 0; i < active.Count; i++)
        {
            if (active[i] != null && !active[i].IsJudged)
            {
                noActiveNotes = false;
                break;
            }
        }

        if (allSpawned && noActiveNotes)
        {
            if (!resultCountdownStarted)
            {
                resultCountdownStarted = true;
                resultCountdownTimer = Mathf.Max(0f, resultDelayAfterLastNote);
            }

            resultCountdownTimer -= Time.deltaTime;
            if (resultCountdownTimer <= 0f)
            {
                hasEnded = true;
                isPlaying = false;

                if (music != null) music.Stop();
                StopUIShake();
                StopMissFlash();

                Debug.Log("SONG CLEAR -> NORMAL");
                PlayResultDialogue(normalDialogueFile);
            }
        }
        else
        {
            resultCountdownStarted = false;
        }
    }

    void PlayResultDialogue(TextAsset file)
    {
        targetAnimator.Play("Base Layer.Crowd", 0, 0f);

        if (resultDialogueTrigger == null)
        {
            Debug.LogError("[RhythmConductor] resultDialogueTrigger is not assigned.");
            return;
        }

        if (file == null)
        {
            Debug.LogError("[RhythmConductor] result dialogue file is null.");
            return;
        }

        resultDialogueTrigger.mainDialogueJsonFile = file;
        resultDialogueTrigger.isMainDialogueFinished = false;
        resultDialogueTrigger.TriggerDialogue();
    }

    void BuildNotes()
    {
        notes.Clear();
        notes.AddRange(CHART);
        notes.Sort((a, b) => a.time.CompareTo(b.time));
    }
}