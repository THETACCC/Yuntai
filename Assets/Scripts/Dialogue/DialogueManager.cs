using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.Rendering;
using DialogueSystem;
using Fungus;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager instance;

    public bool isDialogueActive = false;
    [SerializeField] CanvasGroup UIGroup;
    [SerializeField] Image avatar;
    [SerializeField] TextMeshProUGUI speaker;
    [SerializeField] TextMeshProUGUI contentText;

    public float textSpeed = 0.03f;      // 每个字出现的速度
    public float punctuationPause = 0.3f; // 标点停顿时间
    private Coroutine textAnimationCoroutine;   // 当前的文字动画协程

    [SerializeField] GameObject choiceParent;
    [SerializeField] GameObject choicePrefab;

    public DialogueTrigger currentTrigger; //当前触发对话的对象
    public DialogueData dialogueData;

    // Conversation 快速查找字典（index -> Conversation）
    private Dictionary<int, Conversation> conversationDict = new Dictionary<int, Conversation>();

    public bool isDialogueFinished = false;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        //LoadDialogueFromResources();
        UIGroup.alpha = 0;
    }

    private void Update()
    {
        //进入下一行的代码在DialogueDefaultSequence里

    }

    public void UpdateDialogue()
    {
        //clean choices
        for (int i = choiceParent.transform.childCount - 1; i >= 0; i--)
        {
            Destroy(choiceParent.transform.GetChild(i).gameObject);
        }

        //close Dialogue if index is -1
        if (dialogueData.currentIndex == -1)
        {
            EndDialogue();
            return;
        }

        // 通过 index 字段查找 conversation
        var currentConversation = GetConversationByIndex(dialogueData.currentIndex);
        if (currentConversation == null)
        {
            Debug.LogError($"[DialogueManager] Cannot find conversation with index {dialogueData.currentIndex}");
            EndDialogue();
            return;
        }

        //update information
        // stop previous typing if still running
        if (textAnimationCoroutine != null)
            StopCoroutine(textAnimationCoroutine);

        // start new typing animation
        textAnimationCoroutine = StartCoroutine(TextAnimation(currentConversation.content));
        speaker.text = currentConversation.name;

        if (currentConversation.avatarAddr != null)
        {
            Sprite s = Resources.Load<Sprite>(currentConversation.avatarAddr);
            if (s != null)
            {
                avatar.sprite = s;
            }
            else
            {
                Debug.LogError("Failed to load image from address: Resources/" + currentConversation.avatarAddr);
            }
        }

        // Handle Conditional Branches
        if (currentConversation.conditionalBranches != null)
        {
            if (currentConversation.conditionalBranches.Length > 0)
            {
                for (int i = 0; i < currentConversation.conditionalBranches.Length; i++)
                {
                    ConditionalBranch branch = currentConversation.conditionalBranches[i];

                    //check condition
                    bool conditionResult = ConditionResult(branch.conditions, branch.conditionLogic);

                    if (conditionResult)
                    {
                        currentConversation.nextIndex = branch.targetIndex;
                        break;
                    }
                }
            }
        }



        // Handle choices
        if (currentConversation.choices.Length > 0)
        {
            DialogueDefaultSequence.instance.isActice = false; //make sure default is turned off
            for (int i = 0; i < currentConversation.choices.Length; i++)
            {
                Choice choice = currentConversation.choices[i];

                //check condition
                bool conditionResult = ConditionResult(choice.conditions, choice.conditionLogic);

                ///如果condition正确则生成选项
                if (conditionResult)
                {
                    GameObject newChoice = Instantiate(choicePrefab, choiceParent.transform);
                    newChoice.GetComponentInChildren<TextMeshProUGUI>().text = choice.text;
                    newChoice.GetComponent<DialogueChoice>().index = choice.targetIndex;
                }
            }
            if (choiceParent.transform.childCount == 0)
            {
                DialogueDefaultSequence.instance.isActice = true;
            }
        }
        else
        {
            DialogueDefaultSequence.instance.isActice = true; // turn on default
        }


        if (currentConversation.eventCalls != null && currentConversation.eventCalls.Count != 0)
        {
            foreach (var eventCall in currentConversation.eventCalls)
            {
                if (!DialogueEventExecutor.IsValidEventCall(eventCall))
                {
                    DialogueEventExecutor.LogWarning($"Invalid event call: missing required fields");
                    continue;
                }

                if (!eventCall.triggerOnEnd)
                {
                    DialogueEventExecutor.ExecuteSingleEvent(eventCall);
                }

            }
        }
    }

    private IEnumerator TextAnimation(string text)
    {
        contentText.text = "";
        foreach (char c in text)
        {
            contentText.text += c;

            // 如果是标点符号，增加额外停顿
            if ("，,。.！？!?…".Contains(c.ToString()))
                yield return new WaitForSeconds(punctuationPause);
            else
                yield return new WaitForSeconds(textSpeed);
        }
    }

    bool ConditionResult(List<ChoiceCondition> conditions, ConditionLogic conditionLogic)
    {
        bool isConditionMet = false;

        if (conditions.Count == 0)
        {
            isConditionMet = true;
        }
        else if (conditions.Count == 1)
        {
            ChoiceCondition condition = conditions[0];

            isConditionMet = SingleConditionResult(condition);

        }
        else
        {
            //And Logic
            if (conditionLogic == ConditionLogic.AND)
            {
                bool result = true;
                for (int j = 0; j < conditions.Count; j++)
                {
                    ChoiceCondition condition = conditions[j];

                    bool singleResult = SingleConditionResult(condition);
                    if (!singleResult)
                    {
                        result = false;
                        break;
                    }
                }
                isConditionMet = result;
            }
            //Or Logic
            else if (conditionLogic == ConditionLogic.OR)
            {
                bool result = false;
                for (int j = 0; j < conditions.Count; j++)
                {
                    ChoiceCondition condition = conditions[j];

                    bool singleResult = SingleConditionResult(condition);
                    if (singleResult)
                    {
                        result = true;
                        break;
                    }
                }
                isConditionMet = result;
            }
        }

        return isConditionMet;
    }

    bool SingleConditionResult(ChoiceCondition condition)
    {
        //获取object
        GameObject targetObject = GameObject.Find(condition.targetObjectName);
        if (targetObject == null)
        {
            Debug.LogError($"[Condition Check] GameObject '{condition.targetObjectName}' not found.");
            return false;
        }
        //获取component
        Component targetComponent = targetObject.GetComponent(condition.componentTypeName);
        if (targetComponent == null)
        {
            Debug.LogError($"[Condition Check] Component '{condition.componentTypeName}' not found on '{condition.targetObjectName}'.");
            return false;
        }
        // 尝试获取字段
        Type componentType = targetComponent.GetType();
        object currentValue = null;

        FieldInfo field = componentType.GetField(condition.variableName, BindingFlags.Public | BindingFlags.Instance);
        if (field != null)
        {
            currentValue = field.GetValue(targetComponent);
        }
        else
        {
            //Debug.Log("111");
            // 尝试获取属性
            PropertyInfo property = componentType.GetProperty(condition.variableName, BindingFlags.Public | BindingFlags.Instance);
            if (property != null && property.CanRead)
            {
                currentValue = property.GetValue(targetComponent);

            }
            else
            {
                Debug.LogError($"[Condition Check] Variable '{condition.variableName}' not found in '{condition.componentTypeName}'.");
                return false;
            }
        }

        //compare
        return CompareValues(currentValue, condition.compareValue, condition.comparison);
    }

    bool CompareValues(object currentValue, string compareValueStr, ComparisonType comparisonType)
    {
        if (currentValue == null)
        {
            Debug.LogWarning("[Condition Check] Current value is null.");
            return false;
        }

        try
        {
            // 处理 int 类型
            if (currentValue is int intCurrent)
            {
                if (!int.TryParse(compareValueStr, out int intCompare))
                {
                    Debug.LogWarning($"[Condition Check] Cannot parse '{compareValueStr}' as int.");
                    return false;
                }

                switch (comparisonType)
                {
                    case ComparisonType.Equal:
                        return intCurrent == intCompare;
                    case ComparisonType.NotEqual:
                        return intCurrent != intCompare;
                    case ComparisonType.Greater:
                        return intCurrent > intCompare;
                    case ComparisonType.Less:
                        return intCurrent < intCompare;
                    case ComparisonType.GreaterOrEqual:
                        return intCurrent >= intCompare;
                    case ComparisonType.LessOrEqual:
                        return intCurrent <= intCompare;
                    default:
                        return false;
                }
            }

            // 处理 float 类型
            else if (currentValue is float floatCurrent)
            {
                if (!float.TryParse(compareValueStr, out float floatCompare))
                {
                    Debug.LogWarning($"[Condition Check] Cannot parse '{compareValueStr}' as float.");
                    return false;
                }

                switch (comparisonType)
                {
                    case ComparisonType.Equal:
                        return floatCurrent == floatCompare;
                    case ComparisonType.NotEqual:
                        return floatCurrent != floatCompare;
                    case ComparisonType.Greater:
                        return floatCurrent > floatCompare;
                    case ComparisonType.Less:
                        return floatCurrent < floatCompare;
                    case ComparisonType.GreaterOrEqual:
                        return floatCurrent >= floatCompare;
                    case ComparisonType.LessOrEqual:
                        return floatCurrent <= floatCompare;
                    default:
                        return false;
                }
            }

            // 处理 bool 类型
            else if (currentValue is bool boolCurrent)
            {

                if (!bool.TryParse(compareValueStr, out bool boolCompare))
                {
                    Debug.LogWarning($"[Condition Check] Cannot parse '{compareValueStr}' as bool.");
                    return false;
                }

                switch (comparisonType)
                {
                    case ComparisonType.Equal:
                        return boolCurrent == boolCompare;
                    case ComparisonType.NotEqual:
                        return boolCurrent != boolCompare;
                    default:
                        Debug.LogWarning($"[Condition Check] Bool only supports == and !=.");
                        return false;
                }


            }

            /*
            else if (currentValue is string stringCurrent)
            {
                switch (comparisonType)
                {
                    case ComparisonType.Equal:
                        return stringCurrent == compareValueStr;
                    case ComparisonType.NotEqual:
                        return stringCurrent != compareValueStr;
                    default:
                        Debug.LogWarning($"[Condition Check] String only supports == and !=.");
                        return false;
                }
            }
            */
            else
            {
                Debug.LogWarning($"[Condition Check] Unsupported type: {currentValue.GetType()}");
                return false;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[Condition Check] Error comparing values: {e.Message}");
            return false;
        }
    }

    void EndDialogue()
    {
        DialogueDefaultSequence.instance.isActice = false;
        isDialogueFinished = true;

        StartCoroutine(Tweening.StartTweening(
            TweeningCurve.Linear, 1f,
            t => UIGroup.alpha = 1 - t,
            () => {
                UIGroup.alpha = 0;
                Gamemanager.instance?.EndDialogue();
                if (currentTrigger != null) currentTrigger.isMainDialogueFinished = true;
                isDialogueActive = false;
                isDialogueFinished = false;
            }));
    }


    public void LoadDialogueFromFile(TextAsset dialogueJsonFile)
    {
        if (dialogueJsonFile != null)
        {
            string jsonContent = dialogueJsonFile.text;
            dialogueData = JsonUtility.FromJson<DialogueData>(jsonContent);

            // 构建 index -> Conversation 字典
            conversationDict.Clear();
            if (dialogueData?.conversations != null)
            {
                foreach (var conversation in dialogueData.conversations)
                {
                    conversationDict[conversation.index] = conversation;
                }
                //Debug.Log($"[DialogueManager] Loaded {conversationDict.Count} conversations");
            }

            /*
            // 测试输出
            foreach (var conversation in dialogueData.conversations)
            {
                Debug.Log($"{conversation.name}: {conversation.content}");
            }
            */
        }
        else
        {
            Debug.LogError("Dialogue JSON file is not assigned!");
        }
    }

    public void StartDialogue()
    {
        isDialogueActive = true;
        //StartDialogueAtIndex(0);
        if (dialogueData == null || dialogueData.conversations == null || dialogueData.conversations.Count == 0)
        {
            Debug.LogError("[DialogueManager] dialogueData is null or empty. Load a JSON first.");
            return;
        }

        dialogueData.currentIndex = dialogueData.conversations[0].index;
        // show UI
        StartCoroutine(Tweening.StartTweening(TweeningCurve.Linear, 1f, t => UIGroup.alpha = t));
        UpdateDialogue();
    }


    public void SetDialogueIndex(int index)
    {
        dialogueData.currentIndex = index;
    }

    public void NextDialogueIndex()
    {
        // 通过 index 字段查找 conversation
        var currentConversation = GetConversationByIndex(dialogueData.currentIndex);
        if (currentConversation == null)
        {
            Debug.LogError($"[DialogueManager] Cannot find conversation with index {dialogueData.currentIndex}");
            dialogueData.currentIndex = -1;
            return;
        }

        //Debug.Log(currentConversation.nextIndex);

        if (currentConversation.eventCalls != null && currentConversation.eventCalls.Count != 0)
        {
            foreach (var eventCall in currentConversation.eventCalls)
            {
                if (!DialogueEventExecutor.IsValidEventCall(eventCall))
                {
                    DialogueEventExecutor.LogWarning($"Invalid event call: missing required fields");
                    continue;
                }

                if (eventCall.triggerOnEnd)
                {
                    DialogueEventExecutor.ExecuteSingleEvent(eventCall);
                }

            }
        }

        dialogueData.currentIndex = currentConversation.nextIndex;

    }

    /*
    // 从某个 index 开始（不换 JSON）
    public void StartDialogueAtIndex(int startIndex)
    {
        if (dialogueData == null || dialogueData.conversations == null || dialogueData.conversations.Count == 0)
        {
            Debug.LogError("[DialogueManager] dialogueData is null or empty. Load a JSON first.");
            return;
        }

        dialogueData.currentIndex = startIndex;
        // show UI
        StartCoroutine(Tweening.StartTweening(TweeningCurve.Linear, 1f, t => UIGroup.alpha = t));
        UpdateDialogue();
    }

    // 从指定 JSON + 指定 index 开始
    public void StartDialogueFromJson(TextAsset json, int startIndex = 0)
    {
        if (json == null)
        {
            Debug.LogError("[DialogueManager] JSON is null.");
            return;
        }
        LoadDialogueFromFile(json);
        StartDialogueAtIndex(startIndex);
    }

    //get current dialogue index
    public int GetCurrentDialogueIndex()
    {
        return dialogueData.currentIndex;
    }
    */

    // 通过 index 字段查找 conversation
    private Conversation GetConversationByIndex(int index)
    {
        if (conversationDict.TryGetValue(index, out Conversation conversation))
        {
            return conversation;
        }

        Debug.LogError($"[DialogueManager] Conversation with index {index} not found");
        return null;
    }
}


[Serializable]
public class DialogueData
{
    public List<Conversation> conversations;
    public int currentIndex; //conversation index
}

[Serializable]
public class Conversation
{
    public int index;
    public string name;
    public string avatarAddr; //avatar Address
    public string content;
    public ConditionalBranch[] conditionalBranches;
    public Choice[] choices;
    public int nextIndex; //default next index if no choice, -1 if there is no next conversation
    public List<DialogueEventCall> eventCalls;
}

[Serializable]
public struct Choice
{
    public string text;
    public int targetIndex;
    public List<ChoiceCondition> conditions;
    public ConditionLogic conditionLogic;
}

[Serializable]
public struct ConditionalBranch
{
    public int targetIndex;
    public int priority;
    public List<ChoiceCondition> conditions;
    public ConditionLogic conditionLogic;
}