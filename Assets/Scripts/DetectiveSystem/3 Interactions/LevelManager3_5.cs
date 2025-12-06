using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class LevelManager3_5 : BaseLevelManager
{
    [Header("CGDialogue Settings")]
    public CGDialogue cgDialogue;

    private void Start()
    {
        cgDialogue.StartCG();
    }
}
