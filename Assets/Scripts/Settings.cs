using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using UnityEngine;

public class Settings : MonoBehaviour
{
    public static Settings instance;
    public bool isInGame = false;
    public CanvasGroup canvasGroup;

    public GameObject inGameSettings;
    public GameObject outGameSettings;

    private void Awake()
    {
        instance = this;
    }

    private void Update()
    {
        if (isInGame)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                OpenSettings();
            }
        }
    }

    public void OpenSettings()
    {
        canvasGroup.alpha = 1;
        Debug.Log("222");
        if (isInGame)
        {
            inGameSettings.SetActive(true);
        } else
        {
            outGameSettings.SetActive(true);
        }
     }

    public void Return()
    {
        canvasGroup.alpha = 0;
    }

}
