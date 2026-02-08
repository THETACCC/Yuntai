using System.Collections.Generic;
using UnityEngine;

public class RhythmConductor : MonoBehaviour
{
    [Header("Audio")]
    public AudioSource music;

    [Header("Refs (UI)")]
    public RectTransform noteParent;
    public NoteView notePrefab;

    [Header("Timing")]
    public double preSpawnTime = 1.2;
    public double hitWindow = 0.18;

    [Header("Fail")]
    public int maxMiss = 10;
    public bool emptyPressCountsAsMiss = true;

    [Header("Lane Layout (6 lanes in one row)")]
    public float y = 0f;
    public float laneGap = 160f; // 调大/调小控制 6 个点的间距

    [Header("Timing Calibration")]
    public double globalOffsetSeconds = 0.0; // 如果整体偏早/偏晚，用这个微调（秒）

    // ===== Chart in absolute seconds (from Audacity) =====
    // 这六个：6 个独立 note，但在同一排的 6 个位置
    static readonly (double t, int lane)[] CHART = new[]
    {
        (1.857, 0),
        (2.194, 1),
        (2.523, 2),
        (2.860, 3),
        (3.214, 4),
        (3.542, 5),
    };
    // ================================================

    List<NoteEvent> notes;
    int spawnIndex;
    int missCount;
    bool isPlaying;
    double songDspStart;

    readonly List<NoteView> active = new();

    void Awake()
    {
        notes = BuildNotes(CHART);
        Debug.Log($"[Chart] notes={notes.Count}, first={notes[0].time:F3}s, last={notes[^1].time:F3}s");
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

        // 清理
        for (int i = active.Count - 1; i >= 0; i--)
            if (active[i] != null) Destroy(active[i].gameObject);
        active.Clear();

        // 播放（防止全局静音/暂停）
        AudioListener.pause = false;
        AudioListener.volume = 1f;

        if (music != null)
        {
            music.mute = false;
            music.volume = 1f;
            music.outputAudioMixerGroup = null;
            music.spatialBlend = 0f;

            music.Stop();
            music.time = 0f;

            songDspStart = AudioSettings.dspTime + 0.05;
            music.PlayScheduled(songDspStart);

            Debug.Log($"[RhythmConductor] Scheduled music dsp={songDspStart:F3}, now={AudioSettings.dspTime:F3}, clip={(music.clip ? music.clip.name : "NULL")}");
        }
        else
        {
            Debug.LogError("[RhythmConductor] music AudioSource is NULL.");
            isPlaying = false;
        }
    }

    void Update()
    {
        if (!isPlaying) return;

        double now = AudioSettings.dspTime;

        // 生成 note
        while (spawnIndex < notes.Count)
        {
            double noteTime = songDspStart + (notes[spawnIndex].time + globalOffsetSeconds);
            double spawnTime = noteTime - preSpawnTime;

            if (now >= spawnTime)
            {
                SpawnOne(notes[spawnIndex], noteTime, spawnTime);
                spawnIndex++;
            }
            else break;
        }

        // 输入
        if (Input.GetKeyDown(KeyCode.Space))
            TryHit(now);

        active.RemoveAll(n => n == null || n.IsJudged);
    }

    void SpawnOne(NoteEvent e, double noteTime, double spawnTime)
    {
        var n = Instantiate(notePrefab, noteParent);
        n.Init(noteTime, spawnTime, preSpawnTime, hitWindow);

        // lane 0..5 -> x 坐标（6 个点一排）
        float center = 2.5f; // 6 lanes => index center at 2.5
        float x = (e.lane - center) * laneGap;
        n.SetAnchoredPosition(new Vector2(x, y));

        n.OnMiss = HandleMiss;
        active.Add(n);
    }

    void TryHit(double now)
    {
        // 找窗口内最近的 note（只命中一个）
        NoteView best = null;
        double bestAbs = double.MaxValue;

        for (int i = 0; i < active.Count; i++)
        {
            var n = active[i];
            if (n == null || n.IsJudged) continue;

            double d = System.Math.Abs(now - n.NoteTime);
            if (d <= hitWindow && d < bestAbs)
            {
                bestAbs = d;
                best = n;
            }
        }

        if (best == null)
        {
            if (emptyPressCountsAsMiss) HandleMiss(null);
            return;
        }

        best.JudgeHit();
    }

    void HandleMiss(NoteView _)
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
    }

    // -------- build --------
    List<NoteEvent> BuildNotes((double t, int lane)[] chart)
    {
        var list = new List<NoteEvent>(chart.Length);
        foreach (var item in chart)
        {
            list.Add(new NoteEvent
            {
                time = item.t,
                lane = Mathf.Clamp(item.lane, 0, 5)
            });
        }

        list.Sort((a, b) => a.time.CompareTo(b.time));
        return list;
    }
}
