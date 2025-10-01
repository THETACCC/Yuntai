using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct SceneSpecifics
{
    public string sceneName;
    public int sceneNameLocalID;
    public float cameraSize;
    public SceneWeather weather;
    public int postprocessingProfile;
    public string postprocessingProfileAssetName;

    public bool disableCameraDeadzone, enableSmoothOpening;
    public float smoothOpeningDelay;
    public bool disableHUD, disableUISceneLabel, disablePlayerGravity, disablePlayerDash, disablePlayerAttack;

    public string[] bgms; //file name of bgms
}

[Serializable]
public struct SceneWeather
{
    public bool activeOnStart;
    public WeatherType type;
    public float interval;
    public float probability;
}

public enum WeatherType
{
    None,
    Rain,
    Snow
}
