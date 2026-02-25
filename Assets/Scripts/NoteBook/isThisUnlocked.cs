using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class isThisUnlocked : MonoBehaviour
{
    public bool isThisThingUnlocked = false;
    RedNode redNode;

    private void Awake()
    {
        redNode = GetComponentInChildren<RedNode>();
    }

    public void UnlockThis()
    {
        isThisThingUnlocked = true;
        redNode.Enable();
        //SaveManager.instance.SaveGame();
    }

    public void LockThis()
    {
        isThisThingUnlocked = false;
        //SaveManager.instance.SaveGame();
    }

}
