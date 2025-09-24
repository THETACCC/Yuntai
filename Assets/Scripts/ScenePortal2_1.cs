using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScenePortal2_1 : ScenePortal
{
    public bool isPlayerActed = false;
    public string scenename_Escaped;
    public int SpawnPointLocation_Escaped;

    protected override void Update()
    {
        // your custom logic here
        if (isPlayerInTrigger)
        {
            if (Input.GetKeyDown(KeyCode.E))
            {
                if(!isPlayerActed)
                {
                    SceneController.instance.LoadSceneAndTeleport(scenename, SpawnPointLocation);

                }
                else
                {
                    SceneController.instance.LoadSceneAndTeleport(scenename_Escaped, SpawnPointLocation_Escaped);
                }

            }
        }

        // optionally call the base class¡¯s Update if you want the parent behavior too
        // base.Update();
    }

}
