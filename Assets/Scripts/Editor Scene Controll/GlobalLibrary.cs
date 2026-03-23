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
        {SceneTitle.TitleScene, new SceneSpecifics()
        {


        }},
        {SceneTitle.GYM_Level, new SceneSpecifics()
        {


        }},
        {SceneTitle.InitialCG, new SceneSpecifics()
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
        {SceneTitle.Level2_2, new SceneSpecifics()
        {


        }},
        {SceneTitle.Level3_1, new SceneSpecifics()
        {


        }},
        {SceneTitle.Level3_2, new SceneSpecifics()
        {


        }},
        {SceneTitle.Level3_3, new SceneSpecifics()
        {


        }},
        {SceneTitle.Level3_4, new SceneSpecifics()
        {


        }},
        {SceneTitle.Level3_5, new SceneSpecifics()
        {


        }},
        {SceneTitle.Level3_6, new SceneSpecifics()
        {


        }},
        {SceneTitle.Level4_1City, new SceneSpecifics()
        {


        }},
        {SceneTitle.Level4_2City, new SceneSpecifics()
        {


        }},
        {SceneTitle.Level4_2Festival, new SceneSpecifics()
        {


        }},
        {SceneTitle.Level4_3Apartment1F, new SceneSpecifics()
        {


        }},
        {SceneTitle.Level4_4Apartment2F, new SceneSpecifics()
        {


        }},
        {SceneTitle.Level4_5Apartment3F, new SceneSpecifics()
        {


        }},
        {SceneTitle.Level4_6NoemaHouse, new SceneSpecifics()
        {


        }},
        {SceneTitle.Level4_17Stage, new SceneSpecifics()
        {


        }},
        {SceneTitle.Level4_8Temple, new SceneSpecifics()
        {


        }},
        {SceneTitle.Level4_6_1Ghost3F, new SceneSpecifics()
        {


        }},
        {SceneTitle.Level4_6_2LoopStuck1, new SceneSpecifics()
        {


        }},
        {SceneTitle.Level4_6_3LoopDead, new SceneSpecifics()
        {


        }},
        {SceneTitle.Level4_6_4LoopStuck2, new SceneSpecifics()
        {


        }},
        {SceneTitle.Level4_6_5LoopStuck3, new SceneSpecifics()
        {


        }},
        {SceneTitle.Level4_6_6FinalChase, new SceneSpecifics()
        {


        }},
        {SceneTitle.Level4_6_7City, new SceneSpecifics()
        {


        }},
        {SceneTitle.Level4_7FestivalNoema, new SceneSpecifics()
        {


        }},
        {SceneTitle.Level4_8FestivalGhost, new SceneSpecifics()
        {


        }},
        {SceneTitle.Level4_81FestivalWrong, new SceneSpecifics()
        {


        }},
        {SceneTitle.Level4_9FestivalDead, new SceneSpecifics()
        {


        }},
        {SceneTitle.Level4_10FestivalNoemaHelp, new SceneSpecifics()
        {


        }},
        {SceneTitle.Level4_11FestivalPuzzle1, new SceneSpecifics()
        {


        }},
        {SceneTitle.Level4_12FestivalPuzzle2, new SceneSpecifics()
        {


        }},
        {SceneTitle.Level4_13FestivalPuzzle3, new SceneSpecifics()
        {


        }},
        {SceneTitle.Level4_14FestivalPuzzle4, new SceneSpecifics()
        {


        }},
        {SceneTitle.Level4_15FestivalPuzzle5, new SceneSpecifics()
        {


        }},
        {SceneTitle.Level4_151FestivalPuzzle6, new SceneSpecifics()
        {


        }},
        {SceneTitle.Level4_16FestivalKill, new SceneSpecifics()
        {


        }},
        {SceneTitle.Level4_18MusicGame, new SceneSpecifics()
        {


        }},
        {SceneTitle.Level4_19StageFail, new SceneSpecifics()
        {


        }},
        {SceneTitle.Level4_20StageSuccess, new SceneSpecifics()
        {


        }},
    };


    public static readonly string G_SCENE_LOADING_ASSET_NAME = "Loading";
    public static readonly string G_SCENE_PREPARE_ASSET_NAME = "Initiate";
    //Add level to here to load different levels

    public static readonly Dictionary<SceneCategory, SceneTitle[]> G_SCENE_CATEGORY_DICT = new Dictionary<SceneCategory, SceneTitle[]>
    {
        {SceneCategory.Common, new SceneTitle[]{ SceneTitle.TitleScene, SceneTitle.GYM_Level, SceneTitle.InitialCG, SceneTitle.Level1_1, SceneTitle.Level1_2 , SceneTitle.Level2_1 , SceneTitle.Level2_2
            , SceneTitle.Level3_1, SceneTitle.Level3_2, SceneTitle.Level3_3, SceneTitle.Level3_4, SceneTitle.Level3_5, SceneTitle.Level3_6,
            SceneTitle.Level4_1City,SceneTitle.Level4_2City,SceneTitle.Level4_2Festival,SceneTitle.Level4_3Apartment1F, SceneTitle.Level4_4Apartment2F,SceneTitle.Level4_5Apartment3F,SceneTitle.Level4_6NoemaHouse,SceneTitle.Level4_17Stage,SceneTitle.Level4_8Temple,
            SceneTitle.Level4_6_1Ghost3F, SceneTitle.Level4_6_2LoopStuck1, SceneTitle.Level4_6_3LoopDead, SceneTitle.Level4_6_4LoopStuck2, SceneTitle.Level4_6_5LoopStuck3,SceneTitle.Level4_6_6FinalChase,SceneTitle.Level4_6_7City,
            SceneTitle.Level4_7FestivalNoema,  SceneTitle.Level4_8FestivalGhost, SceneTitle.Level4_81FestivalWrong,SceneTitle.Level4_9FestivalDead,SceneTitle.Level4_10FestivalNoemaHelp,
            SceneTitle.Level4_11FestivalPuzzle1, SceneTitle.Level4_12FestivalPuzzle2, SceneTitle.Level4_13FestivalPuzzle3, SceneTitle.Level4_14FestivalPuzzle4,SceneTitle.Level4_15FestivalPuzzle5,SceneTitle.Level4_151FestivalPuzzle6,SceneTitle.Level4_16FestivalKill,
            SceneTitle.Level4_18MusicGame,SceneTitle.Level4_19StageFail,SceneTitle.Level4_20StageSuccess,


        } },
                            
    };

    public static readonly Dictionary<SceneTitle, string> G_SCENE_ASSET_NAME = new Dictionary<SceneTitle, string>()
    {
        //Common    
        {SceneTitle.TitleScene, "TitleScene" },
        {SceneTitle.GYM_Level, "GYM_Level" },
        {SceneTitle.InitialCG, "InitialCGScene" },
        {SceneTitle.Level1_1, "Level1-1" },
        {SceneTitle.Level1_2, "Level1-2" },
        {SceneTitle.Level2_1, "Level2-1" },
        {SceneTitle.Level2_2, "Level2-2" },
        {SceneTitle.Level3_1, "Level3-1" },
        {SceneTitle.Level3_2, "Level3-2" },
        {SceneTitle.Level3_3, "Level3-3" },
        {SceneTitle.Level3_4, "Level3-4" },
        {SceneTitle.Level3_5, "Level3-5" },
        {SceneTitle.Level3_6, "Level3-6" },
        {SceneTitle.Level4_1City, "Level4-1City" },
        {SceneTitle.Level4_2City, "Level4-2City" },
        {SceneTitle.Level4_2Festival, "Level4-2Festival" },
        {SceneTitle.Level4_3Apartment1F, "Level4-3Apartment1F" },
        {SceneTitle.Level4_4Apartment2F, "Level4-4Apartment2F" },
        {SceneTitle.Level4_5Apartment3F, "Level4-5Apartment3F" },
        {SceneTitle.Level4_6NoemaHouse, "Level4-6NoemaHouse" },
        {SceneTitle.Level4_17Stage, "Level4-17Stage" },
        {SceneTitle.Level4_8Temple, "Level4-8Temple" },

        {SceneTitle.Level4_6_1Ghost3F, "Level4-6-1Ghost3F" },
        {SceneTitle.Level4_6_2LoopStuck1, "Level4-6-2LoopStuck1" },
        {SceneTitle.Level4_6_3LoopDead, "Level4-6-3LoopDead" },
        {SceneTitle.Level4_6_4LoopStuck2, "Level4-6-4LoopStuck2" },
        {SceneTitle.Level4_6_5LoopStuck3, "Level4-6-5LoopStuck3" },
        {SceneTitle.Level4_6_6FinalChase, "Level4-6-6FinalChase" },
        {SceneTitle.Level4_6_7City, "Level4-6-7City" },
        {SceneTitle.Level4_7FestivalNoema, "Level4-7FestivalNoema" },
        {SceneTitle.Level4_8FestivalGhost, "Level4-8FestivalGhost" },
        {SceneTitle.Level4_81FestivalWrong, "Level4-81FestivalWrong" },
        {SceneTitle.Level4_9FestivalDead, "Level4-9FestivalDead" },
        {SceneTitle.Level4_10FestivalNoemaHelp, "Level4-10FestivalNoemaHelp" },
        {SceneTitle.Level4_11FestivalPuzzle1, "Level4-11FestivalPuzzle1" },
        {SceneTitle.Level4_12FestivalPuzzle2, "Level4-12FestivalPuzzle2" },
        {SceneTitle.Level4_13FestivalPuzzle3, "Level4-13FestivalPuzzle3" },
        {SceneTitle.Level4_14FestivalPuzzle4, "Level4-14FestivalPuzzle4" },
        {SceneTitle.Level4_15FestivalPuzzle5, "Level4-15FestivalPuzzle5" },
        {SceneTitle.Level4_151FestivalPuzzle6, "Level4-151FestivalPuzzle6" },
        {SceneTitle.Level4_16FestivalKill, "Level4-16FestivalKill" },
        {SceneTitle.Level4_18MusicGame, "Level4_18MusicGame" },
        {SceneTitle.Level4_19StageFail, "Level4_19StageFail" },
        {SceneTitle.Level4_20StageSuccess, "Level4_20StageSuccess" },
    };

    public static readonly Dictionary<int, SceneTitle> G_SCENE_INDEX = new Dictionary<int, SceneTitle>()
    {

    };


    public static readonly float G_SCENE_PARALLEX_INTENSITY = 0.5f;
    #endregion
}