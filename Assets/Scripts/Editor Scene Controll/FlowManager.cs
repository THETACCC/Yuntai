using SKCell;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FlowManager : SKMonoSingleton<FlowManager>
{
    public static SceneTitle scenetitle;
    public static int t_spawnPoint;
    private void Start()
    {
        scenetitle = (SceneTitle)PlayerPrefs.GetInt("StartScene");
        t_spawnPoint = 0;
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
