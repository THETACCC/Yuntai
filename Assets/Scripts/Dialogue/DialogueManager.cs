using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager instance;

    public bool isDialogueActive = false;
    [SerializeField] CanvasGroup UIGroup;
    [SerializeField] Image avatar;
    [SerializeField] TextMeshProUGUI speaker;
    [SerializeField] TextMeshProUGUI contentText;

    [SerializeField] GameObject choiceParent;
    [SerializeField] GameObject choicePrefab;

    public DialogueTrigger currentTrigger; //当前触发对话的对象
    public DialogueData dialogueData;

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
    }

    private void Update()
    {

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

        var currentConversation = dialogueData.conversations[dialogueData.currentIndex];

        //update information
        contentText.text = currentConversation.content;
        speaker.text = currentConversation.name;

        // Handle choices
        if (currentConversation.choices.Length > 0)
        {
            DialogueDefaultSequence.instance.isButtonActice = false; //make sure default is turned off
            for (int i = 0; i < currentConversation.choices.Length; i++)
            {
                GameObject newChoice = Instantiate(choicePrefab, choiceParent.transform);
                newChoice.GetComponentInChildren<TextMeshProUGUI>().text = currentConversation.choices[i].text;
                newChoice.GetComponent<DialogueChoice>().index = currentConversation.choices[i].targetIndex;
            }
        }
        else
        {
            DialogueDefaultSequence.instance.isButtonActice = true; // turn on default
        }

        // 使用新的事件执行器 - 只需要一行代码！
        DialogueEventExecutor.Execute(currentConversation.eventCalls);
    }

    void EndDialogue()
    {
        DialogueDefaultSequence.instance.isButtonActice = false;

        StartCoroutine(Tweening.StartTweening(
            TweeningCurve.Linear, 1f,
            t => UIGroup.alpha = 1 - t,
            () => {
                UIGroup.alpha = 0;
                Gamemanager.instance?.EndDialogue();
                if (currentTrigger != null) currentTrigger.isMainDialogueFinished = true;
                isDialogueActive = false;
            }));
    }


    public void LoadDialogueFromFile(TextAsset dialogueJsonFile)
    {
        if (dialogueJsonFile != null)
        {
            string jsonContent = dialogueJsonFile.text;
            dialogueData = JsonUtility.FromJson<DialogueData>(jsonContent);

            // 测试输出
            foreach (var conversation in dialogueData.conversations)
            {
                Debug.Log($"{conversation.name}: {conversation.content}");
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
        StartDialogueAtIndex(0);
    }


    public void SetDialogueIndex(int index)
    {
        dialogueData.currentIndex = index;
    }

    public void NextDialogueIndex()
    {
        dialogueData.currentIndex = dialogueData.conversations[dialogueData.currentIndex].nextIndex;
    }

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
}

// 新增：事件调用数据结构（与编辑器中的保持一致）
[Serializable]
public class DialogueEventCall
{
    public string targetObjectName = "";
    public string componentTypeName = "";
    public string methodName = "";
    public string stringParameter = "";
    public int intParameter = 0;
    public float floatParameter = 0f;
    public bool boolParameter = false;
    public ParameterType parameterType = ParameterType.None;
}

[Serializable]
public enum ParameterType
{
    None,
    String,
    Int,
    Float,
    Bool
}

[Serializable]
public class DialogueData
{
    public List<Conversation> conversations;
    public int currentIndex; //conversation index
}

[Serializable]
public struct Conversation
{
    public int index;
    public string name;
    public string avatarAddr; //avatar Address
    public string content;
    public Choice[] choices;
    public int nextIndex; //default next index if no choice, -1 if there is no next conversation
    public List<DialogueEventCall> eventCalls; // 替换原来的 UnityEvent onDialogueEvent
}

[Serializable]
public struct Choice
{
    public string text;
    public int targetIndex;
}