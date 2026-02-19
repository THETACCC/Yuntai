using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LevelManager4_7 : BaseLevelManager
{
    [SerializeField] private GameObject NPCTrigger;

    public void DisableNPCTrigger()
    {
        NPCTrigger.SetActive(false);
    }
}
