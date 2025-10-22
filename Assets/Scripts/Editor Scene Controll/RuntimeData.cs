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

    #region Camera
    public static Transform camera_Transform;
    public static Vector3 camera_PositionDelta;
    private static Vector3 camera_LastPos;
    #endregion


}
