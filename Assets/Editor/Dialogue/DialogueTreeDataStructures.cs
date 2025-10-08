using System;
using System.Collections.Generic;

namespace DialogueSystem.Editor
{
    [Serializable]
    public enum VariableType { Bool, Int, Float, String }

    [Serializable]
    public enum ComparisonType
    {
        Equal, NotEqual, Greater, Less, GreaterOrEqual, LessOrEqual,
        Contains, StartsWith, EndsWith
    }

    [Serializable]
    public enum ConditionLogic { AND, OR }

    [Serializable]
    public class DialogueVariable
    {
        public string name;
        public VariableType type;
        public string defaultValue;
    }

    [Serializable]
    public class ChoiceCondition
    {
        public string variableName;
        public ComparisonType comparison;
        public string compareValue;
    }

    [Serializable]
    public class ChoiceData
    {
        public string text;
        public List<ChoiceCondition> conditions = new List<ChoiceCondition>();
        public ConditionLogic conditionLogic = ConditionLogic.AND;
    }

    [Serializable]
    public class DialogueTreeData
    {
        public List<DialogueVariable> variables = new List<DialogueVariable>();
        public List<DialogueNodeData> nodes = new List<DialogueNodeData>();
        public List<DialogueConnectionData> connections = new List<DialogueConnectionData>();
    }

    [Serializable]
    public class DialogueNodeData
    {
        public string id;
        public int index;
        public string name;
        public string avatarAssetPath;
        public string content;
        public float positionX;
        public float positionY;
        public List<ChoiceData> choices = new List<ChoiceData>();
        public List<DialogueEventCall> eventCalls = new List<DialogueEventCall>();
    }

    [Serializable]
    public class DialogueConnectionData
    {
        public string outputNodeId;
        public string inputNodeId;
        public int choiceIndex;
        public string choiceText;
    }

    [Serializable]
    public class RuntimeDialogueData
    {
        public int index;
        public string name;
        public string avatarAddr;
        public string content;
        public List<RuntimeChoice> choices = new List<RuntimeChoice>();
        public string nextNodeId;
        public List<DialogueEventCall> eventCalls = new List<DialogueEventCall>();
    }

    [Serializable]
    public class RuntimeChoice
    {
        public string text;
        public string nextNodeId;
        public List<ChoiceCondition> conditions = new List<ChoiceCondition>();
        public ConditionLogic conditionLogic = ConditionLogic.AND;
    }

    [Serializable]
    public class SerializableEventCallList
    {
        public List<DialogueEventCall> eventCalls = new List<DialogueEventCall>();
    }

    [Serializable]
    public class SerializableChoiceDataList
    {
        public List<ChoiceData> choicesData = new List<ChoiceData>();
    }
}