using System;
using System.Collections.Generic;
using UnityEngine;

public class LoopTracker : MonoBehaviour
{
    public static LoopTracker I { get; private set; }

    [Min(1)] public int Loop = 1;

    // 简单的 bool 变量仓库
    private readonly Dictionary<string, bool> flags = new(StringComparer.Ordinal);

    // 事件：有需要就订阅，没需要就只用 OnEnable 的 ApplyNow
    public event Action<int> OnLoopChanged;
    public event Action<string, bool> OnFlagChanged;

    private void Awake()
    {
        if (I && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
    }

    public void IncrementLoop()
    {
        Loop++;
        OnLoopChanged?.Invoke(Loop);
        Debug.Log($"[LoopTracker] Loop -> {Loop}");
    }

    public void SetLoop(int setLoop)
    {
        Loop = setLoop;
        OnLoopChanged?.Invoke(Loop);
        Debug.Log($"[LoopTracker] Loop -> {Loop}");
    }

    public void DecrementLoop()
    {
        Loop--;
        OnLoopChanged?.Invoke(Loop);
        Debug.Log($"[LoopTracker] Loop -> {Loop}");
    }

    public void SetFlag(string key, bool value)
    {
        flags[key] = value;
        OnFlagChanged?.Invoke(key, value);
        Debug.Log($"[LoopTracker] Flag '{key}' -> {value}");
    }

    public bool GetFlag(string key)
        => flags.TryGetValue(key, out var v) && v;
}
