using UnityEngine;

public class ChordLayout : MonoBehaviour
{
    [Header("Y position")]
    public float y = 0f;

    [Header("Spacing (loose -> dense)")]
    public float innerGapLoose = 140f;
    public float innerGapDense = 90f;
    public float groupGapLoose = 260f;
    public float groupGapDense = 180f;

    [Header("Density thresholds (seconds)")]
    public float denseDt = 0.12f;
    public float looseDt = 0.45f;

    [Header("Scale multiplier (single note bigger)")]
    public float singleMultiplier = 1.12f;
    public float normalMultiplier = 1.0f;

    public void Compute(int count, float neighborDt, out Vector2[] positions, out float targetScaleMultiplier)
    {
        positions = new Vector2[count];
        targetScaleMultiplier = (count == 1) ? singleMultiplier : normalMultiplier;

        float t = Mathf.InverseLerp(denseDt, looseDt, neighborDt);
        float innerGap = Mathf.Lerp(innerGapDense, innerGapLoose, t);
        float groupGap = Mathf.Lerp(groupGapDense, groupGapLoose, t);

        if (count <= 3)
        {
            FillCluster(positions, 0, count, 0f, innerGap);
            return;
        }

        int leftSize = count / 2;          // 4->2 5->2 6->3
        int rightSize = count - leftSize;  // 4->2 5->3 6->3

        float leftWidth = (leftSize - 1) * innerGap;   // 左组占用宽度
        float rightWidth = (rightSize - 1) * innerGap; // 右组占用宽度

        // 整体包围盒宽度 = 左组宽 + 组间距 + 右组宽
        float totalWidth = leftWidth + groupGap + rightWidth;

        // 让整体包围盒中心对齐 0（先居中）
        // 左组中心 = 包围盒左边缘 + leftWidth/2
        float leftCenterX = -totalWidth * 0.5f + leftWidth * 0.5f;
        // 右组中心 = 左组中心 + (leftWidth/2 + groupGap + rightWidth/2)
        float rightCenterX = leftCenterX + leftWidth * 0.5f + groupGap + rightWidth * 0.5f;

        FillCluster(positions, 0, leftSize, leftCenterX, innerGap);
        FillCluster(positions, leftSize, rightSize, rightCenterX, innerGap);

    }

    void FillCluster(Vector2[] positions, int startIndex, int size, float centerX, float innerGap)
    {
        float half = (size - 1) * 0.5f;
        for (int i = 0; i < size; i++)
        {
            float x = centerX + (i - half) * innerGap;
            positions[startIndex + i] = new Vector2(x, y);
        }
    }
}
