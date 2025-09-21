using Cinemachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class changebound : MonoBehaviour
{

    public GameObject boundingarea_1;
    public GameObject boundingarea_2;
    public GameObject virtualcamera;
    public GameObject InteractIndicator;



    private CinemachineConfiner2D confiner;
    private PolygonCollider2D bound_1;
    private PolygonCollider2D bound_2;

    private bool isInTrigger = false;

    void Start()
    {
        virtualcamera = GameObject.FindGameObjectWithTag("VirtualCam");

        bound_1 = boundingarea_1.GetComponent<PolygonCollider2D>();
        bound_2 = boundingarea_2.GetComponent<PolygonCollider2D>();
        confiner = virtualcamera.GetComponent<CinemachineConfiner2D>();
        InteractIndicator.SetActive(false);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        isInTrigger = true;
        InteractIndicator.SetActive(true);
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        isInTrigger = false;
        InteractIndicator.SetActive(false);
    }

    private void Update()
    {
        if (isInTrigger && Input.GetKeyDown(KeyCode.E))
        {
            Debug.Log("pressed");
            SwitchBoundingArea();
        }
    }

    private void SwitchBoundingArea()
    {
        if (confiner.m_BoundingShape2D == bound_1)
        {
            confiner.m_BoundingShape2D = bound_2;
        }
        else
        {
            confiner.m_BoundingShape2D = bound_1;
        }
    }

}
