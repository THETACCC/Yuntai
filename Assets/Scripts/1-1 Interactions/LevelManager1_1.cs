using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LevelManager1_1 : MonoBehaviour
{
    [SerializeField] private int myLoop = 1;
    [SerializeField] private AudioSource snd_start;
    // Start is called before the first frame update
    void Start()
    {
        LoopTracker.I?.SetLoop(myLoop);
    }

    public void playSndStart()
    {
        snd_start.Play();
    }
}
