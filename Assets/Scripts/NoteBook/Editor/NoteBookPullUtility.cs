#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Notebook 编辑器拉取工具：从 Google Sheets URL 下载 CSV 并写入 Assets/External/。
/// </summary>
public static class NoteBookPullUtility
{
    public static void PullAndSave(NoteBookManager manager, NoteBookLocalization localization, Action<bool, string> onComplete)
    {
        if (manager == null)
        {
            onComplete?.Invoke(false, "NoteBookManager 为空。");
            return;
        }

        if (localization == null)
        {
            onComplete?.Invoke(false, "同 GameObject 上未找到 NoteBookLocalization 组件。");
            return;
        }

        string dataUrl = manager.googleSheetsURL?.Trim();
        string localizationUrl = localization.googleSheetsURL?.Trim();

        if (!IsValidUrl(dataUrl) || !IsValidUrl(localizationUrl))
        {
            onComplete?.Invoke(false, "请先填写两个 Google Sheets CSV URL（必须以 http:// 或 https:// 开头）。");
            return;
        }

        NoteBookEditorCoroutineRunner.StartCoroutine(PullCoroutine(manager, localization, dataUrl, localizationUrl, onComplete));
    }

    private static IEnumerator PullCoroutine(
        NoteBookManager manager,
        NoteBookLocalization localization,
        string dataUrl,
        string localizationUrl,
        Action<bool, string> onComplete)
    {
        // 直接在 PullCoroutine 里 yield WebRequest，避免嵌套 IEnumerator 导致下载未完成就写入
        string dataContent;
        {
            using var request = UnityWebRequest.Get(dataUrl);
            request.timeout = 30;
            yield return request.SendWebRequest();

            if (!TryGetContent(request, out dataContent, out string dataError))
            {
                onComplete?.Invoke(false, "NoteBookData 拉取失败：\n" + dataError);
                yield break;
            }
        }

        string localizationContent;
        {
            using var request = UnityWebRequest.Get(localizationUrl);
            request.timeout = 30;
            yield return request.SendWebRequest();

            if (!TryGetContent(request, out localizationContent, out string locError))
            {
                onComplete?.Invoke(false, "LocalizationTable 拉取失败：\n" + locError);
                yield break;
            }
        }

        try
        {
            SaveCsvToAsset(NoteBookLocalData.NoteBookDataAssetPath, dataContent);
            SaveCsvToAsset(NoteBookLocalData.LocalizationTableAssetPath, localizationContent);

            AssetDatabase.Refresh();

            manager.noteBookData = AssetDatabase.LoadAssetAtPath<TextAsset>(NoteBookLocalData.NoteBookDataAssetPath);
            localization.localizationTable = AssetDatabase.LoadAssetAtPath<TextAsset>(NoteBookLocalData.LocalizationTableAssetPath);

            EditorUtility.SetDirty(manager);
            EditorUtility.SetDirty(localization);

            onComplete?.Invoke(true,
                "已保存到本地：\n" +
                NoteBookLocalData.NoteBookDataAssetPath + "（" + dataContent.Length + " 字符）\n" +
                NoteBookLocalData.LocalizationTableAssetPath + "（" + localizationContent.Length + " 字符）");
        }
        catch (Exception ex)
        {
            onComplete?.Invoke(false, "保存失败：\n" + ex.Message);
        }
    }

    private static bool TryGetContent(UnityWebRequest request, out string content, out string error)
    {
        if (request.result == UnityWebRequest.Result.Success
            && !string.IsNullOrWhiteSpace(request.downloadHandler.text))
        {
            content = request.downloadHandler.text;
            error = null;
            return true;
        }

        if (request.result == UnityWebRequest.Result.ConnectionError)
            error = "连接错误：" + request.error + "\n请检查网络或 VPN。";
        else if (request.result == UnityWebRequest.Result.ProtocolError)
            error = "协议错误（HTTP " + request.responseCode + "）：" + request.error;
        else if (string.IsNullOrWhiteSpace(request.downloadHandler.text))
            error = "下载内容为空";
        else
            error = request.error ?? "未知错误";

        content = null;
        return false;
    }

    private static void SaveCsvToAsset(string assetPath, string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException("CSV 内容为空，拒绝写入：" + assetPath);

        EnsureDirectoryExists(assetPath);
        var utf8WithoutBom = new UTF8Encoding(false);
        File.WriteAllText(assetPath, content, utf8WithoutBom);
    }

    private static bool IsValidUrl(string url)
    {
        return !string.IsNullOrEmpty(url)
            && (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
    }

    private static void EnsureDirectoryExists(string assetPath)
    {
        string directory = Path.GetDirectoryName(assetPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);
    }
}

/// <summary>
/// 编辑器协程运行器（对齐 Dialogue EditorCoroutineRunner 逻辑）。
/// </summary>
public static class NoteBookEditorCoroutineRunner
{
    public static void StartCoroutine(IEnumerator routine)
    {
        EditorApplication.CallbackFunction update = null;
        object current = null;

        update = () =>
        {
            try
            {
                if (current is AsyncOperation asyncOp && !asyncOp.isDone)
                    return;

                if (!routine.MoveNext())
                {
                    EditorApplication.update -= update;
                    return;
                }

                current = routine.Current;
            }
            catch (Exception ex)
            {
                EditorApplication.update -= update;
                Debug.LogException(ex);
            }
        };

        EditorApplication.update += update;
    }
}
#endif
