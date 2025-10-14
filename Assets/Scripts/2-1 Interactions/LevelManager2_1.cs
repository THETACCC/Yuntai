using JetBrains.Annotations;
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
    [HideInInspector] public bool infoFour = false; //乘务员为什么站在我座椅边上。

    [HideInInspector] public bool getFood = false; //找乘务员拿到food
    [HideInInspector] public bool gotFood = false; //zhoushu拿到food

    //Playthrough Related
    [Header("Playthrough Related")]
    public GameObject Stewardess;
    public GameObject Stewardess_Food;

    public GameObject ZhouShu;
    public GameObject ZhouShu_GotFood;

    //Conversation Related
    public bool isStewardess_Conv1 = false;
    public bool isStewardess_Conv2 = false;
    public bool isStewardess_Conv3 = false;
    public bool isStewardess_Conv4 = false;
    public bool isStewardess_AllConv = false;
    public bool isStewardessSet = false;
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

    // 全部 info 是否已收集
    private bool HaveAllInfo() => infoOne && infoTwo && infoThree && infoFour;

    // 当前是否还有“可对话但未完成”的项
    private bool AnyPendingConversation() =>
        (infoOne && !isStewardess_Conv1) ||
        (infoTwo && !isStewardess_Conv2) ||
        (infoThree && !isStewardess_Conv3) ||
        (infoFour && !isStewardess_Conv4);


    public void AllStewardessConversationCheck()
    {
        bool nonePending = !AnyPendingConversation();
        isStewardess_AllConv = nonePending;
        isStewardessSet = nonePending && HaveAllInfo();
    }


    public void SetInfoOneTrue()
    {
        if (!infoOne)
        {
            infoOne = true;
            isStewardess_AllConv = false;  
            isStewardessSet = false;       
        }
        AllStewardessConversationCheck();  
    }

    public void SetInfoTwoTrue()
    {
        if (!infoTwo)
        {
            infoTwo = true;
            isStewardess_AllConv = false;
            isStewardessSet = false;
        }
        AllStewardessConversationCheck();
    }

    public void SetInfoThreeTrue()
    {
        if (!infoThree)
        {
            infoThree = true;
            isStewardess_AllConv = false;
            isStewardessSet = false;
        }
        AllStewardessConversationCheck();
    }

    public void SetInfoFourTrue()
    {
        if (!infoFour)
        {
            infoFour = true;
            isStewardess_AllConv = false;
            isStewardessSet = false;
        }
        AllStewardessConversationCheck();
    }


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

    public void SetStewardessConv1() { 
        isStewardess_Conv1 = true;
        AllStewardessConversationCheck();
    }
    public void SetStewardessConv2() { 
        isStewardess_Conv2 = true;
        AllStewardessConversationCheck();
    }
    public void SetStewardessConv3() { 
        isStewardess_Conv3 = true;
        AllStewardessConversationCheck();
    }
    public void SetStewardessConv4() { 
        isStewardess_Conv4 = true;
        AllStewardessConversationCheck();
    }

    public void ChangeStewardessToForFood()
    {
        Stewardess.gameObject.SetActive(false);
        Stewardess_Food.gameObject.SetActive(true);
}
}
