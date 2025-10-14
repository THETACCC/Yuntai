using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LevelManager2_1 : MonoBehaviour
{
    public bool isZhouShu = false;


    [SerializeField] private int myLoop = 2;


    public bool isPlayerEscaped = false;

    //Scene Related

    public string SceneName_NotEscaped;
    public int SpawnPointLocation_NotEscaped;

    public string SceneName_Escaped;
    public int SpawnPointLocation_Escaped;

    public ScenePortal BathroomPortal;

    // Diaogue Boolean
    [HideInInspector] public bool infoOne = false; //PassA 芸台即将封城，而且此班飞机是最后一班的消息
    [HideInInspector] public bool infoTwo = false; //上班族 芸台近日不是很太平，有很多人目击到了奇怪的外地团体在城中活动
    [HideInInspector] public bool infoThree = false; //ZhouShu 负责发飞机餐的乘务员不知为什么一直不过来。

    [HideInInspector] public bool getFood = false; //找乘务员
    [HideInInspector] public bool gotFood = false; //找乘务员

    //Playthrough Related
    [Header("Playthrough Related")]
    public GameObject Stewardess;
    public GameObject Stewardess_Food;

    public GameObject ZhouShu;
    public GameObject ZhouShu_GotFood;


    public void SpeakZhoushu()
    {
        isZhouShu = true;
        BathroomPortal.scenename = SceneName_Escaped;
        BathroomPortal.SpawnPointLocation = SpawnPointLocation_Escaped;
    }

    public void Start()
    {
        LoopTracker.I?.SetLoop(myLoop);
        BathroomPortal.scenename = SceneName_NotEscaped;
        BathroomPortal.SpawnPointLocation = SpawnPointLocation_NotEscaped;
    }

    public void Update()
    {

    }

    public void SetInfoOneTrue() { infoOne = true; }
    public void SetInfoTwoTrue() { infoTwo = true; }
    public void SetInfoThreeTrue() { infoThree = true; }
    public void SetgetFoodTrue() 
    { 
        getFood = true;
        Stewardess.SetActive(false);
        Stewardess_Food.SetActive(true);
    }

    public void SetGotFoodTrue() 
    { 
        
        gotFood = true;
        ZhouShu.SetActive(false);
        ZhouShu_GotFood.SetActive(true);

    }
}
