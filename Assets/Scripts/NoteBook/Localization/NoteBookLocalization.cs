using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public class NoteBookLocalization : MonoBehaviour
{
    public static NoteBookLocalization instance;

    // 语言切换事件
    public static event Action OnLanguageChanged;

    [Header("Localization Settings")]
    [Tooltip("本地化表CSV文件（如果使用本地文件）")]
    public TextAsset localizationTable;

    [Tooltip("Google Sheets发布的CSV URL（如果使用在线表格）")]
    public string googleSheetsURL = "";

    [Tooltip("当前语言（zh, en, ja等）")]
    public string currentLanguage = "zh";

    [Header("CSV Settings")]
    [SerializeField] private char csvDelimiter = ',';
    [SerializeField] private char csvQuoteChar = '"';

    // 存储 StringID -> 翻译文本 的字典
    private Dictionary<string, string> localizationDict = new Dictionary<string, string>();

    // 数据是否已加载完成
    public bool IsDataReady { get; private set; } = false;

    private void Awake()
    {
        // 单例模式
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        LoadLocalizationTable();
    }

    /// <summary>
    /// 加载本地化表
    /// </summary>
    public void LoadLocalizationTable()
    {
        // 开始加载时先标记为未就绪
        IsDataReady = false;

        // 优先使用Google Sheets URL
        if (!string.IsNullOrEmpty(googleSheetsURL))
        {
            StartCoroutine(LoadFromURL(googleSheetsURL));
        }
        // 否则使用本地文件
        else if (localizationTable != null)
        {
            LoadFromTextAsset(localizationTable.text);
        }
        else
        {
            Debug.LogError("[NoteBookLocalization] No localization source assigned!");
        }
    }

    private System.Collections.IEnumerator LoadFromURL(string url)
    {
        using (UnityEngine.Networking.UnityWebRequest www = UnityEngine.Networking.UnityWebRequest.Get(url))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[NoteBookLocalization] Failed to load from URL: {www.error}");
            }
            else
            {
                string csvText = www.downloadHandler.text;
                LoadFromTextAsset(csvText);
            }
        }
    }

    private void LoadFromTextAsset(string csvText)
    {
        try
        {
            localizationDict = ParseLocalizationCsv(csvText, currentLanguage, csvDelimiter, csvQuoteChar);
            IsDataReady = true;
            Debug.Log($"[NoteBookLocalization] Successfully loaded {localizationDict.Count} entries for language: {currentLanguage}");

            // 数据加载完成，触发语言切换事件通知所有Reader更新
            OnLanguageChanged?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[NoteBookLocalization] Failed to load localization table: {ex.Message}");
        }
    }

    /// <summary>
    /// 根据StringID获取当前语言的文本
    /// </summary>
    public string GetText(string stringID)
    {
        if (string.IsNullOrEmpty(stringID))
            return string.Empty;

        if (localizationDict.TryGetValue(stringID, out string text))
            return text;

        Debug.LogWarning($"[NoteBookLocalization] StringID '{stringID}' not found in localization table!");
        return $"[MISSING:{stringID}]";
    }

    /// <summary>
    /// 切换语言
    /// </summary>
    public void SetLanguage(string newLanguage)
    {
        if (currentLanguage == newLanguage)
            return;

        currentLanguage = newLanguage;
        LoadLocalizationTable();

        Debug.Log($"[NoteBookLocalization] Language changing to '{currentLanguage}'...");
    }

    /// <summary>
    /// 解析本地化CSV文件
    /// 第一列是StringID，其他列是各种语言
    /// 第一行是header（StringID, CN, EN, JP等）
    /// </summary>
    private static Dictionary<string, string> ParseLocalizationCsv(string csv, string language, char delimiter, char quoteChar)
    {
        var dict = new Dictionary<string, string>();
        if (string.IsNullOrEmpty(csv))
            return dict;

        // Normalize line endings & strip BOM
        csv = csv.Replace("\r\n", "\n").Replace("\r", "\n");
        if (csv.Length > 0 && csv[0] == '\uFEFF')
            csv = csv.Substring(1);

        using var reader = new StringReader(csv);

        // 读取header行，找到目标语言的列索引
        string headerLine = reader.ReadLine();
        if (string.IsNullOrEmpty(headerLine))
        {
            Debug.LogError("[NoteBookLocalization] Empty CSV file!");
            return dict;
        }

        var headers = SplitCsvLine(headerLine, delimiter, quoteChar);
        int languageColumnIndex = -1;

        for (int i = 0; i < headers.Count; i++)
        {
            if (headers[i].Trim().Equals(language, StringComparison.OrdinalIgnoreCase))
            {
                languageColumnIndex = i;
                break;
            }
        }

        if (languageColumnIndex == -1)
        {
            Debug.LogError($"[NoteBookLocalization] Language '{language}' not found in CSV header! Available: {string.Join(", ", headers)}");
            return dict;
        }

        // 读取数据行
        string line;
        int lineNumber = 1;
        while ((line = reader.ReadLine()) != null)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var fields = SplitCsvLine(line, delimiter, quoteChar);

            if (fields.Count <= languageColumnIndex)
            {
                Debug.LogWarning($"[NoteBookLocalization] Line {lineNumber}: Not enough columns");
                continue;
            }

            string stringID = fields[0].Trim();
            string text = fields[languageColumnIndex];

            if (!string.IsNullOrEmpty(stringID))
            {
                if (dict.ContainsKey(stringID))
                {
                    Debug.LogWarning($"[NoteBookLocalization] Duplicate StringID '{stringID}' found at line {lineNumber}!");
                }
                else
                {
                    dict[stringID] = text;
                }
            }
        }

        return dict;
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
                    bool escaped = (i + 1 < line.Length && line[i + 1] == quoteChar);
                    if (escaped)
                    {
                        sb.Append(quoteChar);
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
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