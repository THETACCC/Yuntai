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
    public float laneGap = 160f;

    [Header("Timing Calibration")]
    public double globalOffsetSeconds = 0.0;

    [Header("Grouping (seconds)")]
    public double groupTolerance = 0.08;

    [Header("Manual offset (simple + safe)")]
    public float desiredShiftX = 420f;  // slot=-1 左移，slot=+1 右移（建议 160~280）
    public float maxAbsShiftX = 520f;   // 偏移最大绝对值：防止跑太远出屏（建议 220~320）

    // ===== Chart: (timeSeconds, lane 0..5, slot -1/0/+1) =====
    struct ChartRow
    {
        public double t;
        public int lane;
        public int slot;
        public ChartRow(double t, int lane, int slot) { this.t = t; this.lane = lane; this.slot = slot; }
    }

    static readonly ChartRow[] CHART = new[]
    {
    // 中
    new ChartRow(1.857, 0, 0),
    new ChartRow(2.194, 1, 0),
    new ChartRow(2.523, 2, 0),
    new ChartRow(2.860, 3, 0),
    new ChartRow(3.214, 4, 0),
    new ChartRow(3.542, 5, 0),

    // 左
    new ChartRow(4.405, 2, -1),
    new ChartRow(4.762, 3, -1),

    // 右
    new ChartRow(5.654, 3, +1),

    // 左
    new ChartRow(7.002, 2, -1),

    // 右
    new ChartRow(8.252, 3, +1),

    // 左
    new ChartRow(9.567, 2, -1),

    // 右（横向扫过去：2,3,4,5）
    new ChartRow(10.520, 2, +1),
    new ChartRow(11.145, 3, +1),
    new ChartRow(11.835, 4, +1),
    new ChartRow(12.520, 5, +1),

    // 中（横向扫过去：1,2,3,4,5）
    new ChartRow(13.084, 1, 0),
    new ChartRow(13.775, 2, 0),
    new ChartRow(14.410, 3, 0),
    new ChartRow(15.049, 4, 0),
    new ChartRow(15.700, 5, 0),

    // 中（横向扫过去：0,1,2,3,4）
    new ChartRow(16.425, 0, 0),
    new ChartRow(16.767, 1, 0),
    new ChartRow(17.108, 2, 0),
    new ChartRow(17.467, 3, 0),
    new ChartRow(17.783, 4, 0),

    // 左（2个）
    new ChartRow(18.923, 2, -1),
    new ChartRow(19.572, 3, -1),

    // 右（3个）
    new ChartRow(21.067, 2, +1),
    new ChartRow(21.375, 3, +1),
    new ChartRow(21.682, 4, +1),

    // 中（1个）
    new ChartRow(22.519, 0, 0),
    };

    // 复用项目里已有的 NoteEvent（不要在这里定义 NoteEvent）
    List<NoteEvent> notes = new();
    List<int> slots = new();

    int spawnIndex;
    int missCount;
    bool isPlaying;
    double songDspStart;

    readonly List<NoteView> active = new();

    void Awake()
    {
        BuildNotesAndSlots();
        if (notes.Count > 0)
            Debug.Log($"[Chart] notes={notes.Count}, first={notes[0].time:F3}s, last={notes[^1].time:F3}s");
    }

    void Start() => BeginGame();

    void BeginGame()
    {
        spawnIndex = 0;
        missCount = 0;
        isPlaying = true;

        for (int i = active.Count - 1; i >= 0; i--)
            if (active[i] != null) Destroy(active[i].gameObject);
        active.Clear();

        AudioListener.pause = false;
        AudioListener.volume = 1f;

        if (music == null) { Debug.LogError("[RhythmConductor] music AudioSource is NULL."); isPlaying = false; return; }

        music.mute = false;
        music.volume = 1f;
        music.spatialBlend = 0f;

        music.Stop();
        music.time = 0f;

        songDspStart = AudioSettings.dspTime + 0.05;
        music.PlayScheduled(songDspStart);

        Debug.Log($"[RhythmConductor] Scheduled music dsp={songDspStart:F3}, now={AudioSettings.dspTime:F3}, clip={(music.clip ? music.clip.name : "NULL")}");
    }

    void Update()
    {
        if (!isPlaying) return;

        double now = AudioSettings.dspTime;

        while (spawnIndex < notes.Count)
        {
            int start = spawnIndex;
            double t0 = notes[start].time;
            int slot0 = slots[start];

            int end = start + 1;
            while (end < notes.Count &&
                   (notes[end].time - t0) <= groupTolerance &&
                   slots[end] == slot0)
                end++;

            double noteTime0 = songDspStart + (notes[start].time + globalOffsetSeconds);
            double spawnTime0 = noteTime0 - preSpawnTime;

            if (now >= spawnTime0)
            {
                SpawnCluster(start, end, slot0);
                spawnIndex = end;
            }
            else break;
        }

        if (Input.GetKeyDown(KeyCode.Space))
            TryHit(now);

        active.RemoveAll(n => n == null || n.IsJudged);
    }

    void SpawnCluster(int start, int end, int slot)
    {
        float laneCenter = 2.5f;

        // 1) 先计算这组的 minX/maxX（基于 lane）
        float minX = float.MaxValue;
        float maxX = float.MinValue;

        for (int i = start; i < end; i++)
        {
            float baseX = (notes[i].lane - laneCenter) * laneGap;
            minX = Mathf.Min(minX, baseX);
            maxX = Mathf.Max(maxX, baseX);
        }

        int count = end - start;

        // 2) 只对“多点组”做 recenter；单点不 recenter（否则 lane 信息会被抹掉）
        float recenterOffset = 0f;
        if (count >= 2)
        {
            float clusterCenterX = (minX + maxX) * 0.5f;
            recenterOffset = -clusterCenterX;
        }

        // 3) 左右 slot 偏移（对称）
        float sideOffset = slot * desiredShiftX;
        sideOffset = Mathf.Clamp(sideOffset, -maxAbsShiftX, maxAbsShiftX);

        float finalOffsetX = recenterOffset + sideOffset;

        // 4) 生成
        for (int i = start; i < end; i++)
        {
            double noteTime = songDspStart + (notes[i].time + globalOffsetSeconds);
            double spawnTime = noteTime - preSpawnTime;

            var n = Instantiate(notePrefab, noteParent);
            n.Init(noteTime, spawnTime, preSpawnTime, hitWindow);

            float baseX = (notes[i].lane - laneCenter) * laneGap;
            float x = baseX + finalOffsetX;

            n.SetAnchoredPosition(new Vector2(x, y));
            n.OnMiss = HandleMiss;
            active.Add(n);
        }
    }

    void TryHit(double now)
    {
        NoteView next = null;
        double nextTime = double.MaxValue;

        for (int i = 0; i < active.Count; i++)
        {
            var n = active[i];
            if (n == null || n.IsJudged) continue;
            if (n.NoteTime < nextTime) { nextTime = n.NoteTime; next = n; }
        }

        if (next == null) return;

        double delta = System.Math.Abs(now - next.NoteTime);

        if (delta <= hitWindow) next.RegisterHit(now);
        else
        {
            if (!emptyPressCountsAsMiss) return;
            next.ForceMiss();
        }
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

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void BuildNotesAndSlots()
    {
        var tmpNotes = new List<NoteEvent>(CHART.Length);
        var tmpSlots = new List<int>(CHART.Length);

        for (int i = 0; i < CHART.Length; i++)
        {
            var row = CHART[i];
            tmpNotes.Add(new NoteEvent { time = row.t, lane = Mathf.Clamp(row.lane, 0, 5) });
            tmpSlots.Add(Mathf.Clamp(row.slot, -1, 1));
        }

        var idx = new List<int>(tmpNotes.Count);
        for (int i = 0; i < tmpNotes.Count; i++) idx.Add(i);
        idx.Sort((a, b) => tmpNotes[a].time.CompareTo(tmpNotes[b].time));

        notes.Clear();
        slots.Clear();
        for (int k = 0; k < idx.Count; k++)
        {
            int i = idx[k];
            notes.Add(tmpNotes[i]);
            slots.Add(tmpSlots[i]);
        }
    }
}
