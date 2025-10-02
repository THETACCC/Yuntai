using SKCell;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FlowManager : SKMonoSingleton<FlowManager>
{
    public static SceneTitle scenetitle;

    private void Start()
    {
        scenetitle = (SceneTitle)PlayerPrefs.GetInt("StartScene");

        SKUtils.InvokeAction(0.2f, () =>
        {
            LoadScene(new SceneInfo()
            {
                index = scenetitle,
            });
        });
    }

    public void LoadScene(SceneInfo info)
    {
        Scenecontroller.instance.LoadSceneAsset(info);

    }

}
