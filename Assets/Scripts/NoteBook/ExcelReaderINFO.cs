using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;

public class ExcelReaderINFO : MonoBehaviour
{
    [Header("CSV Source (expected columns: Title,BodyText_0,BodyText_1)")]
    public TextAsset Excel_Text;

    [Tooltip("If true, the first CSV row is a header and will be skipped.")]
    public bool hasHeaderRow = true;

    [Header("Hierarchy")]
    [Tooltip("Root that contains Info_0 ... Info_N (default: this.transform)")]
    public Transform infoRoot;

    [Tooltip("Prefix of each panel under the root (Info_0, Info_1, ...).")]
    public string infoPrefix = "Info_";

    [Header("CSV Settings")]
    [SerializeField] private char csvDelimiter = ',';
    [SerializeField] private char csvQuoteChar = '"';

    private struct Row
    {
        public string Title;
        public string Body0;
        public string Body1;
        public Row(string t, string b0, string b1) { Title = t; Body0 = b0; Body1 = b1; }
    }

    private void Reset()
    {
        infoRoot = transform;
    }

    private void Start()
    {
        if (infoRoot == null) infoRoot = transform;

        if (Excel_Text == null)
        {
            Debug.LogWarning("[CharacterExcelToInfoPanels] No CSV TextAsset assigned.");
            return;
        }

        List<Row> rows;
        try
        {
            rows = ParseCsv(Excel_Text.text, hasHeaderRow, csvDelimiter, csvQuoteChar);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CharacterExcelToInfoPanels] CSV parse error: {ex.Message}");
            return;
        }

        // Collect panels named Info_0, Info_1, ... in numeric order (includes inactive)
        var panels = CollectInfoPanels(infoRoot, infoPrefix);

        int count = Mathf.Min(rows.Count, panels.Count);
        for (int i = 0; i < count; i++)
        {
            ApplyRowToPanel(panels[i], rows[i]);
        }

        // Clear remaining panels if there are more panels than rows
        for (int i = count; i < panels.Count; i++)
        {
            ApplyRowToPanel(panels[i], new Row(string.Empty, string.Empty, string.Empty));
        }

        Debug.Log($"[CharacterExcelToInfoPanels] Applied {count} row(s). Panels found={panels.Count}, CSV rows={rows.Count}.");
    }

    private static void ApplyRowToPanel(Transform panel, Row row)
    {
        // Find child TextMeshProUGUI under the panel by exact child name (includes inactive)
        var title = FindText(panel, "Title");
        var body0 = FindText(panel, "BodyText_0");
        var body1 = FindText(panel, "BodyText_1");

        if (title) title.text = row.Title ?? string.Empty;
        if (body0) body0.text = row.Body0 ?? string.Empty;
        if (body1) body1.text = row.Body1 ?? string.Empty;
    }

    /// Finds TextMeshProUGUI by direct child name (deep) under parent, including inactive.
    private static TextMeshProUGUI FindText(Transform parent, string childName)
    {
        // GetComponentsInChildren includes inactive when true is passed.
        var tmps = parent.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var t in tmps)
        {
            if (t.gameObject.name == childName)
                return t;
        }
        Debug.LogWarning($"[CharacterExcelToInfoPanels] '{childName}' not found under '{parent.name}'.");
        return null;
    }

    /// Collect children whose names start with infoPrefix and end with a number; sort by that number.
    private static List<Transform> CollectInfoPanels(Transform root, string prefix)
    {
        var result = new List<(int idx, Transform tf)>();

        // Include inactive children
        var allChildren = root.GetComponentsInChildren<Transform>(true);
        foreach (var t in allChildren)
        {
            if (t.parent != root) continue; // only direct children of root
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

    private static List<Row> ParseCsv(string csv, bool hasHeader, char delimiter, char quoteChar)
    {
        var rows = new List<Row>();
        if (string.IsNullOrEmpty(csv)) return rows;

        // Normalize line endings & strip BOM
        csv = csv.Replace("\r\n", "\n").Replace("\r", "\n");
        if (csv.Length > 0 && csv[0] == '\uFEFF') csv = csv.Substring(1);

        using var reader = new StringReader(csv);
        string line;
        bool headerSkipped = !hasHeader;

        while ((line = reader.ReadLine()) != null)
        {
            if (!headerSkipped) { headerSkipped = true; continue; }
            if (string.IsNullOrWhiteSpace(line)) continue;

            var fields = SplitCsvLine(line, delimiter, quoteChar);

            string title = fields.Count >= 1 ? fields[0] : string.Empty;
            string b0 = fields.Count >= 2 ? fields[1] : string.Empty;
            string b1 = fields.Count >= 3 ? fields[2] : string.Empty;

            rows.Add(new Row(title, b0, b1));
        }

        return rows;
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