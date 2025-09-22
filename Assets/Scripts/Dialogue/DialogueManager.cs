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

        //update information
        contentText.text = dialogueData.conversations[dialogueData.currentIndex].content;
        speaker.text = dialogueData.conversations[dialogueData.currentIndex].name;
        if (dialogueData.conversations[dialogueData.currentIndex].choices.Length > 0)
        {
            DialogueDefaultSequence.instance.isButtonActice = false; //make sure default is turned off
            for (int i = 0; i < dialogueData.conversations[dialogueData.currentIndex].choices.Length; i++)
            {
                GameObject newChoice = Instantiate(choicePrefab, choiceParent.transform);
                newChoice.GetComponentInChildren<TextMeshProUGUI>().text = dialogueData.conversations[dialogueData.currentIndex].choices[i].text;
                newChoice.GetComponent<DialogueChoice>().index = dialogueData.conversations[dialogueData.currentIndex].choices[i].targetIndex;
            }
        }
        else
        {
            DialogueDefaultSequence.instance.isButtonActice = true; // turn on default
        }

        // 新的事件调用系统
        ExecuteEventCalls(dialogueData.conversations[dialogueData.currentIndex].eventCalls);
    }

    // 新增：执行事件调用的方法
    private void ExecuteEventCalls(List<DialogueEventCall> eventCalls)
    {
        if (eventCalls == null || eventCalls.Count == 0) return;

        foreach (var eventCall in eventCalls)
        {
            try
            {
                // 查找目标GameObject
                GameObject targetObj = GameObject.Find(eventCall.targetObjectName);
                if (targetObj == null)
                {
                    Debug.LogWarning($"GameObject '{eventCall.targetObjectName}' not found for event call");
                    continue;
                }

                // 获取指定的Component
                Component targetComponent = null;

                // 首先尝试通过Type.GetType获取类型
                Type componentType = Type.GetType(eventCall.componentTypeName);
                if (componentType == null)
                {
                    // 如果失败，尝试在UnityEngine命名空间中查找
                    componentType = Type.GetType($"UnityEngine.{eventCall.componentTypeName}");
                }
                if (componentType == null)
                {
                    // 如果还是失败，尝试在当前程序集中查找
                    componentType = Assembly.GetExecutingAssembly().GetType(eventCall.componentTypeName);
                }

                if (componentType != null)
                {
                    targetComponent = targetObj.GetComponent(componentType);
                }
                else
                {
                    // 如果类型获取失败，尝试通过GetComponent(string)方法
                    targetComponent = targetObj.GetComponent(eventCall.componentTypeName);
                }

                if (targetComponent == null)
                {
                    Debug.LogWarning($"Component '{eventCall.componentTypeName}' not found on GameObject '{eventCall.targetObjectName}'");
                    continue;
                }

                // 调用方法
                InvokeMethod(targetComponent, eventCall);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error executing event call: {e.Message}");
            }
        }
    }

    // 新增：通过反射调用方法
    private void InvokeMethod(Component component, DialogueEventCall eventCall)
    {
        Type componentType = component.GetType();

        // 根据参数类型准备参数数组
        object[] parameters = null;
        Type[] parameterTypes = null;

        switch (eventCall.parameterType)
        {
            case ParameterType.None:
                parameters = new object[0];
                parameterTypes = new Type[0];
                break;
            case ParameterType.String:
                parameters = new object[] { eventCall.stringParameter };
                parameterTypes = new Type[] { typeof(string) };
                break;
            case ParameterType.Int:
                parameters = new object[] { eventCall.intParameter };
                parameterTypes = new Type[] { typeof(int) };
                break;
            case ParameterType.Float:
                parameters = new object[] { eventCall.floatParameter };
                parameterTypes = new Type[] { typeof(float) };
                break;
            case ParameterType.Bool:
                parameters = new object[] { eventCall.boolParameter };
                parameterTypes = new Type[] { typeof(bool) };
                break;
        }

        // 查找方法
        MethodInfo method = componentType.GetMethod(eventCall.methodName, parameterTypes);

        if (method == null)
        {
            // 如果精确匹配失败，尝试按名称查找（适用于重载方法）
            method = componentType.GetMethod(eventCall.methodName);
        }

        if (method != null)
        {
            try
            {
                method.Invoke(component, parameters);
                Debug.Log($"Successfully called {componentType.Name}.{eventCall.methodName}() on {component.gameObject.name}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Error invoking method {eventCall.methodName}: {e.Message}");
            }
        }
        else
        {
            Debug.LogWarning($"Method '{eventCall.methodName}' not found on component '{componentType.Name}'");
        }
    }

    void EndDialogue()
    {
        DialogueDefaultSequence.instance.isButtonActice = false;

        //animation
        StartCoroutine(Tweening.StartTweening(
            TweeningCurve.Linear,
            1f,
            (t) =>
            {
                UIGroup.alpha = 1 - t;
            },
            () => {
                //End things
                UIGroup.alpha = 0;
                Gamemanager.instance?.EndDialogue();
                currentTrigger.isMainDialogueFinished = true;
                //gameObject.SetActive(false);
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
        dialogueData.currentIndex = 0;
        //animation
        StartCoroutine(Tweening.StartTweening(TweeningCurve.Linear, 1f, (t) =>
        {
            UIGroup.alpha = t;
        }));

        UpdateDialogue();
    }

    public void SetDialogueIndex(int index)
    {
        dialogueData.currentIndex = index;
    }

    public void NextDialogueIndex()
    {
        dialogueData.currentIndex = dialogueData.conversations[dialogueData.currentIndex].nextIndex;
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