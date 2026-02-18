using System;
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
    // CG 播放完的回调（例如让 LevelManager 接后续演出）
    public Action OnCGFinished;

    public TextAsset dialogueJsonFile;
    [HideInInspector] public GameObject contentParent;
    [HideInInspector] public GameObject oneLine;

    private DialogueData dialogueData;
    public float durationPerLine;
    private Dictionary<int, Conversation> conversationDict = new Dictionary<int, Conversation>();
    private CanvasGroup UIGroup;

    private void Awake()
    {
        UIGroup = GetComponent<CanvasGroup>();
    }

    void Start()
    {
        LoadDialogueFromFile(dialogueJsonFile);
    }

    public void StartCG()
    {
        Settings.instance.canOpenSettings = false;

        // CG UI 淡入，然后开始播放第一句
        StartCoroutine(Tweening.StartTweening(
            TweeningCurve.Linear,
            durationPerLine,
            t => UIGroup.alpha = t,
            () =>
            {
                UIGroup.alpha = 1;
                if (dialogueData == null || dialogueData.conversations == null ||
                    !conversationDict.ContainsKey(dialogueData.currentIndex))
                {
                    Debug.LogWarning("[CGDialogue] Dialogue data is invalid, EndCG directly.");
                    EndCG();
                    return;
                }

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
                    if (conversationDict.TryGetValue(dialogueData.currentIndex, out var nextConv))
                    {
                        PrintNextLine(nextConv);
                    }
                    else
                    {
                        Debug.LogWarning("[CGDialogue] nextIndex not found in conversationDict, EndCG.");
                        EndCG();
                    }
                }
                else
                {
                    // 没有下一句了，结束 CG
                    EndCG();
                }
            }
        ));
    }

    void EndCG()
    {
        // CG UI 淡出
        StartCoroutine(Tweening.StartTweening(
            TweeningCurve.Linear, 2f,
            t => UIGroup.alpha = 1 - t,
            () =>
            {
                UIGroup.alpha = 0;
                Settings.instance.canOpenSettings = true;
                // ⭐ 通知外部 “CG 完了”
                OnCGFinished?.Invoke();
            }
        ));
    }
}
