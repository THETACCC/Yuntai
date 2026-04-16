using MoreMountains.Tools;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LevelManager3_7 : MonoBehaviour
{

    //scenes
    public string scenename;
    public int SpawnPointLocation;

    public void toNextScene()
    {
        SceneController.instance.LoadSceneAndTeleport(scenename, SpawnPointLocation);
    }
}
