using TMPro;
using UnityEngine;

public class Settings : MonoBehaviour
{
    public static Settings instance;
    public bool isInGame = false;
    public CanvasGroup canvasGroup;

    public GameObject inGameSettings;
    public GameObject outGameSettings;

    public string currentLanguage = "en";
    public TMP_FontAsset ChineseFont;
    public TMP_FontAsset EnglishFont;
    public TMP_FontAsset JapaneseFont;

    bool isOpen = false;

    private void Awake()
    {
        canvasGroup.alpha = 0;
        canvasGroup.blocksRaycasts = false;
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

    private void Update()
    {
        if (isInGame)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (isOpen)
                {
                    CloseSettings();
                } else
                {
                    OpenSettings();
                }
            }
        }
    }

    public void OpenSettings()
    {
        isOpen = true;
        canvasGroup.alpha = 1;
        canvasGroup.blocksRaycasts = true;
        Debug.Log("222");
        if (isInGame)
        {
            inGameSettings.SetActive(true);
            outGameSettings.SetActive(false);
        } else
        {
            inGameSettings.SetActive(false);
            outGameSettings.SetActive(true);
        }
     }

    public void CloseSettings()
    {
        isOpen = false;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.alpha = 0;
    }

    public void ReturnToMain()
    {
        SaveManager.instance.SaveGame();
        SceneController.instance.LoadScene("TitleScene");
        CloseSettings();
    }

    public void Save()
    {
        SaveManager.instance.SaveGame();
    }

    public void SetLanguage(string languageCode)
    {
        currentLanguage = languageCode;
        switch (currentLanguage)
        {
            case "zh":
                DialogueManager.instance.speaker.font = ChineseFont;
                DialogueManager.instance.contentText.font = ChineseFont;
                break;
            case "en":
                DialogueManager.instance.speaker.font = EnglishFont;
                DialogueManager.instance.contentText.font = ChineseFont;
                break;
            case "ja":
                DialogueManager.instance.speaker.font = JapaneseFont;
                DialogueManager.instance.contentText.font = ChineseFont;
                break;
        }

        
    }
}
