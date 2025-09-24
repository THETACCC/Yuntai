using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ToNextLoop : MonoBehaviour
{
    //scenes
    public string scenename;
    public int SpawnPointLocation;

    public void toNextLoop()
    {
        // Loop +1
        LoopTracker.I?.IncrementLoop();

        SceneController.instance.LoadSceneAndTeleport(scenename, SpawnPointLocation);
    }
}
