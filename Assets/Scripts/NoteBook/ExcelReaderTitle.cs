using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;

public class ExcelReaderTitle : MonoBehaviour
{
    [Header("Data Type")]
    [Tooltip("读取的数据类型：Character 或 Event")]
    public string dataType = "Character";

    [Header("Data Index")]
    [Tooltip("读取第几条数据（0-based）")]
    public int dataIndex = 0;

    [Header("Hierarchy")]
    [Tooltip("Root that contains Info_0 ... Info_N (default: this.transform)")]
    public Transform infoRoot;

    [Tooltip("Prefix of each panel under the root (Info_0, Info_1, ...).")]
    public string infoPrefix = "Info_";

    [Header("Mapping Settings")]
    [Min(0)]
    [Tooltip("How many data rows to skip BEFORE mapping to Info_0")]
    public int rowStartOffset = 1;

    [Header("CSV Settings")]
    [SerializeField] private char csvDelimiter = ',';
    [SerializeField] private char csvQuoteChar = '"';

    private void Reset()
    {
        infoRoot = transform;
    }

    private void Start()
    {
        if (infoRoot == null) infoRoot = transform;

        if (NoteBookManager.instance == null)
        {
            Debug.LogError("[ExcelReaderTitle] NoteBookManager not found!");
            return;
        }

        if (NoteBookLocalization.instance == null)
        {
            Debug.LogError("[ExcelReaderTitle] NoteBookLocalization not found!");
            return;
        }

        string noteBookDataText = NoteBookManager.instance.GetNoteBookDataText();
        if (string.IsNullOrEmpty(noteBookDataText))
        {
            Debug.LogWarning("[ExcelReaderTitle] No NoteBookData loaded.");
            return;
        }

        List<string> titleIDs = ParseTitleData(noteBookDataText, dataType, csvDelimiter, csvQuoteChar);

        if (dataIndex < 0 || dataIndex >= titleIDs.Count)
        {
            Debug.LogWarning($"[ExcelReaderTitle] Data index {dataIndex} out of range (0-{titleIDs.Count - 1}).");
            return;
        }

        int available = Mathf.Max(0, titleIDs.Count - rowStartOffset);
        var panels = CollectInfoPanels(infoRoot, infoPrefix);

        int count = Mathf.Min(available, panels.Count);
        for (int i = 0; i < count; i++)
        {
            string titleID = titleIDs[i + rowStartOffset];
            string titleText = NoteBookLocalization.instance.GetText(titleID);
            ApplyTitleToPanel(panels[i], titleText);
        }

        for (int i = count; i < panels.Count; i++)
        {
            ApplyTitleToPanel(panels[i], string.Empty);
        }

        Debug.Log($"[ExcelReaderTitle] Applied {count} title(s) starting at row offset {rowStartOffset}.");
    }

    private static void ApplyTitleToPanel(Transform panel, string titleValue)
    {
        var title = FindText(panel, "Title");
        if (title != null)
            title.text = titleValue ?? string.Empty;
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
                result.Add((num, t));
        }

        result.Sort((a, b) => a.idx.CompareTo(b.idx));

        var panels = new List<Transform>(result.Count);
        foreach (var item in result) panels.Add(item.tf);
        return panels;
    }

    private static List<string> ParseTitleData(string csv, string filterType, char delimiter, char quoteChar)
    {
        var titleIDs = new List<string>();
        if (string.IsNullOrEmpty(csv)) return titleIDs;

        csv = csv.Replace("\r\n", "\n").Replace("\r", "\n");
        if (csv.Length > 0 && csv[0] == '\uFEFF') csv = csv.Substring(1);

        using var reader = new StringReader(csv);
        reader.ReadLine(); // Skip header

        string line;
        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var fields = SplitCsvLine(line, delimiter, quoteChar);
            if (fields.Count < 5) continue;

            string type = fields[0];
            if (!type.Equals(filterType, StringComparison.OrdinalIgnoreCase)) continue;

            string titleID = fields[4];
            titleIDs.Add(titleID);
        }

        return titleIDs;
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