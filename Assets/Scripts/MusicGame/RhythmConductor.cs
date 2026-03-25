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
    [SerializeField] private TextAsset perfectDialogueFile;
    [SerializeField] private float resultDelayAfterLastNote = 1f;

    static readonly NoteEvent[] CHART = new[]
    {
        new NoteEvent { time = 1.857, lane = 0 },
        new NoteEvent { time = 2.194, lane = 1 },
        new NoteEvent { time = 2.523, lane = 2 },
        new NoteEvent { time = 2.860, lane = 3 },
        new NoteEvent { time = 3.214, lane = 4 },
        new NoteEvent { time = 3.542, lane = 5 },

        new NoteEvent { time = 4.405, lane = 1 },
        new NoteEvent { time = 4.762, lane = 2 },

        new NoteEvent { time = 5.654, lane = 4 },

        new NoteEvent { time = 7.002, lane = 1 },

        new NoteEvent { time = 8.252, lane = 4 },

        new NoteEvent { time = 9.567, lane = 1 },

        new NoteEvent { time = 10.520, lane = 2 },
        new NoteEvent { time = 11.145, lane = 3 },
        new NoteEvent { time = 11.835, lane = 4 },
        new NoteEvent { time = 12.520, lane = 5 },

        new NoteEvent { time = 13.084, lane = 0 },
        new NoteEvent { time = 13.775, lane = 1 },
        new NoteEvent { time = 14.410, lane = 2 },
        new NoteEvent { time = 15.049, lane = 3 },
        new NoteEvent { time = 15.700, lane = 4 },

        new NoteEvent { time = 16.425, lane = 0 },
        new NoteEvent { time = 16.767, lane = 1 },
        new NoteEvent { time = 17.108, lane = 2 },
        new NoteEvent { time = 17.467, lane = 3 },
        new NoteEvent { time = 17.783, lane = 4 },

        new NoteEvent { time = 18.923, lane = 0 },
        new NoteEvent { time = 19.572, lane = 1 },

        new NoteEvent { time = 21.067, lane = 3 },
        new NoteEvent { time = 21.375, lane = 4 },
        new NoteEvent { time = 21.682, lane = 5 },

        new NoteEvent { time = 22.519, lane = 1 },
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

    void Awake()
    {
        BuildNotes();
    }

    public void BeginGame()
    {
        spawnIndex = 0;
        missCount = 0;
        perfectCount = 0;
        goodCount = 0;

        hasEnded = false;
        isPlaying = true;
        resultCountdownStarted = false;
        resultCountdownTimer = 0f;

        for (int i = active.Count - 1; i >= 0; i--)
            if (active[i] != null) Destroy(active[i].gameObject);
        active.Clear();

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
        Debug.Log($"EMPTY -> MISS {missCount}/{maxMiss}");
        if (missCount >= maxMiss) GameOver();
    }

    void HandleMiss(NoteView _)
    {
        missCount++;
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

                if (missCount == 0)
                {
                    Debug.Log("SONG CLEAR -> PERFECT");
                    PlayResultDialogue(perfectDialogueFile);
                }
                else
                {
                    Debug.Log("SONG CLEAR -> NORMAL");
                    PlayResultDialogue(normalDialogueFile);
                }
            }
        }
        else
        {
            resultCountdownStarted = false;
        }
    }

    void PlayResultDialogue(TextAsset file)
    {
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