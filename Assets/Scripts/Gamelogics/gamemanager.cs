using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class gamemanager : MonoBehaviour
{
    //objects don;t destroy
    public GameObject virtualplayer;
    public GameObject playermanager;
    public GameObject avatarplayer;
    public GameObject m_camera;



    private void Awake()
    {
        DontDestroyOnLoad(this);
        DontDestroyOnLoad(virtualplayer);
        DontDestroyOnLoad(avatarplayer);
        DontDestroyOnLoad(m_camera);
        DontDestroyOnLoad(playermanager);
    }


}
