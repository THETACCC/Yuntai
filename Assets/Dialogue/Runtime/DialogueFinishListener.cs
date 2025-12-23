using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Events;

public class DialogueFinishListener : MonoBehaviour
{
    [Header("Only listen to these JSON files")]
    public List<TextAsset> allowedJsonFiles = new();

    [Header("When to fire (pick either or both)")]
    public bool fireOnLastClick = false;     // fires at the exact click that ends dialogue
    public bool fireWhenFullyClosed = true;  // fires after UI fully fades out

    [Header("What to call")]
    public UnityEvent onLastClick;
    public UnityEvent onFullyClosed;

    [Header("Debug")]
    public bool verboseLog = false;

    // --- internal state ---
    private HashSet<string> allowedJsonFingerprints = new();
    private bool lastIsFinished = false;   // for rising-edge detection
    private bool sawFinishPulse = false;   // we saw the short true pulse
    private string pulseJsonFingerprint = null;

    // A lightweight shape that ignores mutable fields like currentIndex.
    [System.Serializable]
    private class DialogueDataLite { public List<Conversation> conversations; }

    private void Awake()
    {
        allowedJsonFingerprints.Clear();
        if (allowedJsonFiles == null) return;

        foreach (var ta in allowedJsonFiles)
        {
            if (ta == null) continue;
            var data = SafeParse(ta.text);
            if (data == null) continue;

            var fp = Fingerprint(data);
            if (!string.IsNullOrEmpty(fp))
            {
                allowedJsonFingerprints.Add(fp);
                if (verboseLog) Debug.Log($"[DFL_JsonOnly] Added allowed JSON fp: {fp} ({ta.name})");
            }
        }
    }

    private void Update()
    {
        var dm = DialogueController.instance;
        if (dm == null) return;

        // Rising edge for the short isDialogueFinished pulse
        bool nowFinished = dm.isDialogueFinished;
        if (!lastIsFinished && nowFinished)
        {
            var curFp = Fingerprint(dm.dialogueData);
            if (!string.IsNullOrEmpty(curFp) && allowedJsonFingerprints.Contains(curFp))
            {
                sawFinishPulse = true;
                pulseJsonFingerprint = curFp;
                if (verboseLog) Debug.Log("[DFL_JsonOnly] Finish pulse matched allowed JSON.");

                if (fireOnLastClick)
                    onLastClick?.Invoke();
            }
            else if (verboseLog)
            {
                Debug.Log("[DFL_JsonOnly] Finish pulse ignored (JSON not allowed).");
            }
        }
        lastIsFinished = nowFinished;

        // Fire when fully closed (UIGroup faded, isDialogueActive == false)
        if (fireWhenFullyClosed && sawFinishPulse && !dm.isDialogueActive)
        {
            // Optional re-check (usually unchanged)
            var endFp = Fingerprint(dm.dialogueData);
            if (string.IsNullOrEmpty(pulseJsonFingerprint) ||
                (!string.IsNullOrEmpty(endFp) && endFp == pulseJsonFingerprint) ||
                allowedJsonFingerprints.Contains(endFp))
            {
                onFullyClosed?.Invoke();
                if (verboseLog) Debug.Log("[DFL_JsonOnly] Fired onFullyClosed.");
            }

            // reset for next dialogue
            sawFinishPulse = false;
            pulseJsonFingerprint = null;
        }
    }

    // ---------- helpers ----------
    private DialogueData SafeParse(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try { return JsonUtility.FromJson<DialogueData>(json); }
        catch { return null; }
    }

    // Stable fingerprint: only hash conversations (ignore currentIndex)
    private string Fingerprint(DialogueData data)
    {
        if (data == null || data.conversations == null) return null;
        var lite = new DialogueDataLite { conversations = data.conversations };
        string s;
        try { s = JsonUtility.ToJson(lite, false); }
        catch { return null; }

        try
        {
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(s);
            var hash = sha.ComputeHash(bytes);
            var sb = new StringBuilder(hash.Length * 2);
            foreach (var b in hash) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
        catch
        {
            // very rare fallback
            int headLen = Mathf.Min(32, s.Length);
            int tailLen = Mathf.Min(32, Mathf.Max(0, s.Length - headLen));
            string head = s.Substring(0, headLen);
            string tail = s.Substring(s.Length - tailLen);
            return $"len:{s.Length}|{head}|{tail}";
        }
    }
}