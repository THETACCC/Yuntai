using System.Collections;
using System.Collections.Generic;
using System.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DialogueLanguage
{
    public string languageCode;
    public TMP_FontAsset font;
}

[ExecuteAlways]
public class DialogueDisplaySettings : MonoBehaviour
{
    public static DialogueDisplaySettings instance;

    public DialogueController dialogueController;

    public string currentLanguage = "en";
    public List<DialogueLanguage> DialogueLanguages;
    private Dictionary<string, TMP_FontAsset> fontDictionary;

    [Header("Template Settings")]
    public bool separatePlayerAndNPC;
    public Color inactiveAvatarColor;

    //public bool showDialogueHistory;

    private void Awake()
    {
        instance = this;
        dialogueController = GetComponent<DialogueController>();

        if (Settings.instance != null && (DialogueLanguages == null || DialogueLanguages.Count == 0))
        {
            DialogueLanguages = new List<DialogueLanguage>();

            foreach (var language in Settings.instance.gameLanguages)
            {
                DialogueLanguages.Add(new DialogueLanguage
                {
                    languageCode = language.languageCode,
                    font = language.font
                });
            }
        }

        InitializeFontDictionary();

    }


    // Start is called before the first frame update
    void Start()
    {
        // 确保Settings已经初始化
        if (Settings.instance != null)
        {
            // 如果Awake时Settings还没初始化，在这里重新初始化DialogueLanguages
            if (DialogueLanguages == null || DialogueLanguages.Count == 0)
            {
                DialogueLanguages = new List<DialogueLanguage>();
                foreach (var language in Settings.instance.gameLanguages)
                {
                    DialogueLanguages.Add(new DialogueLanguage
                    {
                        languageCode = language.languageCode,
                        font = language.font
                    });
                }
                InitializeFontDictionary();
            }

            currentLanguage = Settings.instance.currentLanguage;
            Settings.instance.OnLanguageChanged += SetLanguage;
            SetLanguage(currentLanguage);
        }
        else
        {
            Debug.LogWarning("[DialogueDisplaySettings] Settings.instance is null in Start!");
        }
    }


    // Update is called once per frame
    void Update()
    {
        if (Application.isPlaying) return;

        // 添加空引用检查，避免编辑器模式下报错
        if (dialogueController == null) return;

        if (dialogueController.NPCAvatar != null)
        {
            dialogueController.NPCAvatar.gameObject.SetActive(separatePlayerAndNPC);
        }

        if (dialogueController.NPCName != null && dialogueController.NPCName.transform.parent != null)
        {
            dialogueController.NPCName.transform.parent.gameObject.SetActive(separatePlayerAndNPC);
        }

        /**
        if (dialogueController.historyButton != null)
        {
            dialogueController.historyButton.SetActive(showDialogueHistory);
        }
        **/
    }

    private void InitializeFontDictionary()
    {
        fontDictionary = new Dictionary<string, TMP_FontAsset>();

        if (DialogueLanguages == null)
        {
            Debug.LogWarning("[DialogueDisplaySettings] DialogueLanguages is null, cannot initialize font dictionary!");
            return;
        }

        foreach (var pair in DialogueLanguages)
        {
            if (!string.IsNullOrEmpty(pair.languageCode) && pair.font != null)
            {
                fontDictionary[pair.languageCode] = pair.font;
            }
        }

        if (fontDictionary.Count == 0)
        {
            Debug.LogWarning("[DialogueDisplaySettings] Font dictionary is empty after initialization!");
        }
    }

    public void SetLanguage(string languageCode)
    {
        currentLanguage = languageCode;

        DialogueController dm = DialogueController.instance;

        if (dm != null)
        {
            if (fontDictionary == null || fontDictionary.Count == 0)
            {
                Debug.LogWarning("[DialogueDisplaySettings] Font dictionary is not initialized!");
                return;
            }

            if (!fontDictionary.ContainsKey(languageCode))
            {
                Debug.LogWarning($"[DialogueDisplaySettings] Font for language '{languageCode}' not found in dictionary!");
                return;
            }

            TMP_FontAsset font = fontDictionary[languageCode];

            if (dm.speaker != null) dm.speaker.font = font;
            if (dm.contentText != null) dm.contentText.font = font;
            if (dm.NPCName != null) dm.NPCName.font = font;
            if (dm.choicePrefab != null)
            {
                var choice = dm.choicePrefab.GetComponent<DialogueChoiceButton>();
                if (choice != null && choice.content != null)
                {
                    choice.content.font = font;
                }
            }
        }
    }
}