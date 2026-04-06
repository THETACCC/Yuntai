using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NoDestroy : MonoBehaviour
{

    public static NoDestroy instance;
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
}
