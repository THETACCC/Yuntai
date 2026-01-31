using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class isThisUnlocked : MonoBehaviour
{
    public bool isThisThingUnlocked = false;

    public void UnlockThis()
    {
        isThisThingUnlocked = true;
        //SaveManager.instance.SaveGame();
    }

    public void LockThis()
    {
        isThisThingUnlocked = false;
        //SaveManager.instance.SaveGame();
    }

}
