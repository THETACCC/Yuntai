using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DialogueSystem;

/// <summary>
/// 对话本地化转换工具 - 反向匹配：根据文本内容找到对应的ID
/// </summary>
public class DialogueLocalizationConverter : EditorWindow
{
    private Vector2 scrollPos;
    private string dialogueFolderPath = "Assets/Dialogues";
    private List<ConversionResult> conversionResults = new List<ConversionResult>();
    private bool showResults = false;
    private bool isProcessing = false;

    private class ConversionResult
    {
        public string fileName;
        public int nodesMatched;
        public int nodesFailed;
        public int choicesMatched;
        public int choicesFailed;
        public List<string> failedContents = new List<string>();
    }

    [MenuItem("Tools/Dialogue Localization Converter")]
    public static void ShowWindow()
    {
        var window = GetWindow<DialogueLocalizationConverter>();
        window.titleContent = new GUIContent("Localization Converter");
        window.minSize = new Vector2(700, 550);
        window.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(10);

        // 标题
        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel);
        titleStyle.fontSize = 14;
        EditorGUILayout.LabelField("对话本地化转换工具（反向匹配）", titleStyle);

        EditorGUILayout.Space(5);

        // 说明
        EditorGUILayout.HelpBox(
            "此工具会读取现有对话树（.dtree）的文本内容，在Google Sheets中查找匹配的ID，并自动填充到文件中。\n\n" +
            "⚠️ 使用前请确保：\n" +
            "1. 已在Manager窗口的Loc Settings中加载了Google Sheets数据\n" +
            "2. Google Sheets中包含所有需要的文本内容\n" +
            "3. 已备份原始对话树文件（强烈推荐！）",
            MessageType.Warning);

        EditorGUILayout.Space(10);

        // 显示本地化数据状态
        if (DialogueLocalization.IsLoaded)
        {
            int idCount = DialogueLocalization.GetAllIds().Count;
            EditorGUILayout.HelpBox($"✅ 本地化数据已加载：{idCount} 条记录", MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox("❌ 本地化数据未加载！请先在Manager窗口配置并加载Google Sheets", MessageType.Error);
        }

        EditorGUILayout.Space(10);

        // 文件夹路径
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("对话文件夹:", GUILayout.Width(100));
        dialogueFolderPath = EditorGUILayout.TextField(dialogueFolderPath);
        if (GUILayout.Button("浏览", GUILayout.Width(60)))
        {
            string path = EditorUtility.OpenFolderPanel("选择对话文件夹", "Assets", "");
            if (!string.IsNullOrEmpty(path))
            {
                dialogueFolderPath = "Assets" + path.Substring(Application.dataPath.Length);
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        // 显示将要处理的文件数量
        if (Directory.Exists(dialogueFolderPath))
        {
            string[] files = Directory.GetFiles(dialogueFolderPath, "*.dtree", SearchOption.AllDirectories);
            EditorGUILayout.LabelField($"找到 {files.Length} 个对话树文件（.dtree）", EditorStyles.miniLabel);
        }

        EditorGUILayout.Space(15);

        // 转换按钮
        GUI.enabled = DialogueLocalization.IsLoaded && !isProcessing && Directory.Exists(dialogueFolderPath);
        GUI.backgroundColor = new Color(0.3f, 0.8f, 0.3f);
        if (GUILayout.Button("开始反向匹配转换", GUILayout.Height(40)))
        {
            StartConversion();
        }
        GUI.backgroundColor = Color.white;
        GUI.enabled = true;

        if (isProcessing)
        {
            EditorGUILayout.LabelField("正在处理中，请稍候...", EditorStyles.centeredGreyMiniLabel);
        }

        EditorGUILayout.Space(10);

        // 显示结果
        if (showResults && conversionResults.Count > 0)
        {
            DrawResults();
        }
    }

    private void StartConversion()
    {
        if (!EditorUtility.DisplayDialog(
            "确认转换",
            "此操作会修改对话树文件（.dtree），将文本内容替换为ID引用。\n\n" +
            "⚠️ 强烈建议先备份文件！\n\n" +
            "是否继续？",
            "继续转换",
            "取消"))
        {
            return;
        }

        isProcessing = true;
        conversionResults.Clear();
        showResults = false;

        try
        {
            ConvertDialogues();
        }
        finally
        {
            isProcessing = false;
            Repaint();
        }
    }

    private void ConvertDialogues()
    {
        if (!Directory.Exists(dialogueFolderPath))
        {
            EditorUtility.DisplayDialog("错误", $"文件夹不存在: {dialogueFolderPath}", "确定");
            return;
        }

        // 搜索所有 .dtree 文件
        string[] files = Directory.GetFiles(dialogueFolderPath, "*.dtree", SearchOption.AllDirectories);

        if (files.Length == 0)
        {
            EditorUtility.DisplayDialog("错误", "未找到任何对话树文件（.dtree）", "确定");
            return;
        }

        int processedCount = 0;
        foreach (string filePath in files)
        {
            EditorUtility.DisplayProgressBar(
                "转换中...",
                $"处理文件 {processedCount + 1}/{files.Length}\n{Path.GetFileName(filePath)}",
                (float)processedCount / files.Length);

            var result = ConvertSingleFile(filePath);
            if (result != null)
            {
                conversionResults.Add(result);
            }
            processedCount++;
        }

        EditorUtility.ClearProgressBar();
        showResults = true;
        Repaint();

        // 显示总结
        int totalMatched = conversionResults.Sum(r => r.nodesMatched + r.choicesMatched);
        int totalFailed = conversionResults.Sum(r => r.nodesFailed + r.choicesFailed);

        string message = $"转换完成！\n\n" +
                        $"处理文件: {processedCount}\n" +
                        $"成功匹配: {totalMatched}\n" +
                        $"匹配失败: {totalFailed}";

        if (totalFailed > 0)
        {
            message += "\n\n⚠️ 部分文本未找到匹配，请检查详细结果";
        }

        EditorUtility.DisplayDialog("完成", message, "确定");
    }

    private ConversionResult ConvertSingleFile(string filePath)
    {
        try
        {
            string jsonContent = File.ReadAllText(filePath);
            DialogueTreeData data = JsonUtility.FromJson<DialogueTreeData>(jsonContent);

            if (data == null || data.nodes == null)
            {
                Debug.LogWarning($"无法解析文件: {filePath}");
                return null;
            }

            var result = new ConversionResult
            {
                fileName = Path.GetFileName(filePath)
            };

            bool hasChanges = false;

            // 转换每个节点
            foreach (var node in data.nodes)
            {
                // 转换对话内容
                if (node.content != null && node.content.HasAnyText())
                {
                    // 如果已经有ID，跳过
                    if (string.IsNullOrEmpty(node.contentId))
                    {
                        string matchedId = FindMatchingId(node.content);

                        if (!string.IsNullOrEmpty(matchedId))
                        {
                            node.contentId = matchedId;
                            result.nodesMatched++;
                            hasChanges = true;
                        }
                        else
                        {
                            result.nodesFailed++;
                            // 记录失败的文本（优先中文）
                            string preview = GetPreviewText(node.content);
                            if (!string.IsNullOrEmpty(preview))
                            {
                                result.failedContents.Add($"[对话] {preview}");
                            }
                        }
                    }
                }

                // 转换选择文本
                if (node.choices != null)
                {
                    foreach (var choice in node.choices)
                    {
                        if (choice.text != null && choice.text.HasAnyText())
                        {
                            // 如果已经有ID，跳过
                            if (string.IsNullOrEmpty(choice.textId))
                            {
                                string matchedId = FindMatchingId(choice.text);

                                if (!string.IsNullOrEmpty(matchedId))
                                {
                                    choice.textId = matchedId;
                                    result.choicesMatched++;
                                    hasChanges = true;
                                }
                                else
                                {
                                    result.choicesFailed++;
                                    string preview = GetPreviewText(choice.text);
                                    if (!string.IsNullOrEmpty(preview))
                                    {
                                        result.failedContents.Add($"[选择] {preview}");
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // 如果有修改，保存文件
            if (hasChanges)
            {
                string updatedJson = JsonUtility.ToJson(data, true);
                File.WriteAllText(filePath, updatedJson);
                AssetDatabase.Refresh();
            }

            return result;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"处理文件失败 {filePath}: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// 在本地化数据中查找匹配的ID
    /// </summary>
    private string FindMatchingId(LocalizedText text)
    {
        if (text == null || !text.HasAnyText())
        {
            return null;
        }

        // 遍历所有ID，尝试匹配
        foreach (var id in DialogueLocalization.GetAllIds())
        {
            var allLanguages = DialogueLocalization.GetAllLanguages(id);
            if (allLanguages == null) continue;

            // 尝试匹配中文
            if (!string.IsNullOrEmpty(text.zh) &&
                allLanguages.ContainsKey(Language.ChineseSimplified))
            {
                string sheetText = allLanguages[Language.ChineseSimplified];
                if (NormalizeText(text.zh) == NormalizeText(sheetText))
                {
                    return id;
                }
            }

            // 尝试匹配英文
            if (!string.IsNullOrEmpty(text.en) &&
                allLanguages.ContainsKey(Language.English))
            {
                string sheetText = allLanguages[Language.English];
                if (NormalizeText(text.en) == NormalizeText(sheetText))
                {
                    return id;
                }
            }

            // 尝试匹配日文
            if (!string.IsNullOrEmpty(text.ja) &&
                allLanguages.ContainsKey(Language.Japanese))
            {
                string sheetText = allLanguages[Language.Japanese];
                if (NormalizeText(text.ja) == NormalizeText(sheetText))
                {
                    return id;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// 标准化文本用于比较（去除首尾空格）
    /// </summary>
    private string NormalizeText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return "";
        return text.Trim();
    }

    /// <summary>
    /// 获取预览文本（优先中文）
    /// </summary>
    private string GetPreviewText(LocalizedText text)
    {
        if (!string.IsNullOrEmpty(text.zh))
            return text.zh.Length > 50 ? text.zh.Substring(0, 50) + "..." : text.zh;
        if (!string.IsNullOrEmpty(text.en))
            return text.en.Length > 50 ? text.en.Substring(0, 50) + "..." : text.en;
        if (!string.IsNullOrEmpty(text.ja))
            return text.ja.Length > 50 ? text.ja.Substring(0, 50) + "..." : text.ja;
        return "";
    }

    private void DrawResults()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("转换结果", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(250));

        foreach (var result in conversionResults)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // 文件名
            EditorGUILayout.LabelField(result.fileName, EditorStyles.boldLabel);

            // 统计信息
            EditorGUILayout.BeginHorizontal();
            if (result.nodesMatched > 0)
            {
                GUI.color = Color.green;
                EditorGUILayout.LabelField($"✓ 对话: {result.nodesMatched}", GUILayout.Width(120));
                GUI.color = Color.white;
            }
            if (result.nodesFailed > 0)
            {
                GUI.color = new Color(1f, 0.5f, 0f);
                EditorGUILayout.LabelField($"✗ 对话: {result.nodesFailed}", GUILayout.Width(120));
                GUI.color = Color.white;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (result.choicesMatched > 0)
            {
                GUI.color = Color.green;
                EditorGUILayout.LabelField($"✓ 选择: {result.choicesMatched}", GUILayout.Width(120));
                GUI.color = Color.white;
            }
            if (result.choicesFailed > 0)
            {
                GUI.color = new Color(1f, 0.5f, 0f);
                EditorGUILayout.LabelField($"✗ 选择: {result.choicesFailed}", GUILayout.Width(120));
                GUI.color = Color.white;
            }
            EditorGUILayout.EndHorizontal();

            // 显示失败的内容（最多显示3条）
            if (result.failedContents.Count > 0)
            {
                EditorGUILayout.Space(3);
                EditorGUILayout.LabelField("未匹配的内容:", EditorStyles.miniLabel);
                int displayCount = Mathf.Min(3, result.failedContents.Count);
                for (int i = 0; i < displayCount; i++)
                {
                    GUI.color = new Color(1f, 0.9f, 0.9f);
                    EditorGUILayout.LabelField($"  • {result.failedContents[i]}", EditorStyles.wordWrappedMiniLabel);
                    GUI.color = Color.white;
                }
                if (result.failedContents.Count > 3)
                {
                    EditorGUILayout.LabelField($"  ... 还有 {result.failedContents.Count - 3} 条", EditorStyles.miniLabel);
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }

        EditorGUILayout.EndScrollView();

        // 总计
        EditorGUILayout.Space(10);
        DrawSummary();

        // 导出未匹配内容按钮
        if (conversionResults.Any(r => r.failedContents.Count > 0))
        {
            EditorGUILayout.Space(10);
            if (GUILayout.Button("导出未匹配内容到CSV"))
            {
                ExportFailedContents();
            }
        }
    }

    private void DrawSummary()
    {
        int totalNodesMatched = conversionResults.Sum(r => r.nodesMatched);
        int totalNodesFailed = conversionResults.Sum(r => r.nodesFailed);
        int totalChoicesMatched = conversionResults.Sum(r => r.choicesMatched);
        int totalChoicesFailed = conversionResults.Sum(r => r.choicesFailed);
        int totalFiles = conversionResults.Count;

        string summary = $"总计:\n" +
                        $"• 处理文件: {totalFiles}\n" +
                        $"• 对话节点: ✓ {totalNodesMatched}  ✗ {totalNodesFailed}\n" +
                        $"• 选择文本: ✓ {totalChoicesMatched}  ✗ {totalChoicesFailed}";

        MessageType messageType = MessageType.Info;
        if (totalNodesFailed > 0 || totalChoicesFailed > 0)
        {
            summary += "\n\n⚠️ 部分文本未找到匹配，可能需要手动添加到Google Sheets";
            messageType = MessageType.Warning;
        }
        else
        {
            summary += "\n\n✅ 所有文本都成功匹配！";
        }

        EditorGUILayout.HelpBox(summary, messageType);
    }

    private void ExportFailedContents()
    {
        string savePath = EditorUtility.SaveFilePanel(
            "导出未匹配内容",
            "",
            "未匹配文本_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv",
            "csv");

        if (string.IsNullOrEmpty(savePath))
            return;

        try
        {
            StringBuilder csv = new StringBuilder();
            csv.AppendLine("文件名,类型,内容");

            foreach (var result in conversionResults)
            {
                foreach (var content in result.failedContents)
                {
                    // 提取类型和实际内容
                    string type = content.StartsWith("[对话]") ? "对话" : "选择";
                    string actualContent = content.Replace("[对话] ", "").Replace("[选择] ", "");

                    // CSV格式，处理引号和逗号
                    actualContent = "\"" + actualContent.Replace("\"", "\"\"") + "\"";
                    csv.AppendLine($"{result.fileName},{type},{actualContent}");
                }
            }

            File.WriteAllText(savePath, csv.ToString(), Encoding.UTF8);
            EditorUtility.DisplayDialog("成功", $"未匹配内容已导出到:\n{savePath}\n\n你可以将这些内容添加到Google Sheets中", "确定");

            // 打开文件所在文件夹
            EditorUtility.RevealInFinder(savePath);
        }
        catch (System.Exception e)
        {
            EditorUtility.DisplayDialog("错误", $"导出失败: {e.Message}", "确定");
        }
    }
}