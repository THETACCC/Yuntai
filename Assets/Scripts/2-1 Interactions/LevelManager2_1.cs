using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LevelManager2_1 : MonoBehaviour
{
    public bool isZhouShu = false;

    public int myLoop = 2;


    public bool isPlayerEscaped = false;

    //Scene Related

    public string SceneName_NotEscaped;
    public int SpawnPointLocation_NotEscaped;

    public string SceneName_Escaped;
    public int SpawnPointLocation_Escaped;

    public ScenePortal BathroomPortal;

    public void SpeakZhoushu()
    {
        isZhouShu = true;
        BathroomPortal.scenename = SceneName_Escaped;
        BathroomPortal.SpawnPointLocation = SpawnPointLocation_Escaped;
    }

    public void Start()
    {
        LoopTracker.I?.SetLoop(myLoop);
        BathroomPortal.scenename = SceneName_NotEscaped;
        BathroomPortal.SpawnPointLocation = SpawnPointLocation_NotEscaped;
    }

    public void Update()
    {

    }

}
