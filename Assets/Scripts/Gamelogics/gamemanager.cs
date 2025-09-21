using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Gamemanager : MonoBehaviour
{
    public static Gamemanager instance;
    //objects don;t destroy
    public GameObject virtualplayer;
    public GameObject playermanager;
    public GameObject avatarplayer;
    public GameObject m_camera;

    public GamePhase phase;
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
        DontDestroyOnLoad(virtualplayer);
        DontDestroyOnLoad(avatarplayer);
        DontDestroyOnLoad(m_camera);
        DontDestroyOnLoad(playermanager);
    }


}

public enum GamePhase
{
    Loading,
    Moving,
    Talking,
    Eventing
}
