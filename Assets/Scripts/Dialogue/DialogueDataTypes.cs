using System;
using System.Collections.Generic;

namespace DialogueSystem
{
    // ==================== 比较类型 ====================
    /// <summary>
    /// Comparison operators for conditions
    /// </summary>
    [Serializable]
    public enum ComparisonType
    {
        Equal,              // ==
        NotEqual,           // !=
        Greater,            // >
        Less,               // 
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
    /// Helper class for serializing choice data lists
    /// </summary>
    [Serializable]
    public class SerializableChoiceDataList
    {
        public List<ChoiceData> choicesData = new List<ChoiceData>();
    }

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
}