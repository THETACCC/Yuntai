using System.Collections.Generic;
using UnityEngine;

public class ChordLayout : MonoBehaviour
{
    [Header("Y position")]
    public float y = 0f;

    [Header("Spacing (loose -> dense)")]
    public float innerGapLoose = 140f;     // 组内点间距（节拍不密时）
    public float innerGapDense = 90f;      // 组内点间距（节拍很密时）
    public float groupGapLoose = 260f;     // 两组中心间距（节拍不密时）
    public float groupGapDense = 180f;     // 两组中心间距（节拍很密时）

    [Header("Density thresholds (seconds)")]
    public float denseDt = 0.12f;          // <= 这个就算“很密”
    public float looseDt = 0.45f;          // >= 这个就算“很松”

    [Header("Note scales")]
    public float singleScale = 1.35f;      // 只有一个点时更大
    public float normalScale = 1.0f;

    /// <param name="count">本拍点的数量</param>
    /// <param name="neighborDt">与相邻拍的最小时间差（秒），越小越密</param>
    public void Compute(int count, float neighborDt, out Vector2[] positions, out float targetScale)
    {
        positions = new Vector2[count];
        targetScale = (count == 1) ? singleScale : normalScale;

        // 0..1：0=很密，1=很松
        float t = Mathf.InverseLerp(denseDt, looseDt, neighborDt);
        float innerGap = Mathf.Lerp(innerGapDense, innerGapLoose, t);
        float groupGap = Mathf.Lerp(groupGapDense, groupGapLoose, t);

        if (count <= 3)
        {
            // 单组，居中对称
            FillCluster(positions, startIndex: 0, size: count, centerX: 0f, innerGap: innerGap, y: y);
            return;
        }

        // 两组：左组 + 右组，每组最多3
        int leftSize = count / 2;          // 4->2, 5->2, 6->3
        int rightSize = count - leftSize;  // 4->2, 5->3, 6->3

        float leftCenterX = -groupGap * 0.5f;
        float rightCenterX = groupGap * 0.5f;

        FillCluster(positions, 0, leftSize, leftCenterX, innerGap, y);
        FillCluster(positions, leftSize, rightSize, rightCenterX, innerGap, y);
    }

    private void FillCluster(Vector2[] positions, int startIndex, int size, float centerX, float innerGap, float y)
    {
        // 让 cluster 内部对称，例如 size=2 -> [-0.5, +0.5] * innerGap
        float half = (size - 1) * 0.5f;

        for (int i = 0; i < size; i++)
        {
            float x = centerX + (i - half) * innerGap;
            positions[startIndex + i] = new Vector2(x, y);
        }
    }
}
