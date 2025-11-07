using UnityEngine;

public class PuzzleLanternManager : MonoBehaviour
{
    private const int N = 7;
    private const int REQUIRED_TRUE = 4;

    [Header("Assign 7 PuzzleLanterns in order (index 0..6)")]
    public PuzzleLantern[] lanterns = new PuzzleLantern[N];

    [Header("Correct Slots (exact match: only these are true)")]
    public bool[] correctSlots = new bool[N];  // e.g., tick 1,3,5,6

    [Header("Result")]
    public bool Solved = false;

    private void Awake()
    {
        ClampArraySizes();
        Recompute();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ClampArraySizes();
    }
#endif

    private void Update()
    {
        // Always recompute so you never miss a toggle
        Recompute();
    }

    public void ForceRecheck() => Recompute(); // still available if you want to call it

    private void Recompute()
    {
        Solved = ComputeStrictMatch();
    }

    private bool ComputeStrictMatch()
    {
        if (lanterns == null || correctSlots == null) return false;
        if (lanterns.Length != N || correctSlots.Length != N) return false;

        // must have exactly 4 trues in the answer
        int cnt = 0;
        for (int i = 0; i < N; i++) if (correctSlots[i]) cnt++;
        if (cnt != REQUIRED_TRUE) return false;

        // strict match per index
        for (int i = 0; i < N; i++)
        {
            bool desired = correctSlots[i];
            bool actual = (lanterns[i] != null) && lanterns[i].IsHanged;
            if (actual != desired) return false;
        }
        return true;
    }

    [ContextMenu("Debug Print State (desired vs actual)")]
    private void DebugPrintState()
    {
        for (int i = 0; i < N; i++)
        {
            bool desired = (i < correctSlots.Length) && correctSlots[i];
            bool actual = (i < lanterns.Length) && lanterns[i] && lanterns[i].IsHanged;
            Debug.Log($"[Lantern {i}] desired={desired}  actual={actual}  obj={(lanterns[i] ? lanterns[i].name : "null")}");
        }
        Debug.Log($"Solved = {Solved}");
    }

    private void ClampArraySizes()
    {
        if (lanterns == null || lanterns.Length != N)
        {
            var old = lanterns ?? new PuzzleLantern[0];
            var arr = new PuzzleLantern[N];
            for (int i = 0; i < Mathf.Min(old.Length, N); i++) arr[i] = old[i];
            lanterns = arr;
        }
        if (correctSlots == null || correctSlots.Length != N)
        {
            var old = correctSlots ?? new bool[0];
            var arr = new bool[N];
            for (int i = 0; i < Mathf.Min(old.Length, N); i++) arr[i] = old[i];
            correctSlots = arr;
        }
    }
}
