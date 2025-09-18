using Cinemachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Playermanager : MonoBehaviour
{
    public bool transfercontroll = false;

    //controll the virtual player

    public GameObject virtualplayer;

    //controll the player
    public GameObject avatarplayer;
    public GameObject avatarfollow;

    //controll the camera
    public GameObject cam;
    public CinemachineVirtualCamera virtualcam;


    //information about the bounding box
    public GameObject virtualcamera;
    private CinemachineConfiner2D confiner;
    private Collider2D avatarBoundingArea;

    public GameObject virtualbound;
    private PolygonCollider2D thevirtualbound;
    private void Start()
    {
        thevirtualbound = virtualbound.GetComponent<PolygonCollider2D>();
        confiner = cam.GetComponent<CinemachineConfiner2D>();
        //virtualplayer = GetComponent<GameObject>(); 
        virtualcam = cam.GetComponent<CinemachineVirtualCamera>();
    }


    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Y) && !transfercontroll)
        {
            avatarBoundingArea = confiner.m_BoundingShape2D;
            transfercontroll = true;
            confiner.m_BoundingShape2D = thevirtualbound;
        }
        else if (Input.GetKeyDown(KeyCode.Y) && transfercontroll)
        {
            transfercontroll = false;
            confiner.m_BoundingShape2D = avatarBoundingArea;
        }

        //this is where controll transfer between player happens
        controlltransfer();
    }

    public void controlltransfer()
    {
        if (transfercontroll)
        {
            virtualcam.Follow = virtualplayer.transform;
            avatarplayer.gameObject.SetActive(false);
            virtualplayer.gameObject.SetActive(true);
        }
        else 
        {
            //virtualplayer.transform = avatarplayer.transform;
            virtualcam.Follow = avatarfollow.transform;
            avatarplayer.gameObject.SetActive(true);
            virtualplayer.gameObject.SetActive(false);
        }

    }
}
