using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LevelManager4_2_City : BaseLevelManager
{
    [SerializeField] private GameObject door;

    public void SetOpenDoorOpen()
    {
        door.SetActive(true);
    }
}
