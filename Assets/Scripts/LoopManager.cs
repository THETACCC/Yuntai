using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class LoopManager : MonoBehaviour
{
    public static LoopManager instance;

    public int currentLoop = 1;


    private void Awake()
    {
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

    public void IncreaseLoop()
    {
        currentLoop = currentLoop + 1;
    }

    public void DecreaseLoop()
    {
        currentLoop = currentLoop - 1;
    }

}
