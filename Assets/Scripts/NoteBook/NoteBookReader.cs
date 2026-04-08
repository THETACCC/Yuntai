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

    // 标志位，表示是否已经成功加载
    private bool hasLoaded = false;

    // 记录上次加载的语言
    private string lastLoadedLanguage = "";

    private struct DataRow
    {
        public string NameID;
        public string InfoID;
        public string TitleID;
        public string Body0ID;
        public string Body1ID;
    }

    private void OnEnable()
    {
        // 订阅语言切换事件
        NoteBookLocalization.OnLanguageChanged += OnLanguageChanged;

        // 订阅Settings语言切换事件（用于字体更新）
        if (Settings.instance != null)
        {
            Settings.instance.OnLanguageChanged += OnSettingsLanguageChanged;
        }

        // GameObject激活时检查是否需要加载或重新加载
        CheckAndLoad();
    }

    private void OnDisable()
    {
        // 取消订阅语言切换事件
        NoteBookLocalization.OnLanguageChanged -= OnLanguageChanged;

        if (Settings.instance != null)
        {
            Settings.instance.OnLanguageChanged -= OnSettingsLanguageChanged;
        }
    }

    /// <summary>
    /// Settings语言切换时的回调 - 更新字体
    /// </summary>
    private void OnSettingsLanguageChanged(string languageCode)
    {
        Debug.Log($"[NoteBookReader] Settings language changed to '{languageCode}' for key '{key}', updating fonts...");
        UpdateAllFonts(languageCode);
    }

    /// <summary>
    /// 语言切换时的回调 - 立即重新加载
    /// </summary>
    private void OnLanguageChanged()
    {
        Debug.Log($"[NoteBookReader] Language changed event received for key '{key}' on {gameObject.name}, reloading immediately...");
        hasLoaded = false;

        // 立即重新加载（不等待Update）
        CheckAndLoad();
    }

    private void Update()
    {
        // 持续检查并加载（CheckAndLoad内部会判断是否真的需要加载）
        CheckAndLoad();
    }

    /// <summary>
    /// 检查条件并加载数据
    /// </summary>
    private void CheckAndLoad()
    {
        // 检查所有条件是否满足
        if (NoteBookManager.instance == null || NoteBookLocalization.instance == null)
            return;

        if (!NoteBookManager.instance.IsDataReady || !NoteBookLocalization.instance.IsDataReady)
            return;

        // 检查是否需要加载：未加载过 或 语言已变化
        string currentLanguage = NoteBookLocalization.instance.currentLanguage;
        bool needLoad = !hasLoaded || (lastLoadedLanguage != currentLanguage);

        if (!needLoad)
            return;

        // 条件满足，开始加载
        TryLoadData();
    }

    private void TryLoadData()
    {
        if (string.IsNullOrEmpty(key))
        {
            Debug.LogWarning($"[NoteBookReader] No key specified on {gameObject.name}!");
            hasLoaded = true; // 标记为已尝试，避免重复警告
            return;
        }

        string noteBookDataText = NoteBookManager.instance.GetNoteBookDataText();
        if (string.IsNullOrEmpty(noteBookDataText))
        {
            Debug.LogWarning($"[NoteBookReader] No NoteBookData loaded on {gameObject.name}!");
            hasLoaded = true;
            return;
        }

        // 解析CSV
        Dictionary<string, DataRow> dataDict = ParseNoteBookData(noteBookDataText, csvDelimiter, csvQuoteChar);

        if (!dataDict.TryGetValue(key, out DataRow data))
        {
            Debug.LogError($"[NoteBookReader] Key '{key}' not found in NoteBookData! GameObject: {gameObject.name}");
            hasLoaded = true;
            return;
        }

        // 填充数据
        if (nameText != null)
            nameText.text = NoteBookLocalization.instance.GetText(data.NameID);

        if (descriptionText != null)
            descriptionText.text = NoteBookLocalization.instance.GetText(data.Body0ID);

        if (titleText != null)
            titleText.text = NoteBookLocalization.instance.GetText(data.TitleID);

        if (bodyText0 != null)
            bodyText0.text = NoteBookLocalization.instance.GetText(data.Body0ID);

        if (bodyText1 != null)
            bodyText1.text = NoteBookLocalization.instance.GetText(data.Body1ID);

        // 更新所有文本的字体
        if (Settings.instance != null)
        {
            UpdateAllFonts(Settings.instance.currentLanguage);
        }

        // 标记为已成功加载，并记录当前语言
        hasLoaded = true;
        lastLoadedLanguage = NoteBookLocalization.instance.currentLanguage;

        Debug.Log($"[NoteBookReader] Successfully loaded data for key '{key}' in language '{lastLoadedLanguage}' on {gameObject.name}");
    }

    /// <summary>
    /// 更新所有TextMeshProUGUI组件的字体
    /// </summary>
    private void UpdateAllFonts(string languageCode)
    {
        if (Settings.instance == null || Settings.instance.fontDictionary == null)
        {
            Debug.LogWarning($"[NoteBookReader] Settings or font dictionary not available!");
            return;
        }

        if (!Settings.instance.fontDictionary.ContainsKey(languageCode))
        {
            Debug.LogWarning($"[NoteBookReader] Font for language '{languageCode}' not found!");
            return;
        }

        TMP_FontAsset font = Settings.instance.fontDictionary[languageCode];

        // 更新所有TextMeshProUGUI的字体
        if (nameText != null) nameText.font = font;
        if (descriptionText != null) descriptionText.font = font;
        if (titleText != null) titleText.font = font;
        if (bodyText0 != null) bodyText0.font = font;
        if (bodyText1 != null) bodyText1.font = font;

        Debug.Log($"[NoteBookReader] Updated fonts to '{languageCode}' for key '{key}'");
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