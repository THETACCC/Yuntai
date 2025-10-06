using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class GlobalLibrary
{
    #region SceneControl
    public static readonly string G_SCENE_TAG_SPAWNPOINT = "SpawnPoint";
    public static readonly string G_SCENE_TAG_CHECKPOINT = "CheckPoint";
    public static readonly string G_SCENE_ZOOMIN_PRID = "SceneZoomIn";


    public static readonly Dictionary<SceneTitle, SceneSpecifics> G_SCENE_SPECIFICS = new Dictionary<SceneTitle, SceneSpecifics>()
    {
        {SceneTitle.GYM_Level, new SceneSpecifics()
        {


        }},
        {SceneTitle.Level1_1, new SceneSpecifics()
        {


        }},
        {SceneTitle.Level1_2, new SceneSpecifics()
        {


        }},
        {SceneTitle.Level2_1, new SceneSpecifics()
        {


        }},
    };


    public static readonly string G_SCENE_LOADING_ASSET_NAME = "Loading";
    public static readonly string G_SCENE_PREPARE_ASSET_NAME = "Initiate";
    //Add level to here to load different levels

    public static readonly Dictionary<SceneCategory, SceneTitle[]> G_SCENE_CATEGORY_DICT = new Dictionary<SceneCategory, SceneTitle[]>
    {
        {SceneCategory.Common, new SceneTitle[]{ SceneTitle.GYM_Level, SceneTitle.Level1_1, SceneTitle.Level1_2 , SceneTitle.Level2_1 } },

    };

    public static readonly Dictionary<SceneTitle, string> G_SCENE_ASSET_NAME = new Dictionary<SceneTitle, string>()
    {
        //Common    
        {SceneTitle.GYM_Level, "GYM_Level" },
        {SceneTitle.Level1_1, "Level1-1" },
        {SceneTitle.Level1_2, "Level1-2" },
        {SceneTitle.Level2_1, "Level2-1" },
    };

    public static readonly Dictionary<int, SceneTitle> G_SCENE_INDEX = new Dictionary<int, SceneTitle>()
    {

    };


    public static readonly float G_SCENE_PARALLEX_INTENSITY = 0.5f;
    #endregion
}