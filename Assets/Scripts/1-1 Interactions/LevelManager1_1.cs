using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LevelManager1_1 : BaseLevelManager
{
    [SerializeField] private int myLoop = 1;

    private void Start()
    {
        LoopTracker.I?.SetLoop(myLoop);
    }

    
}
