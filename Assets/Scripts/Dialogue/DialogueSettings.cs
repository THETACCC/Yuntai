using System.Collections;
using System.Collections.Generic;
using System.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class DialogueSettings : MonoBehaviour
{
    public static DialogueSettings instance;

    public DialogueManager dialogueManager;

    public string currentLanguage = "en";
    public TMP_FontAsset ChineseFont;
    public TMP_FontAsset EnglishFont;
    public TMP_FontAsset JapaneseFont;

    [Header("Template Settings")]
    public bool separatePlayerAndNPC;

    private void Awake()
    {
        instance = this;
        dialogueManager = GetComponent<DialogueManager>();
    }


    // Start is called before the first frame update
    void Start()
    {
        if (currentLanguage != null)
        {
            SetLanguage(currentLanguage);
        }
    }


    // Update is called once per frame
    void Update()
    {
        if (Application.isPlaying) return;

        dialogueManager.NPCAvatar.gameObject.SetActive(separatePlayerAndNPC);
        dialogueManager.NPCName.transform.parent.gameObject.SetActive(separatePlayerAndNPC);
    }

    public void SetLanguage(string languageCode)
    {
        currentLanguage = languageCode;
        TextMeshProUGUI speaker = DialogueManager.instance.speaker;
        TextMeshProUGUI content = DialogueManager.instance.contentText;
        TextMeshProUGUI choice = DialogueManager.instance.choicePrefab.GetComponent<DialogueChoice>().content;
        TextMeshProUGUI NPCSpeaker = DialogueManager.instance.NPCName;
        switch (currentLanguage)
        {
            case "zh":
                speaker.font = ChineseFont;
                content.font = ChineseFont;
                choice.font = ChineseFont;
                NPCSpeaker.font = ChineseFont;
                break;
            case "en":
                speaker.font = EnglishFont;
                content.font = EnglishFont;
                choice.font = EnglishFont;
                NPCSpeaker.font = EnglishFont;
                break;
            case "ja":
                speaker.font = JapaneseFont;
                content.font = JapaneseFont;
                choice.font = JapaneseFont;
                NPCSpeaker.font = JapaneseFont;
                break;
        }
    }
}
