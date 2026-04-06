using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DoorBlockController : MonoBehaviour
{

    public GameObject myDoor;
    public GameObject myIndicator;
    public GameObject myDoorClose;

    private bool isTrigger = false;

    // Start is called before the first frame update
    void Start()
    {
        myDoorClose.SetActive(true);
        myDoor.SetActive(false);
        myIndicator.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void OnTriggerEnter2D(Collider2D collision)
    {
        if(!isTrigger)
        {
            if (collision.gameObject.tag == "Player")
            {
                myDoorClose.SetActive(false);
                myDoor.SetActive(true);
                myIndicator.SetActive(true);
                isTrigger = true;
            }

        }


    }

}
