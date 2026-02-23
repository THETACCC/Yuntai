using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static AudioManager;

public class LevelManager1_1 : BaseLevelManager
{
    [SerializeField] private int myLoop = 1;

    private void Start()
    {
        LoopTracker.I?.SetLoop(myLoop);
    }

    public void PlayStandUpSound()
    {
        AudioManager.Play("Sound Effects/Chapter1/sndStandUp", AudioGroup.SFX);
        AudioManager.SetVolume("Sound Effects/Chapter1/sndStandUp", 0.4f);
    }

}
