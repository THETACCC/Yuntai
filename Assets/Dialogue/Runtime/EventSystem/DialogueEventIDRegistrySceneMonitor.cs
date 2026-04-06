using UnityEngine;
using System.Linq;


#if UNITY_EDITOR
using UnityEditor;

namespace DialogueSystem
{
    /// <summary>
    /// ID注册表场景监控器 - 监听场景文件删除/移动，自动维护注册表
    /// </summary>
    public class DialogueEventIDRegistrySceneMonitor : AssetModificationProcessor
    {
        /// <summary>
        /// 在资源删除前调用
        /// </summary>
        static AssetDeleteResult OnWillDeleteAsset(string assetPath, RemoveAssetOptions options)
        {
            // 检查是否是场景文件
            if (assetPath.EndsWith(".unity"))
            {
                // 场景文件被删除，清理该场景的所有ID
                DialogueEventIDRegistry.Instance.RemoveByScene(assetPath);

                Debug.Log($"[DialogueEventIDRegistry] 场景文件被删除: {assetPath}，已自动清理相关ID");
            }

            return AssetDeleteResult.DidNotDelete;
        }

        /// <summary>
        /// 在资源移动/重命名后调用
        /// </summary>
        static AssetMoveResult OnWillMoveAsset(string sourcePath, string destinationPath)
        {
            // 检查是否是场景文件
            if (sourcePath.EndsWith(".unity") && destinationPath.EndsWith(".unity"))
            {
                // 场景文件被移动或重命名
                // 需要更新注册表中该场景的路径

                var registry = DialogueEventIDRegistry.Instance;
                var records = registry.GetAllRecords();

                // 找出该场景的所有记录并更新路径
                var affectedRecords = records.Where(r => r.scenePath == sourcePath).ToList();

                if (affectedRecords.Count > 0)
                {
                    // 先删除旧路径的记录
                    registry.RemoveByScene(sourcePath);

                    // 用新路径重新添加
                    foreach (var record in affectedRecords)
                    {
                        registry.Add(record.id, destinationPath, record.objectName);
                    }

                    Debug.Log($"[DialogueEventIDRegistry] 场景文件移动: {sourcePath} → {destinationPath}，已更新 {affectedRecords.Count} 个ID记录");
                }
            }

            return AssetMoveResult.DidNotMove;
        }
    }
}
#endif