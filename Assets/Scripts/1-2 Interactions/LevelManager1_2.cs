using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LevelManager1_2 : MonoBehaviour
{
    [SerializeField] private int myLoop = 1;
    // Start is called before the first frame update
    void Start()
    {
        LoopTracker.I?.SetLoop(myLoop);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
