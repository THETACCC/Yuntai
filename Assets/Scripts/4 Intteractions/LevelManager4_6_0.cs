using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LevelManager4_6_0 : MonoBehaviour
{
    public GameObject NoemaNotes1;
    public GameObject NoemaNotes2;
    public GameObject NoemaNotes3;


    public void DisableNoemaNotes1()
    {
        NoemaNotes1.SetActive(false);
    }

    public void DisableNoemaNotes2()
    {
        NoemaNotes2.SetActive(false);
    }

    public void DisableNoemaNotes3()
    {
        NoemaNotes3.SetActive(false);
    }
}
