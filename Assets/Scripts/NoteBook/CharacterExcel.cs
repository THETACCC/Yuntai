using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;

public class CharacterExcel : MonoBehaviour
{
    [Header("CSV Source (expected columns: Name,Info)")]
    public TextAsset Character_Text;

    [Tooltip("If true, the first CSV row is a header and will be skipped.")]
    public bool hasHeaderRow = true;

    [Header("Tags for UI Objects")]
    public string nameTag = "NoteBook_CharacterName";
    public string descriptionTag = "NoteBook_CharacterDescription";

    [Header("CSV Settings")]
    [SerializeField] private char csvDelimiter = ',';   // You said the CSV is separated by commas
    [SerializeField] private char csvQuoteChar = '"';

    // Internal row structure
    private struct CharacterRow
    {
        public string Name;
        public string Info;
        public CharacterRow(string name, string info) { Name = name; Info = info; }
    }

    private void Start()
    {
        if (Character_Text == null)
        {
            Debug.LogWarning("[CharacterExcel] No CSV TextAsset assigned.");
            return;
        }

        List<CharacterRow> rows;
        try
        {
            rows = ParseCsv(Character_Text.text, hasHeaderRow, csvDelimiter, csvQuoteChar);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CharacterExcel] CSV parse error: {ex.Message}");
            return;
        }

        // Find all tagged objects and get their TextMeshProUGUI components in a deterministic order
        var nameTexts = CollectTaggedText(nameTag);
        var descTexts = CollectTaggedText(descriptionTag);

        int count = Mathf.Min(rows.Count, Mathf.Min(nameTexts.Count, descTexts.Count));
        for (int i = 0; i < count; i++)
        {
            nameTexts[i].text = rows[i].Name ?? string.Empty;
            descTexts[i].text = rows[i].Info ?? string.Empty;
        }

        // Clear any extras if there are more UI objects than CSV entries
        for (int i = count; i < nameTexts.Count; i++) nameTexts[i].text = string.Empty;
        for (int i = count; i < descTexts.Count; i++) descTexts[i].text = string.Empty;

        Debug.Log($"[CharacterExcel] Applied {count} record(s). Name fields={nameTexts.Count}, Description fields={descTexts.Count}, CSV rows={rows.Count}.");
    }

    /// <summary>
    /// Finds TextMeshProUGUI components whose GameObjects carry the given tag,
    /// including INACTIVE objects, and returns them in deterministic hierarchy order.
    /// </summary>
    private static List<TextMeshProUGUI> CollectTaggedText(string tag)
    {
        // OPTION A (Unity 2020.1+): includeInactive overload
        // Finds ALL loaded TextMeshProUGUI in the scene (active + inactive)
        var allTmps = UnityEngine.Object.FindObjectsOfType<TextMeshProUGUI>(true);

        // If you are on an older Unity version, comment the line above and use OPTION B below:
        // var allTmps = Resources.FindObjectsOfTypeAll<TextMeshProUGUI>();

        var list = new List<TextMeshProUGUI>(allTmps.Length);
        foreach (var tmp in allTmps)
        {
            // Exclude assets/prefabs not instantiated in the scene (important if using Resources.FindObjectsOfTypeAll)
            if (!tmp.gameObject.scene.IsValid()) continue;

            if (tmp.CompareTag(tag))
                list.Add(tmp);
        }

        // Deterministic order by hierarchy path (root->leaf sibling indices)
        list.Sort((a, b) => string.CompareOrdinal(GetHierarchyIndexPath(a.transform), GetHierarchyIndexPath(b.transform)));
        return list;
    }

    /// <summary>
    /// Stable sort key using sibling indices along the full transform path.
    /// </summary>
    private static int CompareByHierarchyPath(GameObject a, GameObject b)
    {
        string pa = GetHierarchyIndexPath(a.transform);
        string pb = GetHierarchyIndexPath(b.transform);
        return string.CompareOrdinal(pa, pb);
    }

    private static string GetHierarchyIndexPath(Transform t)
    {
        const int pad = 5;
        var stack = new Stack<int>();
        while (t != null)
        {
            stack.Push(t.GetSiblingIndex());
            t = t.parent;
        }
        var sb = new StringBuilder();
        bool first = true;
        foreach (var idx in stack)
        {
            if (!first) sb.Append('/');
            sb.Append(idx.ToString().PadLeft(pad, '0'));
            first = false;
        }
        return sb.ToString();
    }

    /// <summary>
    /// Parse CSV text into rows (Name, Info), supporting quoted fields and escaped quotes.
    /// </summary>
    private static List<CharacterRow> ParseCsv(string csv, bool hasHeader, char delimiter, char quoteChar)
    {
        var rows = new List<CharacterRow>();
        if (string.IsNullOrEmpty(csv)) return rows;

        // Normalize line endings and strip BOM
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
            // We only care about the first two columns
            string name = fields.Count >= 1 ? fields[0] : string.Empty;
            string info = fields.Count >= 2 ? fields[1] : string.Empty;

            rows.Add(new CharacterRow(name, info));
        }

        return rows;
    }

    /// <summary>
    /// Split a single CSV line into fields, honoring quotes and escaped quotes ("").
    /// </summary>
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
                    // Escaped quote?
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