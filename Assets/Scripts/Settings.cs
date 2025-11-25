using TMPro;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Settings : MonoBehaviour
{
    public static Settings instance;
    public bool isInGame = false;
    public CanvasGroup canvasGroup;

    public string currentLanguage = "en"; //zh en ja

    public float mainVolume;
    //public Slider mainVolumeSlider;
    //public TextMeshProUGUI mainVolumeNum;

    public bool isSettingsOpen = false;
    int _localeIndex;

    public GameObject returnToMain;
    public GameObject saveFile;

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
                if (isSettingsOpen)
                {
                    CloseSettings();
                } else
                {
                    OpenSettings();
                }
            }
        }

        //mainVolume = mainVolumeSlider.value;
        //mainVolumeNum.text = mainVolumeSlider.value.ToString();
    }

    public void OpenSettings()
    {
        isSettingsOpen = true;
        canvasGroup.alpha = 1;
        canvasGroup.blocksRaycasts = true;
        
        if (isInGame)
        {
            returnToMain.SetActive(true);
            saveFile.SetActive(true);
        } else
        {
            returnToMain.SetActive(false);
            saveFile.SetActive(false);
        }
     }

    public void CloseSettings()
    {
        isSettingsOpen = false;
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

        //更新Dialogue
        DialogueSettings.instance.SetLanguage(currentLanguage); //dialogue
        if (DialogueManager.instance.isDialogueActive)
        {
            DialogueManager.instance.UpdateDialogue();
        }

        //更新Notebook
        NoteBookLocalization.instance.SetLanguage(currentLanguage);

        //更新普通UI
        LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.GetLocale(currentLanguage); //UI

    }
}
