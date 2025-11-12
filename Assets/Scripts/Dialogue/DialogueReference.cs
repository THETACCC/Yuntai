using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 为GameObject提供持久化的唯一ID，用于对话系统的引用
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class DialogueReference : MonoBehaviour
{
    [SerializeField] private string uniqueID;

    public string UniqueID
    {
        get
        {
            if (string.IsNullOrEmpty(uniqueID))
            {
                GenerateNewID();
            }
            return uniqueID;
        }
    }

    private void OnValidate()
    {
        // 确保有ID
        if (string.IsNullOrEmpty(uniqueID))
        {
            GenerateNewID();
        }
    }

    private void Awake()
    {
        // 运行时也确保有ID
        if (string.IsNullOrEmpty(uniqueID))
        {
            GenerateNewID();
        }
    }

    private void GenerateNewID()
    {
        uniqueID = System.Guid.NewGuid().ToString();
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(this);
        }
#endif
    }

    /// <summary>
    /// 查找所有场景中的DialogueReference（包括inactive对象）
    /// </summary>
    public static GameObject FindByID(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        var allRefs = Resources.FindObjectsOfTypeAll<DialogueReference>();
        var target = System.Array.Find(allRefs, x => x.UniqueID == id && x.gameObject.scene.IsValid());
        return target?.gameObject;
    }

    /// <summary>
    /// 获取或创建GameObject的DialogueReference组件
    /// </summary>
    public static DialogueReference GetOrCreate(GameObject obj)
    {
        if (obj == null) return null;

        var component = obj.GetComponent<DialogueReference>();
        if (component == null)
        {
            component = obj.AddComponent<DialogueReference>();
        }
        return component;
    }
}