using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class GameLanguage
{
    public string languageCode;
    public TMP_FontAsset font;
}

public class Settings : MonoBehaviour
{
    public static Settings instance;
    public bool isInGame = false;
    public bool canOpenSettings = true;
    public CanvasGroup canvasGroup;

    [Header("Localization Settings")]
    [SerializeField]
    [Tooltip("Current language code (en, zh, ja)")]
    private string _currentLanguage = "en"; //zh, en, ja
    public string currentLanguage
    {
        get => _currentLanguage;
        set
        {
            if (_currentLanguage != value)
            {
                Debug.Log("to " + value);
                _currentLanguage = value;
                OnLanguageChanged?.Invoke(_currentLanguage);
            }
        }
    }
    public List<GameLanguage> gameLanguages;
    public event Action<string> OnLanguageChanged;
    [HideInInspector] public Dictionary<string, TMP_FontAsset> fontDictionary;

    [Header("Volume Settings")]
    public Slider mainVolumeSlider;
    public TextMeshProUGUI mainVolumeText;
    private UILocalization volumeLocalization;

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
            InitializeFontDictionary();
        }
        else
        {
            Destroy(gameObject);
        }
        volumeLocalization = mainVolumeText.GetComponent<UILocalization>();
    }

    private void Start()
    {
        if (currentLanguage != null && OnLanguageChanged != null)
        {
            OnLanguageChanged(currentLanguage);
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
                    if (canOpenSettings)
                    {
                        OpenSettings();
                    }
                }
            }
        }

        //set volume
        if (isSettingsOpen)
        {
            mainVolumeText.text = volumeLocalization.contentDictionary[currentLanguage] + " " + mainVolumeSlider.value.ToString("F0");
            AudioManager.instance.SetMasterVolume(mainVolumeSlider.value);
        }
        
    }

    void InitializeFontDictionary()
    {
        fontDictionary = new Dictionary<string, TMP_FontAsset>();
        foreach (var pair in gameLanguages)
        {
            if (!string.IsNullOrEmpty(pair.languageCode) && pair.font != null)
            {
                fontDictionary[pair.languageCode] = pair.font;
            }
        }
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
        //LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.GetLocale(currentLanguage); //UI
    }
}
