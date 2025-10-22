using System;
using System.Collections.Generic;

namespace DialogueSystem
{
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
        public string targetObjectName;    // GameObject name
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
        public string targetObjectName = "";  // Target GameObject name
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
        public string text;
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
        public string id;                    // 唯一ID
        public string character;             // Manager中显示的分类名称
        public string characterName;         // 角色名称（用于运行时）
        public string avatarAssetPath;       // Avatar 资源路径

        public CharacterData()
        {
            id = Guid.NewGuid().ToString();
            character = "New Character";
            characterName = "New Character";
            avatarAssetPath = "";
        }

        public CharacterData(string name, string avatarPath)
        {
            id = Guid.NewGuid().ToString();
            character = name;
            characterName = name;
            avatarAssetPath = avatarPath;
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
        public string characterId = "";      // 角色ID引用（新）
        // 移除: public string name;
        // 移除: public string avatarAssetPath;
        public string content;
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
        public string choiceText;
        public int branchPriority;
    }

    // ==================== 运行时数据结构 ====================

    /// <summary>
    /// 运行时对话数据 - 用于游戏运行时
    /// 导出时从 characterId 解析出 name 和 avatarAddr
    /// </summary>
    [Serializable]
    public class RuntimeDialogueData
    {
        public int index;
        public string name;              // 运行时解析的角色名称
        public string avatarAddr;        // 运行时解析的 avatar 路径
        public string content;
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
        public string text;
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