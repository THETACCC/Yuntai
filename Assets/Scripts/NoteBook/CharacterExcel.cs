using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;

public class CharacterExcel : MonoBehaviour
{
    [Header("Data Type")]
    [Tooltip("读取的数据类型：Character 或 Event")]
    public string dataType = "Character";

    [Header("Tags for UI Objects")]
    public string nameTag = "NoteBook_CharacterName";
    public string descriptionTag = "NoteBook_CharacterDescription";

    [Header("CSV Settings")]
    [SerializeField] private char csvDelimiter = ',';
    [SerializeField] private char csvQuoteChar = '"';

    private struct DataRow
    {
        public string NameID;
        public string InfoID;
    }

    private void Start()
    {
        if (NoteBookManager.instance == null)
        {
            Debug.LogError("[CharacterExcel] NoteBookManager not found!");
            return;
        }

        if (NoteBookLocalization.instance == null)
        {
            Debug.LogError("[CharacterExcel] NoteBookLocalization not found!");
            return;
        }

        string noteBookDataText = NoteBookManager.instance.GetNoteBookDataText();
        if (string.IsNullOrEmpty(noteBookDataText))
        {
            Debug.LogWarning("[CharacterExcel] No NoteBookData loaded.");
            return;
        }

        List<DataRow> rows = ParseNoteBookData(noteBookDataText, dataType, csvDelimiter, csvQuoteChar);

        var nameTexts = CollectTaggedText(nameTag);
        var descTexts = CollectTaggedText(descriptionTag);

        int count = Mathf.Min(rows.Count, Mathf.Min(nameTexts.Count, descTexts.Count));
        for (int i = 0; i < count; i++)
        {
            nameTexts[i].text = NoteBookLocalization.instance.GetText(rows[i].NameID);
            descTexts[i].text = NoteBookLocalization.instance.GetText(rows[i].InfoID);
        }

        for (int i = count; i < nameTexts.Count; i++) nameTexts[i].text = string.Empty;
        for (int i = count; i < descTexts.Count; i++) descTexts[i].text = string.Empty;

        Debug.Log($"[CharacterExcel] Applied {count} {dataType} record(s).");
    }

    private static List<DataRow> ParseNoteBookData(string csv, string filterType, char delimiter, char quoteChar)
    {
        var rows = new List<DataRow>();
        if (string.IsNullOrEmpty(csv)) return rows;

        csv = csv.Replace("\r\n", "\n").Replace("\r", "\n");
        if (csv.Length > 0 && csv[0] == '\uFEFF') csv = csv.Substring(1);

        using var reader = new StringReader(csv);

        // Skip header
        reader.ReadLine();

        string line;
        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var fields = SplitCsvLine(line, delimiter, quoteChar);
            if (fields.Count < 4) continue;

            string type = fields[0];
            if (!type.Equals(filterType, StringComparison.OrdinalIgnoreCase)) continue;

            string nameID = fields[2];
            string infoID = fields[3];

            rows.Add(new DataRow { NameID = nameID, InfoID = infoID });
        }

        return rows;
    }

    private static List<TextMeshProUGUI> CollectTaggedText(string tag)
    {
        var allTmps = UnityEngine.Object.FindObjectsOfType<TextMeshProUGUI>(true);
        var list = new List<TextMeshProUGUI>();

        foreach (var tmp in allTmps)
        {
            if (!tmp.gameObject.scene.IsValid()) continue;
            if (tmp.CompareTag(tag)) list.Add(tmp);
        }

        list.Sort((a, b) => string.CompareOrdinal(GetHierarchyIndexPath(a.transform), GetHierarchyIndexPath(b.transform)));
        return list;
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