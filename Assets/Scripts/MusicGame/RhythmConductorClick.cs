using System.Collections.Generic;
using UnityEngine;

public class RhythmConductorClick : MonoBehaviour
{
    [Header("Audio")]
    public AudioSource music;

    [Header("Refs (UI)")]
    public RectTransform noteParent;
    public NoteViewClick notePrefab;

    [Header("Lane Anchors (size must be 6)")]
    public RectTransform[] laneAnchors; // index 0..5

    [Header("Timing")]
    public double preSpawnTime = 1.2;
    public double hitWindow = 0.18;

    [Header("Fail")]
    public int maxMiss = 10;
    public bool wrongClickCountsAsMiss = true; // 点击但不在窗口内：是否算 miss

    [Header("Timing Calibration")]
    public double globalOffsetSeconds = 0.0;

    static readonly NoteEvent[] CHART = new[]
    {
        new NoteEvent { time = 1.857, lane = 0 },
        new NoteEvent { time = 2.194, lane = 1 },
        new NoteEvent { time = 2.523, lane = 2 },
        new NoteEvent { time = 2.860, lane = 3 },
        new NoteEvent { time = 3.214, lane = 4 },
        new NoteEvent { time = 3.542, lane = 5 },
        new NoteEvent { time = 4.405, lane = 2 },
        new NoteEvent { time = 4.762, lane = 3 },
        new NoteEvent { time = 5.654, lane = 3 },
        new NoteEvent { time = 7.002, lane = 2 },
        new NoteEvent { time = 8.252, lane = 3 },
        new NoteEvent { time = 9.567, lane = 2 },
        new NoteEvent { time = 10.520, lane = 2 },
        new NoteEvent { time = 11.145, lane = 3 },
        new NoteEvent { time = 11.835, lane = 4 },
        new NoteEvent { time = 12.520, lane = 5 },
        new NoteEvent { time = 13.084, lane = 1 },
        new NoteEvent { time = 13.775, lane = 2 },
        new NoteEvent { time = 14.410, lane = 3 },
        new NoteEvent { time = 15.049, lane = 4 },
        new NoteEvent { time = 15.700, lane = 5 },
        new NoteEvent { time = 16.425, lane = 0 },
        new NoteEvent { time = 16.767, lane = 1 },
        new NoteEvent { time = 17.108, lane = 2 },
        new NoteEvent { time = 17.467, lane = 3 },
        new NoteEvent { time = 17.783, lane = 4 },
        new NoteEvent { time = 18.923, lane = 2 },
        new NoteEvent { time = 19.572, lane = 3 },
        new NoteEvent { time = 21.067, lane = 2 },
        new NoteEvent { time = 21.375, lane = 3 },
        new NoteEvent { time = 21.682, lane = 4 },
        new NoteEvent { time = 22.519, lane = 0 },
    };

    readonly List<NoteEvent> notes = new();
    readonly List<NoteViewClick> active = new();

    int spawnIndex;
    int missCount;
    bool isPlaying;
    double songDspStart;

    void Awake()
    {
        BuildNotes();
    }

    void Start()
    {
        BeginGame();
    }

    void BeginGame()
    {
        spawnIndex = 0;
        missCount = 0;
        isPlaying = true;

        for (int i = active.Count - 1; i >= 0; i--)
            if (active[i] != null) Destroy(active[i].gameObject);
        active.Clear();

        if (music == null)
        {
            Debug.LogError("[RhythmConductorClick] music is NULL.");
            isPlaying = false;
            return;
        }

        if (laneAnchors == null || laneAnchors.Length != 6)
        {
            Debug.LogError("[RhythmConductorClick] laneAnchors must have 6 elements (0..5).");
            isPlaying = false;
            return;
        }

        music.Stop();
        music.time = 0f;

        songDspStart = AudioSettings.dspTime + 0.05;
        music.PlayScheduled(songDspStart);
    }

    void Update()
    {
        if (!isPlaying) return;

        double now = AudioSettings.dspTime;

        while (spawnIndex < notes.Count)
        {
            var e = notes[spawnIndex];

            double noteTime = songDspStart + (e.time + globalOffsetSeconds);
            double spawnTime = noteTime - preSpawnTime;

            if (now > noteTime + hitWindow)
            {
                spawnIndex++;
                continue;
            }

            if (now >= spawnTime)
            {
                SpawnOne(e, noteTime, spawnTime);
                spawnIndex++;
            }
            else break;
        }


        active.RemoveAll(n => n == null || n.IsJudged);
    }

    void SpawnOne(NoteEvent e, double noteTime, double spawnTime)
    {
        int lane = Mathf.Clamp(e.lane, 0, 5);
        var anchor = laneAnchors[lane];
        if (anchor == null) return;

        var n = Instantiate(notePrefab, noteParent);
        n.Init(noteTime, spawnTime, preSpawnTime, hitWindow);

        // ✅ 完全重合：用世界坐标对齐
        ((RectTransform)n.transform).position = anchor.position;

        n.OnMiss = HandleMiss;
        n.OnClicked = HandleClick;
        active.Add(n);
    }

    void HandleClick(NoteViewClick n, double nowDsp)
    {
        if (n == null || n.IsJudged) return;

        double delta = System.Math.Abs(nowDsp - n.NoteTime);
        if (delta <= hitWindow)
        {
            n.RegisterHit(nowDsp);
        }
        else
        {
            if (wrongClickCountsAsMiss) n.ForceExpireAsMiss();
        }
    }

    void HandleMiss(NoteViewClick _)
    {
        missCount++;
        Debug.Log($"MISS {missCount}/{maxMiss}");
        if (missCount >= maxMiss) GameOver();
    }

    void GameOver()
    {
        isPlaying = false;
        if (music != null) music.Stop();

        Debug.Log("GAME OVER");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void BuildNotes()
    {
        notes.Clear();
        notes.AddRange(CHART);
        notes.Sort((a, b) => a.time.CompareTo(b.time));
    }
}
