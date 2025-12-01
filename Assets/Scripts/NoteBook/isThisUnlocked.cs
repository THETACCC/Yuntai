using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class isThisUnlocked : MonoBehaviour
{
    public bool isThisThingUnlocked = false;

    public void UnlockThis()
    {
        isThisThingUnlocked = true;
    }

    public void LockThis()
    {
        isThisThingUnlocked = false;
    }

}
