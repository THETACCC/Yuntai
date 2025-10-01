using SKCell;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RuntimeData : SKMonoSingleton<RuntimeData>
{
    #region Scene
    public static bool isSceneLoading;
    public static SceneTitle activeSceneTitle;

    #endregion
}
