using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using URPLight2D = UnityEngine.Rendering.Universal.Light2D;

public class LevelManager3_3 : MonoBehaviour
{
    [Header("Lantern Stall")]
    [SerializeField] private GameObject LanternStall;
    [SerializeField] private GameObject LanternStall_wait;
    [SerializeField] private GameObject LanternStall_wrong;
    [SerializeField] private GameObject LanternStall_correct;

    [SerializeField] private GameObject LanternPile;

    [SerializeField] private PuzzleLanternManager puzzleLanternManager;

    private void Start()
    {

    }

    public void ChangeLanternToWait()
    {
        LanternStall.SetActive(false);
        LanternStall_wait.SetActive(true);
    }
    public void CheckIfLanternCorrect()
    {
        if (puzzleLanternManager.Solved)
        {
            print("Lantern puzzle solved!");
            LanternStall.SetActive(false);
            LanternStall_wait.SetActive(false);
            LanternStall_wrong.SetActive(false);
            LanternStall_correct.SetActive(true);
        }
        else
        {
            print("Lantern puzzle solved!");
            LanternStall.SetActive(false);
            LanternStall_wait.SetActive(false);
            LanternStall_correct.SetActive(false);
            LanternStall_wrong.SetActive(true);
        }

    }

    public void LanternPileDisappear()
    {
        LanternPile.SetActive(false);
    }
}
