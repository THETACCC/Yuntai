using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;

public class ExcelReaderTitle : MonoBehaviour
{
    [Header("CSV Source (Title-only)")]
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

    [Header("Mapping Settings")]
    [Min(0)]
    [Tooltip("Zero-based CSV column index to read as Title (default 0).")]
    public int titleColumnIndex = 0;

    [Min(0)]
    [Tooltip("How many data rows to skip BEFORE mapping to Info_0 (in addition to any header). Set to 1 to start from the 2nd data row.")]
    public int rowStartOffset = 1;   // <-- "+1 row"

    private void Reset()
    {
        infoRoot = transform;
    }

    private void Start()
    {
        if (infoRoot == null) infoRoot = transform;

        if (Excel_Text == null)
        {
            Debug.LogWarning("[ExcelReaderINFO_TitlesOnly] No CSV TextAsset assigned.");
            return;
        }

        List<string> titles;
        try
        {
            titles = ParseCsvTitlesOnly(Excel_Text.text, hasHeaderRow, csvDelimiter, csvQuoteChar, titleColumnIndex);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ExcelReaderINFO_TitlesOnly] CSV parse error: {ex.Message}");
            return;
        }

        // Apply row offset (+1 row) safely
        int available = Mathf.Max(0, titles.Count - rowStartOffset);

        // Collect panels named Info_0, Info_1, ... (includes inactive)
        var panels = CollectInfoPanels(infoRoot, infoPrefix);

        int count = Mathf.Min(available, panels.Count);
        for (int i = 0; i < count; i++)
        {
            ApplyTitleToPanel(panels[i], titles[i + rowStartOffset]);
        }

        // Clear any leftover panels
        for (int i = count; i < panels.Count; i++)
        {
            ApplyTitleToPanel(panels[i], string.Empty);
        }

        Debug.Log($"[ExcelReaderINFO_TitlesOnly] Applied {count} title(s) starting at row offset {rowStartOffset}. Panels={panels.Count}, CSV rows (post-header)={titles.Count}.");
    }

    private static void ApplyTitleToPanel(Transform panel, string titleValue)
    {
        var title = FindText(panel, "Title");
        if (title != null)
            title.text = titleValue ?? string.Empty;
    }

    /// Finds TextMeshProUGUI by name anywhere under 'parent', including inactive.
    private static TextMeshProUGUI FindText(Transform parent, string childName)
    {
        var tmps = parent.GetComponentsInChildren<TextMeshProUGUI>(true); // includeInactive = true
        foreach (var t in tmps)
        {
            if (t.gameObject.name == childName)
                return t;
        }
        Debug.LogWarning($"[ExcelReaderINFO_TitlesOnly] '{childName}' not found under '{parent.name}'.");
        return null;
    }

    /// Collect direct children of 'root' whose names start with prefix and end with a number; sort by that number.
    private static List<Transform> CollectInfoPanels(Transform root, string prefix)
    {
        var result = new List<(int idx, Transform tf)>();
        var allChildren = root.GetComponentsInChildren<Transform>(true); // include inactive

        foreach (var t in allChildren)
        {
            if (t.parent != root) continue; // only direct children of root
            if (!t.name.StartsWith(prefix, StringComparison.Ordinal)) continue;

            if (int.TryParse(t.name.Substring(prefix.Length), out int num))
                result.Add((num, t));
        }

        result.Sort((a, b) => a.idx.CompareTo(b.idx));

        var panels = new List<Transform>(result.Count);
        foreach (var item in result) panels.Add(item.tf);
        return panels;
    }

    /// Parse CSV and return a list of Title strings from 'titleColumnIdx' only.
    /// Header handling is applied BEFORE rowStartOffset.
    private static List<string> ParseCsvTitlesOnly(string csv, bool hasHeader, char delimiter, char quoteChar, int titleColumnIdx)
    {
        var titles = new List<string>();
        if (string.IsNullOrEmpty(csv)) return titles;

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

            string title = (titleColumnIdx >= 0 && titleColumnIdx < fields.Count)
                ? fields[titleColumnIdx]
                : string.Empty;

            titles.Add(title);
        }

        return titles;
    }

    /// Split a single CSV line into fields, honoring quotes and escaped quotes ("").
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