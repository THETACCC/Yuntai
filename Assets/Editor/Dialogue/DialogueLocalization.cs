using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DialogueSystem
{
    /// <summary>
    /// 本地化数据管理器，从Google Sheets加载和缓存数据
    /// </summary>
    public static class DialogueLocalization
    {
        // Google Sheets CSV公开链接
        private static string googleSheetsCsvUrl = "";

        // 缓存数据：ID -> (语言 -> 文本)
        private static Dictionary<string, Dictionary<Language, string>> localizationCache =
            new Dictionary<string, Dictionary<Language, string>>();

        // 是否已加载
        private static bool isLoaded = false;

        public static bool IsLoaded => isLoaded;

        /// <summary>
        /// 设置Google Sheets CSV URL
        /// </summary>
        public static void SetCsvUrl(string url)
        {
            googleSheetsCsvUrl = url;
            PlayerPrefs.SetString("DialogueLocalization_CsvUrl", url);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 获取当前设置的URL
        /// </summary>
        public static string GetCsvUrl()
        {
            if (string.IsNullOrEmpty(googleSheetsCsvUrl))
            {
                googleSheetsCsvUrl = PlayerPrefs.GetString("DialogueLocalization_CsvUrl", "");
            }
            return googleSheetsCsvUrl;
        }

        /// <summary>
        /// 从Google Sheets加载数据
        /// </summary>
        public static IEnumerator LoadFromGoogleSheets(Action<bool, string> onComplete)
        {
            string url = GetCsvUrl();

            if (string.IsNullOrEmpty(url))
            {
                onComplete?.Invoke(false, "未设置Google Sheets CSV URL");
                yield break;
            }

            Debug.Log($"[DialogueLocalization] 开始加载: {url}");

            UnityWebRequest request = UnityWebRequest.Get(url);
            request.timeout = 30; // 设置30秒超时

            yield return request.SendWebRequest();

            Debug.Log($"[DialogueLocalization] 请求完成，状态码: {request.responseCode}");
            Debug.Log($"[DialogueLocalization] Result: {request.result}");

            // 检查各种错误情况
            if (request.result == UnityWebRequest.Result.ConnectionError)
            {
                string error = $"连接错误: {request.error}\n请检查网络连接";
                Debug.LogError($"[DialogueLocalization] {error}");
                onComplete?.Invoke(false, error);
                yield break;
            }

            if (request.result == UnityWebRequest.Result.ProtocolError)
            {
                string error = $"协议错误 (状态码: {request.responseCode}): {request.error}\n" +
                              $"可能的原因:\n" +
                              $"1. URL格式不正确\n" +
                              $"2. Google Sheets未正确发布\n" +
                              $"3. 网络被拦截";
                Debug.LogError($"[DialogueLocalization] {error}");
                onComplete?.Invoke(false, error);
                yield break;
            }

            if (request.result == UnityWebRequest.Result.DataProcessingError)
            {
                string error = $"数据处理错误: {request.error}";
                Debug.LogError($"[DialogueLocalization] {error}");
                onComplete?.Invoke(false, error);
                yield break;
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                string error = $"未知错误: {request.error}\nResult: {request.result}";
                Debug.LogError($"[DialogueLocalization] {error}");
                onComplete?.Invoke(false, error);
                yield break;
            }

            // 成功获取数据
            string csvContent = request.downloadHandler.text;

            if (string.IsNullOrEmpty(csvContent))
            {
                string error = "下载的CSV内容为空";
                Debug.LogError($"[DialogueLocalization] {error}");
                onComplete?.Invoke(false, error);
                yield break;
            }

            Debug.Log($"[DialogueLocalization] 成功下载，内容长度: {csvContent.Length} 字符");

            // 解析CSV
            bool success = ParseCsvData(csvContent, out string parseError);

            if (success)
            {
                isLoaded = true;
                Debug.Log($"[DialogueLocalization] 加载成功，共 {localizationCache.Count} 条数据");
                onComplete?.Invoke(true, $"成功加载 {localizationCache.Count} 条本地化数据");
            }
            else
            {
                onComplete?.Invoke(false, parseError);
            }
        }

        /// <summary>
        /// 解析CSV数据 - 格式: ID, 中文, English, 日语
        /// </summary>
        private static bool ParseCsvData(string csvContent, out string error)
        {
            error = "";
            localizationCache.Clear();

            if (string.IsNullOrEmpty(csvContent))
            {
                error = "CSV内容为空";
                return false;
            }

            try
            {
                Debug.Log($"[DialogueLocalization] CSV原始内容前200字符:\n{csvContent.Substring(0, Mathf.Min(200, csvContent.Length))}");

                string[] lines = csvContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                Debug.Log($"[DialogueLocalization] CSV总行数: {lines.Length}");

                if (lines.Length < 2)
                {
                    error = "CSV文件格式错误：至少需要标题行和一行数据";
                    return false;
                }

                // 显示标题行
                Debug.Log($"[DialogueLocalization] 标题行: {lines[0]}");

                // 跳过标题行
                int successCount = 0;
                for (int i = 1; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (string.IsNullOrEmpty(line)) continue;

                    // 使用正确的CSV解析（处理引号内的逗号）
                    string[] columns = ParseCsvLine(line);

                    if (columns.Length < 4)
                    {
                        Debug.LogWarning($"[DialogueLocalization] 第{i + 1}行格式错误，列数={columns.Length}: {line}");
                        continue;
                    }

                    string id = columns[0].Trim();
                    string chinese = columns[1].Trim();
                    string english = columns[2].Trim();
                    string japanese = columns[3].Trim();

                    if (string.IsNullOrEmpty(id))
                    {
                        Debug.LogWarning($"[DialogueLocalization] 第{i + 1}行ID为空，跳过");
                        continue;
                    }

                    var languageDict = new Dictionary<Language, string>
                    {
                        { Language.ChineseSimplified, UnescapeCsvField(chinese) },
                        { Language.English, UnescapeCsvField(english) },
                        { Language.Japanese, UnescapeCsvField(japanese) }
                    };

                    localizationCache[id] = languageDict;
                    successCount++;

                    // 显示前3条数据用于调试
                    if (successCount <= 3)
                    {
                        Debug.Log($"[DialogueLocalization] 解析第{successCount}条: ID={id}, 中文={chinese}, English={english}, 日语={japanese}");
                    }
                }

                Debug.Log($"[DialogueLocalization] 成功解析 {successCount} 条数据");
                return true;
            }
            catch (Exception ex)
            {
                error = $"解析CSV时出错: {ex.Message}";
                Debug.LogError($"[DialogueLocalization] {error}");
                Debug.LogError($"[DialogueLocalization] 堆栈: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// 正确解析CSV行，处理引号内的逗号
        /// </summary>
        private static string[] ParseCsvLine(string line)
        {
            List<string> fields = new List<string>();
            bool inQuotes = false;
            string currentField = "";

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    // 检查是否是转义的引号（两个连续的引号）
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        currentField += '"';
                        i++; // 跳过下一个引号
                    }
                    else
                    {
                        // 切换引号状态
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    // 字段分隔符（不在引号内）
                    fields.Add(currentField);
                    currentField = "";
                }
                else
                {
                    currentField += c;
                }
            }

            // 添加最后一个字段
            fields.Add(currentField);

            return fields.ToArray();
        }

        private static string UnescapeCsvField(string field)
        {
            if (string.IsNullOrEmpty(field)) return "";

            field = field.Trim();

            // 移除首尾的引号（只有当字段长度>=2且首尾都是引号时）
            if (field.Length >= 2 && field.StartsWith("\"") && field.EndsWith("\""))
            {
                field = field.Substring(1, field.Length - 2);
            }

            // 处理转义的引号
            field = field.Replace("\"\"", "\"");
            return field;
        }

        /// <summary>
        /// 根据ID和语言获取文本
        /// </summary>
        public static string GetText(string id, Language language)
        {
            if (string.IsNullOrEmpty(id)) return "";

            if (!isLoaded)
            {
                return $"[未加载:{id}]";
            }

            if (localizationCache.TryGetValue(id, out var languageDict))
            {
                if (languageDict.TryGetValue(language, out var text))
                {
                    return text;
                }
            }

            return null; // null表示ID不存在
        }

        /// <summary>
        /// 检查ID是否存在
        /// </summary>
        public static bool HasId(string id)
        {
            return !string.IsNullOrEmpty(id) && localizationCache.ContainsKey(id);
        }

        /// <summary>
        /// 获取ID对应的所有语言文本（用于保存到JSON）
        /// </summary>
        public static Dictionary<Language, string> GetAllLanguages(string id)
        {
            if (string.IsNullOrEmpty(id) || !localizationCache.TryGetValue(id, out var languageDict))
            {
                return null;
            }

            return new Dictionary<Language, string>(languageDict);
        }

        /// <summary>
        /// 获取所有已加载的ID列表
        /// </summary>
        public static List<string> GetAllIds()
        {
            return new List<string>(localizationCache.Keys);
        }

        /// <summary>
        /// 清空缓存
        /// </summary>
        public static void Clear()
        {
            localizationCache.Clear();
            isLoaded = false;
            Debug.Log("[DialogueLocalization] 缓存已清空");
        }

#if UNITY_EDITOR
        /// <summary>
        /// 编辑器专用：同步加载
        /// </summary>
        public static void LoadInEditorSync()
        {
            EditorCoroutineRunner.StartCoroutine(LoadFromGoogleSheets((success, message) =>
            {
                if (success)
                {
                    Debug.Log($"[DialogueLocalization] {message}");
                }
                else
                {
                    Debug.LogError($"[DialogueLocalization] {message}");
                }
            }));
        }
#endif
    }

#if UNITY_EDITOR
    /// <summary>
    /// 编辑器协程运行器 - 支持UnityWebRequest等异步操作
    /// </summary>
    public static class EditorCoroutineRunner
    {
        public static void StartCoroutine(IEnumerator routine)
        {
            EditorApplication.CallbackFunction update = null;
            object current = null;
            
            update = () =>
            {
                try
                {
                    // 如果上次yield的是AsyncOperation（如UnityWebRequest），等待它完成
                    if (current is UnityEngine.AsyncOperation asyncOp)
                    {
                        if (!asyncOp.isDone)
                        {
                            return; // 继续等待
                        }
                    }
                    
                    // 继续执行协程
                    bool hasNext = routine.MoveNext();
                    
                    if (!hasNext)
                    {
                        // 协程结束
                        EditorApplication.update -= update;
                    }
                    else
                    {
                        // 保存当前yield的对象
                        current = routine.Current;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"EditorCoroutine异常: {e}");
                    EditorApplication.update -= update;
                }
            };
            
            EditorApplication.update += update;
        }
    }
#endif
}