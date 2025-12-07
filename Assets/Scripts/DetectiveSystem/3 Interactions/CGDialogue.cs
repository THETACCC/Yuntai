using DialogueSystem;
using Fungus;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Unity.Burst.Intrinsics.X86.Avx;

public class CGDialogue : MonoBehaviour
{
    //public bool begin = false;
    public TextAsset dialogueJsonFile;
    [HideInInspector]public GameObject contentParent;
    [HideInInspector] public GameObject oneLine;
    private DialogueData dialogueData;
    public float durationPerLine;
    private Dictionary<int, Conversation> conversationDict = new Dictionary<int, Conversation>();
    private CanvasGroup UIGroup;

    private void Awake()
    {
        UIGroup = GetComponent<CanvasGroup>();
    }

    // Start is called before the first frame update
    void Start()
    {
        LoadDialogueFromFile(dialogueJsonFile);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void StartCG()
    {
        Settings.instance.canOpenSettings = false;
        StartCoroutine(Tweening.StartTweening(
        TweeningCurve.Linear,
            durationPerLine,
        t => UIGroup.alpha = t,
        () =>
            {
                UIGroup.alpha = 1;
                PrintNextLine(conversationDict[dialogueData.currentIndex]);
            }
            ));
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

    void PrintNextLine(Conversation conversation)
    {
        GameObject newLine = Instantiate(oneLine, contentParent.transform);
        TextMeshProUGUI tmp = newLine.GetComponent<TextMeshProUGUI>();
        tmp.font = Settings.instance.fontDictionary[Settings.instance.currentLanguage];
        tmp.text = conversation.content.GetText(Settings.instance.currentLanguage);
        StartCoroutine(Tweening.StartTweening(
            TweeningCurve.Linear,
            durationPerLine,
            t => tmp.alpha = t,
            () =>
            {
                tmp.alpha = 1;
                if (conversation.nextIndex != -1)
                {
                    dialogueData.currentIndex++;
                    PrintNextLine(conversationDict[dialogueData.currentIndex]);
                } else
                {
                    EndCG();
                }
            }
            ));
    }

    void EndCG()
    {
        StartCoroutine(Tweening.StartTweening(
            TweeningCurve.Linear, 2f,
            t => UIGroup.alpha = 1 - t,
            () => {
                UIGroup.alpha = 0;
            }));
    }
}
