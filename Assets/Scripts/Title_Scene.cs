using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Title_Scene : MonoBehaviour
{
    public GameObject savesSlots;
    public GameObject defaultOptions;
    public void NewGame()
    {
        //call start function -- TODO

        //open save slots UI
        defaultOptions.SetActive(false);
        savesSlots.SetActive(true);
    }
}
