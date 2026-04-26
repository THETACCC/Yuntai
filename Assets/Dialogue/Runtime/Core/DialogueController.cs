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
using MoreMountains.Tools;
using static AudioManager;

//原Dialogue Manager，已改名
public class DialogueController : MonoBehaviour
{
    public static DialogueController instance;

    public bool isDialogueActive = false;

    public CanvasGroup UIGroup;
    public Image avatar;
    public TextMeshProUGUI speaker;
    public TextMeshProUGUI contentText;

    public float textSpeed = 0.03f;      // 每个字出现的速度
    public float punctuationPause = 0.3f; // 标点停顿时间
    private Coroutine textAnimationCoroutine;   // 当前的文字动画协程

    [SerializeField] GameObject choiceParent;
    public GameObject choicePrefab;

    public DialogueTrigger currentTrigger; //当前触发对话的对象
    public DialogueData dialogueData;

    // Conversation 快速查找字典（index -> Conversation）
    private Dictionary<int, Conversation> conversationDict = new Dictionary<int, Conversation>();

    public bool isDialogueFinished = false;

    private Conversation currentConversation;

    [Header("Components")]
    public Image NPCAvatar;
    public TextMeshProUGUI NPCName;

    //public GameObject historyButton;
    //public GameObject historyContentUI;
    //public TextMeshProUGUI historyContentText;

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
        if (Settings.instance != null)
        {
            Settings.instance.OnLanguageChanged += UpdateDialogue;
        }
        UIGroup.alpha = 0;
        UIGroup.blocksRaycasts = false;
    }

    private void Update()
    {
        if (isDialogueActive && !Settings.instance.isSettingsOpen)
        {
            MoveToNextInputCheck();
        }
    }

    void MoveToNextInputCheck()
    {
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
        {
            if (textAnimationCoroutine != null)
            {
                StopCoroutine(textAnimationCoroutine);
                textAnimationCoroutine = null;
                contentText.text = currentConversation.content.GetText(Settings.instance.currentLanguage); // 直接显示完整文本
                contentText.maxVisibleCharacters = 99999; // 显示所有字符
            }
            else
            {
                if (DialogueContinueButton.instance.isActice)
                {
                    DialogueContinueButton.instance.GoToNextDialogue();
                }
            }
        }
    }

    public void UpdateDialogue(string languageCode)
    {
        if (isDialogueActive)
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
            currentConversation = GetConversationByIndex(dialogueData.currentIndex);
            if (currentConversation == null)
            {
                Debug.LogError($"[DialogueController] Cannot find conversation with index {dialogueData.currentIndex}");
                EndDialogue();
                return;
            }

            //****************************update information***************************************

            // Apply text alignment
            switch (currentConversation.textAlignment)
            {
                case TextAlignmentType.Center:
                    contentText.alignment = TextAlignmentOptions.Center;
                    break;
                case TextAlignmentType.Right:
                    contentText.alignment = TextAlignmentOptions.Right;
                    break;
                default: // Left
                    contentText.alignment = TextAlignmentOptions.Left;
                    break;
            }

            // stop previous typing if still running
            if (textAnimationCoroutine != null)
                StopCoroutine(textAnimationCoroutine);
            // start new typing animation
            textAnimationCoroutine = StartCoroutine(TextAnimation(currentConversation.content.GetText(languageCode)));
            string speakerName = currentConversation.name.GetText(languageCode);

            //add text history
            // 说话人加粗+颜色，分隔线更清晰
            NoteBookManager.instance.historyContentText.text = $"<b><color=#4A90E2><size=110%>{speakerName}</size></color></b>\n" +
                                      $"<color=#333333>{currentConversation.content.GetText(languageCode)}</color>\n" +
                                      $"<color=#DDDDDD>―――――――――――――</color>\n" +
                                      NoteBookManager.instance.historyContentText.text;

            //check if separate
            if (DialogueDisplaySettings.instance.separatePlayerAndNPC)
            {
                speaker.transform.parent.gameObject.SetActive(currentConversation.isPlayer && speakerName != "");
                NPCName.transform.parent.gameObject.SetActive(!currentConversation.isPlayer && speakerName != "");
                if (currentConversation.isPlayer)
                {
                    speaker.text = speakerName;
                }
                else
                {
                    NPCName.text = speakerName;
                }
            }
            else
            {
                speaker.text = speakerName;
            }

            if (currentConversation.avatarAddr != null)
            {
                if (currentConversation.avatarAddr == "")
                {
                    NPCAvatar.gameObject.SetActive(false);
                    avatar.gameObject.SetActive(false);
                }
                else
                {
                    Sprite s = Resources.Load<Sprite>(currentConversation.avatarAddr);
                    if (s != null)
                    {
                        if (DialogueDisplaySettings.instance.separatePlayerAndNPC)
                        {
                            if (currentConversation.isPlayer)
                            {
                                NPCAvatar.color = DialogueDisplaySettings.instance.inactiveAvatarColor;
                                avatar.color = Color.white;
                                avatar.sprite = s;
                            }
                            else
                            {
                                avatar.color = DialogueDisplaySettings.instance.inactiveAvatarColor;
                                NPCAvatar.color = Color.white;
                                NPCAvatar.sprite = s;
                            }
                            NPCAvatar.gameObject.SetActive(NPCAvatar.sprite != null);
                            avatar.gameObject.SetActive(avatar.sprite != null);
                        }
                        else
                        {
                            avatar.sprite = s;
                        }
                    }
                    else
                    {
                        Debug.LogError("Failed to load image from address: Resources/" + currentConversation.avatarAddr);
                    }
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
            if (currentConversation.choices?.Length > 0)
            {
                DialogueContinueButton.instance.isActice = false; //make sure default is turned off
                for (int i = 0; i < currentConversation.choices.Length; i++)
                {
                    Choice choice = currentConversation.choices[i];

                    //check condition
                    bool conditionResult = ConditionResult(choice.conditions, choice.conditionLogic);

                    ///如果condition正确则生成选项
                    if (conditionResult)
                    {
                        GameObject newChoice = Instantiate(choicePrefab, choiceParent.transform);
                        newChoice.GetComponentInChildren<TextMeshProUGUI>().text = choice.text.GetText(languageCode);
                        newChoice.GetComponent<DialogueChoiceButton>().index = choice.targetIndex;
                    }
                }
                if (choiceParent.transform.childCount == 0)
                {
                    DialogueContinueButton.instance.isActice = true;
                }
            }
            else
            {
                DialogueContinueButton.instance.isActice = true; // turn on default
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

                    if (eventCall.triggerTiming == EventTriggerTiming.OnDialogueStart)
                    {
                        //Debug.Log("红红火火恍恍惚惚");
                        DialogueEventExecutor.ExecuteSingleEvent(eventCall);
                    }

                }
            }
        }
    }

    private IEnumerator TextAnimation(string text)
    {
        contentText.text = text; // 先设置完整文本，让 TMP 解析富文本标签
        contentText.maxVisibleCharacters = 0; // 从 0 个可见字符开始

        // 强制更新文本网格，确保 textInfo 正确
        contentText.ForceMeshUpdate();

        int totalVisibleCharacters = contentText.textInfo.characterCount; // 获取实际字符数（不包括富文本标签）

        for (int i = 0; i <= totalVisibleCharacters; i++)
        {
            contentText.maxVisibleCharacters = i; // 逐渐显示字符
            //Audio
            AudioManager.Play("Sound Effects/Henk/sndType2", AudioGroup.SFX, this.gameObject.transform);
            // 获取当前显示的最后一个字符来判断是否需要标点停顿
            if (i > 0 && i <= totalVisibleCharacters)
            {
                char currentChar = contentText.textInfo.characterInfo[i - 1].character;

                // 如果是标点符号，增加额外停顿
                if ("，,。.！？!?…".Contains(currentChar.ToString()))
                    yield return new WaitForSeconds(punctuationPause);
                else
                    yield return new WaitForSeconds(textSpeed);
            }
            else
            {
                yield return new WaitForSeconds(textSpeed);
            }
        }

        textAnimationCoroutine = null;
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
        //获取object - 优先使用ID查找
        GameObject targetObject = null;

        // 优先使用ID查找
        if (!string.IsNullOrEmpty(condition.targetObjectID))
        {
            targetObject = DialogueEventTarget.FindByID(condition.targetObjectID);
        }

        // 向后兼容：如果没有ID或ID查找失败，使用名字查找
        if (targetObject == null && !string.IsNullOrEmpty(condition.targetObjectName))
        {
            targetObject = GameObject.Find(condition.targetObjectName);

            if (targetObject == null)
            {
                // 如果还没找到，使用 FindObjectsOfTypeAll 查找包括 inactive 的对象
                var allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
                targetObject = System.Array.Find(allObjects, obj => obj.name == condition.targetObjectName && obj.scene.IsValid());
            }
        }

        if (targetObject == null)
        {
            string identifier = !string.IsNullOrEmpty(condition.targetObjectID) ? condition.targetObjectID : condition.targetObjectName;
            Debug.LogError($"[Condition Check] GameObject '{identifier}' not found (searched by ID and name, including inactive objects).");
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
        DialogueContinueButton.instance.isActice = false;
        isDialogueFinished = true;

        var lastConversation = currentConversation;

        StartCoroutine(Tweening.StartTweening(
            TweeningCurve.Linear, 1f,
            t => UIGroup.alpha = 1 - t,
            () => {
                UIGroup.alpha = 0;
                UIGroup.blocksRaycasts = false;
                Gamemanager.instance?.EndDialogue();
                if (currentTrigger != null) currentTrigger.isMainDialogueFinished = true;
                isDialogueActive = false;
                isDialogueFinished = false;

                //excute event on disappear
                if (lastConversation?.eventCalls != null && lastConversation.eventCalls.Count != 0)
                {
                    foreach (var eventCall in lastConversation.eventCalls)
                    {
                        if (!DialogueEventExecutor.IsValidEventCall(eventCall))
                        {
                            DialogueEventExecutor.LogWarning($"Invalid event call: missing required fields");
                            continue;
                        }

                        if (eventCall.triggerTiming == EventTriggerTiming.OnDialogueDisappear)
                        {
                            DialogueEventExecutor.ExecuteSingleEvent(eventCall);
                        }
                    }
                }
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
            }
        }
        else
        {
            Debug.LogError("Dialogue JSON file is not assigned!");
        }
    }

    public void StartDialogue()
    {
        isDialogueActive = true;
        if (dialogueData == null || dialogueData.conversations == null || dialogueData.conversations.Count == 0)
        {
            Debug.LogError("[DialogueController] dialogueData is null or empty. Load a JSON first.");
            return;
        }

        dialogueData.currentIndex = dialogueData.conversations[0].index;

        //clear avatars
        if (DialogueDisplaySettings.instance.separatePlayerAndNPC)
        {
            if (NPCAvatar != null)
            {
                NPCAvatar.sprite = null;
            }
            if (avatar != null)
            {
                avatar.sprite = null;
            }
        }

        // show UI
        StartCoroutine(Tweening.StartTweening(
            TweeningCurve.Linear,
            1f,
            t => UIGroup.alpha = t,
            () =>
            {
                UIGroup.blocksRaycasts = true;
            }
            )
        );
        UpdateDialogue(DialogueDisplaySettings.instance.currentLanguage);
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
            Debug.LogError($"[DialogueController] Cannot find conversation with index {dialogueData.currentIndex}");
            dialogueData.currentIndex = -1;
            return;
        }

        //excute event on end
        if (currentConversation.eventCalls != null && currentConversation.eventCalls.Count != 0)
        {
            foreach (var eventCall in currentConversation.eventCalls)
            {
                if (!DialogueEventExecutor.IsValidEventCall(eventCall))
                {
                    DialogueEventExecutor.LogWarning($"Invalid event call: missing required fields");
                    continue;
                }

                if (eventCall.triggerTiming == EventTriggerTiming.OnDialogueEnd)
                {
                    DialogueEventExecutor.ExecuteSingleEvent(eventCall);
                }

            }
        }

        dialogueData.currentIndex = currentConversation.nextIndex;

    }

    // 通过 index 字段查找 conversation
    private Conversation GetConversationByIndex(int index)
    {
        if (conversationDict.TryGetValue(index, out Conversation conversation))
        {
            return conversation;
        }

        Debug.LogError($"[DialogueController] Conversation with index {index} not found");
        return null;
    }

    /**
    public void OpenCloseHistory()
    {
        if (historyContentUI != null)
        {
            historyContentUI.SetActive(!historyContentUI.activeInHierarchy);
        }
    }
    **/
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
    public LocalizedText name;
    public string avatarAddr; //avatar Address
    public bool isPlayer;
    public LocalizedText content;
    public TextAlignmentType textAlignment; // 文本对齐方式（0=Left, 1=Center, 2=Right）
    public ConditionalBranch[] conditionalBranches;
    public Choice[] choices;
    public int nextIndex; //default next index if no choice, -1 if there is no next conversation
    public List<DialogueEventCall> eventCalls;
}

[Serializable]
public struct Choice
{
    public LocalizedText text;
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
