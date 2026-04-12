using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

public class LevelManager4_2_City : BaseLevelManager
{
    [SerializeField] private GameObject door;
    [Header("ZhouShu 失败时相关的物体")]
    [SerializeField] private List<GameObject> ApartmentFailObjects = new();
    [SerializeField] private GameObject BadScenePortal;
    public void SetOpenDoorOpen()
    {
        door.SetActive(true);
    }


    public void ApartmentFailed()
    {
        foreach (var go in ApartmentFailObjects)
        {
            if (go != null && go.activeSelf)
                go.SetActive(false);
        }

        if (BadScenePortal != null)
            BadScenePortal.SetActive(true);

    }
}
