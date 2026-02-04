using System.Collections.Generic;
using UnityEngine;

public class RhythmConductor : MonoBehaviour
{
    [Header("Audio")]
    public AudioSource music;

    [Header("Chart")]
    public Chord[] chords;

    [Header("Refs")]
    public RectTransform noteParent;
    public NoteView notePrefab;
    public ChordLayout layout;

    [Header("Timing")]
    public double preSpawnTime = 1.2;

    [Header("Windows (seconds)")]
    public double hitWindow = 0.18;
    public double perfectWindow = 0.05;
    public double goodWindow = 0.10;

    [Header("Same-beat grouping")]
    public double chordTolerance = 0.02;

    double songDspStart;
    int spawnIndex = 0;

    readonly List<NoteView> activeNotes = new();

    void Start()
    {
        // 用 dspTime 预约播放，确保所有机器对齐稳定
        songDspStart = AudioSettings.dspTime + 0.1;
        music.PlayScheduled(songDspStart);
        spawnIndex = 0;
    }

    void Update()
    {
        double now = AudioSettings.dspTime;

        // 1) 生成 chord
        while (spawnIndex < chords.Length)
        {
            double chordTime = songDspStart + chords[spawnIndex].time;
            double spawnTime = chordTime - preSpawnTime;

            if (now >= spawnTime)
            {
                SpawnChord(spawnIndex, chordTime, spawnTime);
                spawnIndex++;
            }
            else break;
        }

        // 2) Space 判定
        if (Input.GetKeyDown(KeyCode.Space))
        {
            JudgeOnSpace(now);
        }

        // 3) 清理无效引用
        activeNotes.RemoveAll(n => n == null || n.IsJudged);
    }

    void SpawnChord(int chordIndex, double chordTime, double spawnTime)
    {
        int count = Mathf.Clamp(chords[chordIndex].count, 1, 6);
        float neighborDt = GetNeighborDt(chordIndex);

        layout.Compute(count, neighborDt, out Vector2[] pos, out float targetScale);

        for (int i = 0; i < count; i++)
        {
            var note = Instantiate(notePrefab, noteParent);
            note.Init(chordTime, spawnTime, preSpawnTime, hitWindow);


            note.SetAnchoredPosition(pos[i]);
            activeNotes.Add(note);
        }
    }

    float GetNeighborDt(int i)
    {
        float prev = float.MaxValue;
        float next = float.MaxValue;

        if (i > 0) prev = (float)(chords[i].time - chords[i - 1].time);
        if (i < chords.Length - 1) next = (float)(chords[i + 1].time - chords[i].time);

        float dt = Mathf.Min(prev, next);
        if (dt == float.MaxValue) dt = 999f; // 只有一个 chord 的情况
        return dt;
    }

    void JudgeOnSpace(double now)
    {
        activeNotes.RemoveAll(n => n == null || n.IsJudged);

        // 找出 hitWindow 内候选，选最近的那一拍
        double bestAbs = double.MaxValue;
        double bestTime = 0;

        for (int i = 0; i < activeNotes.Count; i++)
        {
            var n = activeNotes[i];
            double delta = System.Math.Abs(now - n.NoteTime);
            if (delta <= hitWindow && delta < bestAbs)
            {
                bestAbs = delta;
                bestTime = n.NoteTime;
            }
        }

        if (bestAbs == double.MaxValue)
        {
            // 空按：你可以选择扣分/断连击，先不做
            return;
        }

        // 同一拍（同一 chord 里多个点）一起判定
        for (int i = 0; i < activeNotes.Count; i++)
        {
            var n = activeNotes[i];
            if (!n.IsJudged && System.Math.Abs(n.NoteTime - bestTime) <= chordTolerance)
            {
                n.Judge(now);
            }
        }
    }
}
