using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[System.Serializable]
public class UILanguage
{
    public string languageCode;
    public string content;
    public TMP_FontAsset font;
}

public class UILocalization : MonoBehaviour
{
    private TextMeshProUGUI tmp;
    private string currentLanguage;

    [Tooltip("Default using settings' fonts. Set true to use different fonts")]
    public bool useDifferentFonts = false;

    public List<UILanguage> UILanguages;
    private Dictionary<string, TMP_FontAsset> fontDictionary;
    public Dictionary<string, string> contentDictionary;


    private void Awake()
    {
        tmp = GetComponent<TextMeshProUGUI>();
        InitializeDictionaries();
    }

    private void Start()
    {
        currentLanguage = Settings.instance?.currentLanguage;

        if (!useDifferentFonts)
        {
            if (Settings.instance != null)
            {
                foreach (var language in UILanguages)
                {
                    if (Settings.instance.fontDictionary.ContainsKey(language.languageCode))
                    {
                        language.font = Settings.instance.fontDictionary[language.languageCode];
                    }
                }
                // 重新初始化字典
                InitializeDictionaries();
            }
        }

        // 订阅事件
        if (Settings.instance != null)
        {
            Settings.instance.OnLanguageChanged += UpdateText;
        }

        // 初始化显示
        UpdateText(Settings.instance?.currentLanguage);
    }

    private void OnDestroy()
    {
        if (Settings.instance != null)
        {
            Settings.instance.OnLanguageChanged -= UpdateText;
        }
    }

    void InitializeDictionaries()
    {
        fontDictionary = new Dictionary<string, TMP_FontAsset>();
        contentDictionary = new Dictionary<string, string>();
        foreach (var pair in UILanguages)
        {
            if (!string.IsNullOrEmpty(pair.languageCode))
            {
                if (pair.font != null)
                {
                    fontDictionary[pair.languageCode] = pair.font;
                }
                if (pair.content != null)
                {
                    contentDictionary[pair.languageCode] = pair.content;
                }
            }
        }
    }

    public void UpdateText(string languageCode)
    {
        if (string.IsNullOrEmpty(languageCode) ||
            contentDictionary == null ||
            fontDictionary == null)
        {
            return;
        }

        if (tmp != null)
        {
            if (contentDictionary.ContainsKey(languageCode))
            {
                tmp.text = contentDictionary[languageCode]
                .Replace("\\n", "\n")
                .Replace("/n", "\n");
            }
            else
            {
                Debug.LogWarning($"GameObject: {gameObject.name}, Language: '{languageCode}' 's content is not set");
            }

            if (fontDictionary.ContainsKey(languageCode))
            {
                tmp.font = fontDictionary[languageCode];
            }
            else
            {
                Debug.LogWarning($"GameObject: {gameObject.name}, Language: '{languageCode}' 's font is not set");
            }
        }
        
    }

#if UNITY_EDITOR
    // 组件被添加或Reset时自动调用
    private void Reset()
    {
        // 在场景中查找Settings实例
        Settings settings = FindObjectOfType<Settings>();
        
        if (settings == null)
        {
            Debug.LogWarning("Settings not found in scene. Please add Settings to the scene first.");
            return;
        }

        if (settings.gameLanguages == null || settings.gameLanguages.Count == 0)
        {
            Debug.LogWarning("No languages configured in Settings!");
            return;
        }

        // 自动生成语言列表
        UILanguages = new List<UILanguage>();

        foreach (var gameLang in settings.gameLanguages)
        {
            if (!string.IsNullOrEmpty(gameLang.languageCode))
            {
                UILanguages.Add(new UILanguage
                {
                    languageCode = gameLang.languageCode,
                    content = "", // 留空让用户填写
                    font = useDifferentFonts ? null : gameLang.font
                });
            }
        }

        Debug.Log($"UILocalization: Auto-generated {UILanguages.Count} language entries from Settings!");
    }
#endif
}

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(UILanguage))]
public class UILanguageDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        
        // 获取useDifferentFonts的值
        var localization = property.serializedObject.targetObject as UILocalization;
        bool useDifferentFonts = localization != null && localization.useDifferentFonts;
        
        float lineHeight = EditorGUIUtility.singleLineHeight + 2;
        Rect rect = position;
        rect.height = EditorGUIUtility.singleLineHeight;
        
        // 获取languageCode来显示更友好的标签
        var languageCodeProp = property.FindPropertyRelative("languageCode");
        string displayLabel = string.IsNullOrEmpty(languageCodeProp.stringValue) 
            ? label.text 
            : $"{label.text} ({languageCodeProp.stringValue})";
        
        // 折叠标题
        property.isExpanded = EditorGUI.Foldout(rect, property.isExpanded, displayLabel, true);
        
        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;
            
            rect.y += lineHeight;
            EditorGUI.PropertyField(rect, property.FindPropertyRelative("languageCode"));
            
            rect.y += lineHeight;
            // content字段使用标准单行高度，保持和languageCode一致的宽度
            EditorGUI.PropertyField(rect, property.FindPropertyRelative("content"));
            
            // 只有useDifferentFonts为true时才显示font字段
            if (useDifferentFonts)
            {
                rect.y += lineHeight;
                EditorGUI.PropertyField(rect, property.FindPropertyRelative("font"));
            }
            
            EditorGUI.indentLevel--;
        }
        
        EditorGUI.EndProperty();
    }
    
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (!property.isExpanded)
            return EditorGUIUtility.singleLineHeight;
        
        var localization = property.serializedObject.targetObject as UILocalization;
        bool useDifferentFonts = localization != null && localization.useDifferentFonts;
        
        // languageCode (1行) + content (1行) + font (如果需要，1行)
        float lines = useDifferentFonts ? 4 : 3;
        return EditorGUIUtility.singleLineHeight * lines + 2 * (lines - 1);
    }
}
#endif