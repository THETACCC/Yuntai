using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager instance;

    [SerializeField] CanvasGroup UIGroup;
    [SerializeField] Image avatar;
    [SerializeField] TextMeshProUGUI speaker;
    [SerializeField] TextMeshProUGUI contentText;

    [SerializeField] GameObject choiceParent;
    [SerializeField] GameObject choicePrefab;

    
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
        } else
        {
            DialogueDefaultSequence.instance.isButtonActice = true; // turn on default
        }
    }

    void EndDialogue()
    {
        //animation
        StartCoroutine(Tweening.StartTweening(
            TweeningCurve.Linear,
            1f,
            (t) =>
            {
                UIGroup.alpha = 1 - t;
            },
            () => {

                Gamemanager.instance.EndDialogue();
                //gameObject.SetActive(false);
            }));
    }

    public void LoadDialogueFromResources(TextAsset dialogueJsonFile)
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
}

[Serializable]
public struct Choice
{
    public string text;
    public int targetIndex;
}