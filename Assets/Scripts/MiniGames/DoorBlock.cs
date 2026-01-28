using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DoorBlock : MonoBehaviour
{
    public bool isReadyToTrigger = true; // 是否可以触发
    public GameObject myDoorBlock;

    [Header("Interaction Settings")]
    public int requiredPresses = 3;

    private int currentPressCount = 0;

    void Update()
    {
        if (!isReadyToTrigger) return;

        if (Input.GetKeyDown(KeyCode.E))
        {
            currentPressCount++;

            if (currentPressCount >= requiredPresses)
            {
                DisableDoorBlock();
            }
        }
    }

    public void DisableDoorBlock()
    {
        myDoorBlock.SetActive(false);
        isReadyToTrigger = false; // 防止再次触发
    }
}
