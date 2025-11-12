using System;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;

namespace DialogueSystem
{
    // ==================== 本地化系统 ====================

    /// <summary>
    /// 支持的语言
    /// </summary>
    [Serializable]
    public enum Language
    {
        English,
        ChineseSimplified,
        Japanese
    }

    /// <summary>
    /// 本地化文本 - 存储一个文本的多语言版本
    /// </summary>
    [Serializable]
    public class LocalizedText
    {
        [SerializeField] public string en = "";  // English
        [SerializeField] public string zh = "";  // 中文
        [SerializeField] public string ja = "";  // 日本語

        public LocalizedText()
        {
        }

        public LocalizedText(string text)
        {
            // 默认设置为英语
            en = text;
        }

        /// <summary>
        /// 获取指定语言的文本（带Fallback）- 使用string参数
        /// </summary>
        public string GetText(string languageCode)
        {
            string text = GetTextDirect(languageCode);
            if (!string.IsNullOrEmpty(text))
                return text;

            // Fallback: 当前语言 → en → zh → ja
            if (languageCode != "en")
            {
                text = en;
                if (!string.IsNullOrEmpty(text)) return text;
            }

            if (languageCode != "zh")
            {
                text = zh;
                if (!string.IsNullOrEmpty(text)) return text;
            }

            if (languageCode != "ja")
            {
                text = ja;
                if (!string.IsNullOrEmpty(text)) return text;
            }

            return "";
        }

        /// <summary>
        /// 获取指定语言的文本（带Fallback）- 使用Language enum参数（向后兼容）
        /// </summary>
        public string GetText(Language language)
        {
            return GetText(LanguageToCode(language));
        }

        /// <summary>
        /// 直接获取指定语言的文本（不做Fallback）- 使用string参数
        /// </summary>
        public string GetTextDirect(string languageCode)
        {
            switch (languageCode.ToLower())
            {
                case "en":
                case "english":
                    return en;
                case "zh":
                case "chinese":
                case "chinesesimplified":
                    return zh;
                case "ja":
                case "japanese":
                    return ja;
                default:
                    return "";
            }
        }

        /// <summary>
        /// 直接获取指定语言的文本（不做Fallback）- 使用Language enum参数（向后兼容）
        /// </summary>
        public string GetTextDirect(Language language)
        {
            return GetTextDirect(LanguageToCode(language));
        }

        /// <summary>
        /// 设置指定语言的文本 - 使用string参数
        /// </summary>
        public void SetText(string languageCode, string text)
        {
            switch (languageCode.ToLower())
            {
                case "en":
                case "english":
                    en = text;
                    break;
                case "zh":
                case "chinese":
                case "chinesesimplified":
                    zh = text;
                    break;
                case "ja":
                case "japanese":
                    ja = text;
                    break;
            }
        }

        /// <summary>
        /// 设置指定语言的文本 - 使用Language enum参数（向后兼容）
        /// </summary>
        public void SetText(Language language, string text)
        {
            SetText(LanguageToCode(language), text);
        }

        /// <summary>
        /// 检查是否有任何语言的文本
        /// </summary>
        public bool HasAnyText()
        {
            return !string.IsNullOrEmpty(en) || !string.IsNullOrEmpty(zh) || !string.IsNullOrEmpty(ja);
        }

        /// <summary>
        /// 从旧的单一字符串创建LocalizedText（兼容性）
        /// </summary>
        public static LocalizedText FromString(string text)
        {
            return new LocalizedText { en = text };
        }

        /// <summary>
        /// 隐式转换：字符串 → LocalizedText
        /// </summary>
        public static implicit operator LocalizedText(string text)
        {
            return FromString(text);
        }

        /// <summary>
        /// Language enum转换为语言代码
        /// </summary>
        private static string LanguageToCode(Language language)
        {
            switch (language)
            {
                case Language.English:
                    return "en";
                case Language.ChineseSimplified:
                    return "zh";
                case Language.Japanese:
                    return "ja";
                default:
                    return "en";
            }
        }
    }

    /// <summary>
    /// 本地化工具类
    /// </summary>
    public static class LocalizationHelper
    {
        /// <summary>
        /// 获取语言显示名称
        /// </summary>
        public static string GetLanguageDisplayName(Language language)
        {
            switch (language)
            {
                case Language.English:
                    return "English";
                case Language.ChineseSimplified:
                    return "中文";
                case Language.Japanese:
                    return "日本語";
                default:
                    return language.ToString();
            }
        }

        /// <summary>
        /// 获取语言简称（用于CSV表头）
        /// </summary>
        public static string GetLanguageCode(Language language)
        {
            switch (language)
            {
                case Language.English:
                    return "English";
                case Language.ChineseSimplified:
                    return "中文";
                case Language.Japanese:
                    return "日本語";
                default:
                    return language.ToString();
            }
        }

        /// <summary>
        /// 获取所有语言
        /// </summary>
        public static Language[] GetAllLanguages()
        {
            return new[] { Language.English, Language.ChineseSimplified, Language.Japanese };
        }
    }

    // ==================== 比较和逻辑类型 ====================

    /// <summary>
    /// Comparison operators for conditions
    /// </summary>
    [Serializable]
    public enum ComparisonType
    {
        Equal,              // ==
        NotEqual,           // !=
        Greater,            // >
        Less,               // <
        GreaterOrEqual,     // >=
        LessOrEqual         // <=
    }

    /// <summary>
    /// Logic operators for multiple conditions
    /// </summary>
    [Serializable]
    public enum ConditionLogic
    {
        AND,
        OR
    }

    /// <summary>
    /// Choice condition for branching dialogue
    /// </summary>
    [Serializable]
    public class ChoiceCondition
    {
        public string targetObjectID;      // GameObject unique ID (推荐使用)
        public string targetObjectName;    // GameObject name (向后兼容)
        public string componentTypeName;   // Component type name
        public string variableName;        // Variable name
        public ComparisonType comparison;
        public string compareValue;
    }

    // ==================== 事件系统 ====================

    /// <summary>
    /// Parameter types supported by dialogue events
    /// </summary>
    [Serializable]
    public enum ParameterType
    {
        None,
        String,
        Int,
        Float,
        Bool
    }

    /// <summary>
    /// Data structure for dialogue event calls
    /// </summary>
    [Serializable]
    public class DialogueEventCall
    {
        public string targetObjectID = "";    // Target GameObject unique ID (推荐使用)
        public string targetObjectName = "";  // Target GameObject name (向后兼容)
        public string componentTypeName = ""; // Component type name
        public string methodName = "";        // Method name
        public string stringParameter = "";   // String parameter
        public int intParameter = 0;          // Integer parameter
        public float floatParameter = 0f;     // Float parameter
        public bool boolParameter = false;    // Boolean parameter
        public ParameterType parameterType = ParameterType.None; // Parameter type
        public bool triggerOnEnd = false;     // 是否在对话结束时触发
    }

    /// <summary>
    /// Helper class for serializing event call lists
    /// </summary>
    [Serializable]
    public class SerializableEventCallList
    {
        public List<DialogueEventCall> eventCalls = new List<DialogueEventCall>();
    }

    // ==================== 选项数据 ====================

    /// <summary>
    /// Choice data for dialogue branches
    /// </summary>
    [Serializable]
    public class ChoiceData
    {
        public LocalizedText text = new LocalizedText();  // 改为本地化文本
        public List<ChoiceCondition> conditions = new List<ChoiceCondition>();
        public ConditionLogic conditionLogic = ConditionLogic.AND;
    }

    /// <summary>
    /// Helper class for serializing choice data lists
    /// </summary>
    [Serializable]
    public class SerializableChoiceDataList
    {
        public List<ChoiceData> choicesData = new List<ChoiceData>();
    }

    // ==================== 条件分支数据 ====================

    /// <summary>
    /// 条件分支数据
    /// </summary>
    [Serializable]
    public class ConditionalBranchData
    {
        public int priority;
        public List<ChoiceCondition> conditions = new List<ChoiceCondition>();
        public ConditionLogic conditionLogic = ConditionLogic.AND;
    }

    // ==================== 角色数据结构 ====================

    /// <summary>
    /// 角色数据 - 用于在编辑器中管理角色信息
    /// </summary>
    [Serializable]
    public class CharacterData
    {
        public string id;                           // 唯一ID
        public string character;                    // Manager中显示的分类名称
        public LocalizedText characterName = new LocalizedText();  // 角色名称（改为本地化）
        public string avatarAssetPath;              // Avatar 资源路径
        public bool isPlayer = false;               // 是否为玩家角色（用于 separatePlayerAndNPC）

        public CharacterData()
        {
            id = Guid.NewGuid().ToString();
            character = "New Character";
            characterName = new LocalizedText("New Character");
            avatarAssetPath = "";
            isPlayer = false;
        }

        public CharacterData(string name, string avatarPath)
        {
            id = Guid.NewGuid().ToString();
            character = name;
            characterName = new LocalizedText(name);
            avatarAssetPath = avatarPath;
            isPlayer = false;
        }
    }

    /// <summary>
    /// 角色库数据 - 存储所有角色
    /// </summary>
    [Serializable]
    public class CharacterLibraryData
    {
        public CharacterData[] characters = new CharacterData[0];
    }

    // ==================== 编辑器数据结构 ====================

    /// <summary>
    /// 对话树数据结构 - 用于序列化整个对话树（编辑器格式）
    /// </summary>
    [Serializable]
    public class DialogueTreeData
    {
        public List<DialogueNodeData> nodes = new List<DialogueNodeData>();
        public List<DialogueConnectionData> connections = new List<DialogueConnectionData>();
    }

    /// <summary>
    /// 对话节点数据 - 编辑器格式
    /// 使用 characterId 引用角色，而不是直接存储 name 和 avatarAssetPath
    /// </summary>
    [Serializable]
    public class DialogueNodeData
    {
        public string id;
        public int index;
        public string characterId = "";      // 角色ID引用
        public LocalizedText content = new LocalizedText();  // 改为本地化文本
        public float positionX;
        public float positionY;
        public List<ChoiceData> choices = new List<ChoiceData>();
        public List<DialogueEventCall> eventCalls = new List<DialogueEventCall>();
        public List<ConditionalBranchData> conditionalBranches = new List<ConditionalBranchData>();
    }

    /// <summary>
    /// 节点连接数据
    /// </summary>
    [Serializable]
    public class DialogueConnectionData
    {
        public string outputNodeId;
        public string inputNodeId;
        public int choiceIndex;
        public string choiceText;  // 保持字符串（仅用于编辑器显示参考）
        public int branchPriority;
    }

    // ==================== 运行时数据结构 ====================

    /// <summary>
    /// 运行时对话数据 - 用于游戏运行时
    /// 导出时从 characterId 解析出 name 和 avatarAddr，并保存所有语言的文本
    /// </summary>
    [Serializable]
    public class RuntimeDialogueData
    {
        public int index;
        public LocalizedText name = new LocalizedText();        // 角色名称（多语言）
        public string avatarAddr;                                // avatar 路径
        public bool isPlayer = false;                            // 是否为玩家角色
        public LocalizedText content = new LocalizedText();     // 对话内容（多语言）
        public List<RuntimeChoice> choices = new List<RuntimeChoice>();
        public string nextNodeId;
        public List<DialogueEventCall> eventCalls = new List<DialogueEventCall>();
        public List<RuntimeConditionalBranch> conditionalBranches = new List<RuntimeConditionalBranch>();
    }

    /// <summary>
    /// 运行时选项数据
    /// </summary>
    [Serializable]
    public class RuntimeChoice
    {
        public LocalizedText text = new LocalizedText();        // 选项文本（多语言）
        public string nextNodeId;
        public List<ChoiceCondition> conditions = new List<ChoiceCondition>();
        public ConditionLogic conditionLogic = ConditionLogic.AND;
    }

    /// <summary>
    /// 运行时条件分支数据
    /// </summary>
    [Serializable]
    public class RuntimeConditionalBranch
    {
        public int targetIndex;
        public int priority;
        public List<ChoiceCondition> conditions = new List<ChoiceCondition>();
        public ConditionLogic conditionLogic = ConditionLogic.AND;
    }
}