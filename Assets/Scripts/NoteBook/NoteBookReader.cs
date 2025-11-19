using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;

public class NoteBookReader : MonoBehaviour
{
    [Header("Data Key")]
    [Tooltip("要显示的Key（如：Dream, Kiki等）")]
    public string key = "";

    [Header("UI Components")]
    [Tooltip("显示Name的TextMeshProUGUI")]
    public TextMeshProUGUI nameText;

    [Tooltip("显示Description的TextMeshProUGUI")]
    public TextMeshProUGUI descriptionText;

    [Tooltip("显示Title的TextMeshProUGUI")]
    public TextMeshProUGUI titleText;

    [Tooltip("显示BodyText_0的TextMeshProUGUI")]
    public TextMeshProUGUI bodyText0;

    [Tooltip("显示BodyText_1的TextMeshProUGUI")]
    public TextMeshProUGUI bodyText1;

    [Header("CSV Settings")]
    [SerializeField] private char csvDelimiter = ',';
    [SerializeField] private char csvQuoteChar = '"';

    private struct DataRow
    {
        public string NameID;
        public string InfoID;
        public string TitleID;
        public string Body0ID;
        public string Body1ID;
    }

    private void Start()
    {
        if (NoteBookManager.instance == null)
        {
            Debug.LogError("[NoteBookReader] NoteBookManager not found!");
            return;
        }

        if (NoteBookLocalization.instance == null)
        {
            Debug.LogError("[NoteBookReader] NoteBookLocalization not found!");
            return;
        }

        if (string.IsNullOrEmpty(key))
        {
            Debug.LogWarning("[NoteBookReader] No key specified!");
            return;
        }

        string noteBookDataText = NoteBookManager.instance.GetNoteBookDataText();
        if (string.IsNullOrEmpty(noteBookDataText))
        {
            Debug.LogWarning("[NoteBookReader] No NoteBookData loaded.");
            return;
        }

        // 解析CSV
        Dictionary<string, DataRow> dataDict = ParseNoteBookData(noteBookDataText, csvDelimiter, csvQuoteChar);

        if (!dataDict.TryGetValue(key, out DataRow data))
        {
            Debug.LogWarning($"[NoteBookReader] Key '{key}' not found in NoteBookData!");
            return;
        }

        // 填充数据
        if (nameText != null)
            nameText.text = NoteBookLocalization.instance.GetText(data.NameID);

        if (descriptionText != null)
            descriptionText.text = NoteBookLocalization.instance.GetText(data.InfoID);

        if (titleText != null)
            titleText.text = NoteBookLocalization.instance.GetText(data.TitleID);

        if (bodyText0 != null)
            bodyText0.text = NoteBookLocalization.instance.GetText(data.Body0ID);

        if (bodyText1 != null)
            bodyText1.text = NoteBookLocalization.instance.GetText(data.Body1ID);

        Debug.Log($"[NoteBookReader] Applied data for key '{key}'.");
    }

    private static Dictionary<string, DataRow> ParseNoteBookData(string csv, char delimiter, char quoteChar)
    {
        var dict = new Dictionary<string, DataRow>();
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
            string nameID = fields[1];
            string infoID = fields[2];
            string titleID = fields[3];
            string body0ID = fields[4];
            string body1ID = fields[5];

            dict[key] = new DataRow
            {
                NameID = nameID,
                InfoID = infoID,
                TitleID = titleID,
                Body0ID = body0ID,
                Body1ID = body1ID
            };
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