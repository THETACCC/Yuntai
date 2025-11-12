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

    private void Start()
    {
        if (currentLanguage != null)
        {
            SetLanguage(currentLanguage);
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
        DialogueSettings.instance.SetLanguage(languageCode);
    }
}
