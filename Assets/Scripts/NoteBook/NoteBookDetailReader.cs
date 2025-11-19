using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;

public class NoteBookDetailReader : MonoBehaviour
{
    [Header("Data Key")]
    [Tooltip("要显示的Key")]
    public string key = "";

    [Header("Hierarchy")]
    [Tooltip("Root that contains Info_0 ... Info_N (default: this.transform)")]
    public Transform infoRoot;

    [Tooltip("Prefix of each panel under the root (Info_0, Info_1, ...).")]
    public string infoPrefix = "Info_";

    [Header("CSV Settings")]
    [SerializeField] private char csvDelimiter = ',';
    [SerializeField] private char csvQuoteChar = '"';

    private struct DetailRow
    {
        public string TitleID;
        public string Body0ID;
        public string Body1ID;
    }

    private void Reset()
    {
        infoRoot = transform;
    }

    private void Start()
    {
        if (infoRoot == null) infoRoot = transform;

        if (NoteBookManager.instance == null)
        {
            Debug.LogError("[NoteBookDetailReader] NoteBookManager not found!");
            return;
        }

        if (NoteBookLocalization.instance == null)
        {
            Debug.LogError("[NoteBookDetailReader] NoteBookLocalization not found!");
            return;
        }

        if (string.IsNullOrEmpty(key))
        {
            Debug.LogWarning("[NoteBookDetailReader] No key specified!");
            return;
        }

        string noteBookDataText = NoteBookManager.instance.GetNoteBookDataText();
        if (string.IsNullOrEmpty(noteBookDataText))
        {
            Debug.LogWarning("[NoteBookDetailReader] No NoteBookData loaded.");
            return;
        }

        // 解析CSV并查找对应的key
        Dictionary<string, DetailRow> dataDict = ParseNoteBookData(noteBookDataText, csvDelimiter, csvQuoteChar);

        if (!dataDict.TryGetValue(key, out DetailRow detail))
        {
            Debug.LogWarning($"[NoteBookDetailReader] Key '{key}' not found in NoteBookData!");
            return;
        }

        var panels = CollectInfoPanels(infoRoot, infoPrefix);

        // 显示到第一个面板
        if (panels.Count > 0)
        {
            ApplyDetailToPanel(panels[0], detail);
        }

        // 清空其他面板
        for (int i = 1; i < panels.Count; i++)
        {
            ApplyDetailToPanel(panels[i], new DetailRow { TitleID = "", Body0ID = "", Body1ID = "" });
        }

        Debug.Log($"[NoteBookDetailReader] Applied detail for key '{key}'.");
    }

    private void ApplyDetailToPanel(Transform panel, DetailRow detail)
    {
        var title = FindText(panel, "Title");
        var body0 = FindText(panel, "BodyText_0");
        var body1 = FindText(panel, "BodyText_1");

        if (title) title.text = NoteBookLocalization.instance.GetText(detail.TitleID);
        if (body0) body0.text = NoteBookLocalization.instance.GetText(detail.Body0ID);
        if (body1) body1.text = NoteBookLocalization.instance.GetText(detail.Body1ID);
    }

    private static TextMeshProUGUI FindText(Transform parent, string childName)
    {
        var tmps = parent.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var t in tmps)
        {
            if (t.gameObject.name == childName)
                return t;
        }
        return null;
    }

    private static List<Transform> CollectInfoPanels(Transform root, string prefix)
    {
        var result = new List<(int idx, Transform tf)>();
        var allChildren = root.GetComponentsInChildren<Transform>(true);

        foreach (var t in allChildren)
        {
            if (t.parent != root) continue;
            if (!t.name.StartsWith(prefix, StringComparison.Ordinal)) continue;

            if (int.TryParse(t.name.Substring(prefix.Length), out int num))
            {
                result.Add((num, t));
            }
        }

        result.Sort((a, b) => a.idx.CompareTo(b.idx));

        var panels = new List<Transform>(result.Count);
        foreach (var item in result) panels.Add(item.tf);
        return panels;
    }

    private static Dictionary<string, DetailRow> ParseNoteBookData(string csv, char delimiter, char quoteChar)
    {
        var dict = new Dictionary<string, DetailRow>();
        if (string.IsNullOrEmpty(csv)) return dict;

        csv = csv.Replace("\r\n", "\n").Replace("\r", "\n");
        if (csv.Length > 0 && csv[0] == '\uFEFF') csv = csv.Substring(1);

        using var reader = new StringReader(csv);
        reader.ReadLine(); // Skip header

        string line;
        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var fields = SplitCsvLine(line, delimiter, quoteChar);
            if (fields.Count < 6) continue;

            string key = fields[0];
            string titleID = fields[3];
            string body0ID = fields[4];
            string body1ID = fields[5];

            dict[key] = new DetailRow { TitleID = titleID, Body0ID = body0ID, Body1ID = body1ID };
        }

        return dict;
    }

    private static List<string> SplitCsvLine(string line, char delimiter, char quoteChar)
    {
        var result = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (inQuotes)
            {
                if (c == quoteChar)
                {
                    bool escaped = (i + 1 < line.Length && line[i + 1] == quoteChar);
                    if (escaped) { sb.Append(quoteChar); i++; }
                    else { inQuotes = false; }
                }
                else
                {
                    sb.Append(c);
                }
            }
            else
            {
                if (c == delimiter)
                {
                    result.Add(sb.ToString());
                    sb.Length = 0;
                }
                else if (c == quoteChar)
                {
                    inQuotes = true;
                }
                else
                {
                    sb.Append(c);
                }
            }
        }
        result.Add(sb.ToString());
        return result;
    }
}