using System;
using System.Collections.Generic;
using DialogueSystem;
using TMPro;
using UnityEngine;

[Serializable]
public class TimedDialogueCue
{
    public int conversationIndex;
    public float showTime;
    public float hideTime;
    public Color textColor = Color.white;

    [Header("Optional Tutorial Image")]
    public GameObject canvasImageObject;
}

public class MusicGameDialogue : MonoBehaviour
{
    [Header("Dialogue JSON")]
    public TextAsset dialogueJsonFile;

    [Header("Refs")]
    public RhythmConductor rhythmConductor;
    public TextMeshProUGUI dialogueTMP;
    public GameObject dialogueBGObject;

    [Header("Tutorial Canvas Images")]
    public GameObject tutorialImage1;
    public GameObject tutorialImage2;

    [Header("Special Font For This Music Game Only")]
    public TMP_FontAsset specialFont;

    [Header("Display")]
    public bool useFade = true;
    public float fadeSpeed = 8f;

    private DialogueData dialogueData;
    private readonly Dictionary<int, Conversation> conversationDict = new Dictionary<int, Conversation>();

    private int currentCueIndex = -1;

    private TimedDialogueCue[] cues;

    private void Awake()
    {
        if (dialogueTMP == null)
            dialogueTMP = GetComponent<TextMeshProUGUI>();

        if (dialogueTMP != null)
        {
            dialogueTMP.text = "";
            SetTMPAlpha(0f);
        }

        if (dialogueBGObject != null)
            dialogueBGObject.SetActive(false);

        SetTutorialImages(false, false);

        cues = new TimedDialogueCue[]
{
    // Tutorial 1
    new TimedDialogueCue
    {
        conversationIndex = 0,
        showTime = 0.794f,
        hideTime = 4.500f,
        textColor = Color.white,
        canvasImageObject = tutorialImage1
    },

    // Tutorial 2
    new TimedDialogueCue
    {
        conversationIndex = 9,
        showTime = 4.500f,
        hideTime = 9.700f,
        textColor = Color.white,
        canvasImageObject = tutorialImage2
    },

    // Story dialogues
    new TimedDialogueCue { conversationIndex = 10, showTime = 42.000f, hideTime = 44.071f, textColor = Color.white },
    new TimedDialogueCue { conversationIndex = 1, showTime = 44.071f, hideTime = 46.268f, textColor = Color.red },
    new TimedDialogueCue { conversationIndex = 2, showTime = 59.780f, hideTime = 61.980f, textColor = Color.white },
    new TimedDialogueCue { conversationIndex = 3, showTime = 61.980f, hideTime = 63.883f, textColor = Color.red },
    new TimedDialogueCue { conversationIndex = 4, showTime = 63.883f, hideTime = 65.338f, textColor = Color.white },
    new TimedDialogueCue { conversationIndex = 5, showTime = 65.338f, hideTime = 66.500f, textColor = Color.white },
    new TimedDialogueCue { conversationIndex = 6, showTime = 73.895f, hideTime = 76.144f, textColor = Color.red },
    new TimedDialogueCue { conversationIndex = 7, showTime = 76.144f, hideTime = 78.028f, textColor = Color.red },
    new TimedDialogueCue { conversationIndex = 8, showTime = 78.028f, hideTime = 79.200f, textColor = Color.white }
};
    }

    private void Start()
    {
        LoadDialogueFromFile(dialogueJsonFile);
    }

    private void Update()
    {
        if (rhythmConductor == null || rhythmConductor.music == null || dialogueTMP == null)
            return;

        if (dialogueData == null || cues == null || cues.Length == 0)
        {
            HideTMP();
            SetBGActive(false);
            SetTutorialImages(false, false);
            return;
        }

        float currentTime = rhythmConductor.music.time;
        int newCueIndex = FindCurrentCueIndex(currentTime);

        if (newCueIndex != currentCueIndex)
        {
            currentCueIndex = newCueIndex;

            if (currentCueIndex >= 0)
                ShowCue(cues[currentCueIndex]);
            else
                HideTextOnly();
        }

        if (currentCueIndex >= 0)
        {
            SetBGActive(true);
            ShowTMP();
            UpdateTutorialImages(cues[currentCueIndex]);
        }
        else
        {
            SetBGActive(false);
            HideTMP();
            SetTutorialImages(false, false);
        }
    }

    public void LoadDialogueFromFile(TextAsset file)
    {
        if (file == null)
        {
            Debug.LogError("[MusicGameDialogue] Dialogue JSON file is not assigned!");
            dialogueData = null;
            return;
        }

        string jsonContent = file.text;
        dialogueData = JsonUtility.FromJson<DialogueData>(jsonContent);

        conversationDict.Clear();

        if (dialogueData?.conversations != null)
        {
            foreach (var conversation in dialogueData.conversations)
                conversationDict[conversation.index] = conversation;
        }
        else
        {
            Debug.LogError("[MusicGameDialogue] Failed to parse dialogue json.");
        }
    }

    private int FindCurrentCueIndex(float currentTime)
    {
        for (int i = 0; i < cues.Length; i++)
        {
            if (currentTime >= cues[i].showTime && currentTime < cues[i].hideTime)
                return i;
        }

        return -1;
    }

    private void ShowCue(TimedDialogueCue cue)
    {
        if (!conversationDict.TryGetValue(cue.conversationIndex, out var conversation))
        {
            Debug.LogWarning($"[MusicGameDialogue] conversation index {cue.conversationIndex} not found in json.");
            dialogueTMP.text = "";
            return;
        }

        if (specialFont != null)
            dialogueTMP.font = specialFont;

        string lang = "zh";

        if (Settings.instance != null && !string.IsNullOrEmpty(Settings.instance.currentLanguage))
            lang = Settings.instance.currentLanguage;

        dialogueTMP.text = conversation.content.GetText(lang);

        SetTMPColor(cue.textColor);
    }

    private void UpdateTutorialImages(TimedDialogueCue cue)
    {
        SetTutorialImages(
            cue.canvasImageObject == tutorialImage1,
            cue.canvasImageObject == tutorialImage2
        );
    }

    private void SetTutorialImages(bool image1Active, bool image2Active)
    {
        if (tutorialImage1 != null && tutorialImage1.activeSelf != image1Active)
            tutorialImage1.SetActive(image1Active);

        if (tutorialImage2 != null && tutorialImage2.activeSelf != image2Active)
            tutorialImage2.SetActive(image2Active);
    }

    private void HideTextOnly()
    {
        if (!useFade)
            dialogueTMP.text = "";
    }

    private void ShowTMP()
    {
        if (!useFade)
        {
            SetTMPAlpha(1f);
            return;
        }

        float a = Mathf.MoveTowards(dialogueTMP.alpha, 1f, fadeSpeed * Time.deltaTime);
        SetTMPAlpha(a);
    }

    private void HideTMP()
    {
        if (!useFade)
        {
            SetTMPAlpha(0f);
            dialogueTMP.text = "";
            return;
        }

        float a = Mathf.MoveTowards(dialogueTMP.alpha, 0f, fadeSpeed * Time.deltaTime);
        SetTMPAlpha(a);

        if (a <= 0.001f)
            dialogueTMP.text = "";
    }

    private void SetTMPAlpha(float alpha)
    {
        Color c = dialogueTMP.color;
        c.a = alpha;
        dialogueTMP.color = c;
    }

    private void SetTMPColor(Color color)
    {
        Color c = color;
        c.a = dialogueTMP.color.a;
        dialogueTMP.color = c;
    }

    private void SetBGActive(bool active)
    {
        if (dialogueBGObject != null && dialogueBGObject.activeSelf != active)
            dialogueBGObject.SetActive(active);
    }

    public void ResetDialogue()
    {
        currentCueIndex = -1;

        if (dialogueTMP != null)
        {
            dialogueTMP.text = "";
            SetTMPAlpha(0f);
        }

        SetBGActive(false);
        SetTutorialImages(false, false);
    }
}