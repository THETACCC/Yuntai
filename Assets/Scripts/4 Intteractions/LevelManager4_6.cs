using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LevelManager4_6 : MonoBehaviour
{
    [SerializeField] private GameObject door;
    [SerializeField] private GameObject doorBefore;
    [SerializeField] private GameObject Gift;
    [SerializeField] private GameObject GiftVisual;

    public void SetOpenDoorOpen()
    {
        doorBefore.SetActive(false);
        door.SetActive(true);
        Gift.SetActive(false);
        GiftVisual.SetActive(false);
    }
}
