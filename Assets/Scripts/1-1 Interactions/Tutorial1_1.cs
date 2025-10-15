using MoreMountains.Feedbacks;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Tutorial1_1 : MonoBehaviour
{
    [Header("Feedback Reference")]
    public MMFeedbacks A_Indicator;
    public MMFeedbacks A_Indicator2;
    public MMFeedbacks D_Indicator;
    public MMFeedbacks D_Indicator2;
    [Header("Settings")]
    public float requiredTime = 3f;  // Total A press/hold time required

    private float aKeyTimer = 0f;    // Accumulated time while A is pressed/held
    private bool tutorialHidden = false;

    void Update()
    {
        // If player is pressing or holding down A
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.D))
        {
            aKeyTimer += Time.deltaTime;
        }

        // When timer exceeds the threshold, trigger once
        if (aKeyTimer >= requiredTime && !tutorialHidden)
        {
            HideTutorialIndicatorA();
            tutorialHidden = true;
        }
    }

    public void ShowTutorialIndicator()
    {
        A_Indicator?.PlayFeedbacks();
        A_Indicator?.PauseFeedbacks();
        A_Indicator2?.PlayFeedbacks();
        A_Indicator2?.PauseFeedbacks();
        D_Indicator?.PlayFeedbacks();
        D_Indicator?.PauseFeedbacks();
        D_Indicator2?.PlayFeedbacks();
        D_Indicator2?.PauseFeedbacks();
    }

    public void HideTutorialIndicatorD()
    {
        A_Indicator?.ResumeFeedbacks();
        Debug.Log("Tutorial indicator hidden after 3 seconds of A input.");
    }
    public void HideTutorialIndicatorA()
    {
        A_Indicator?.ResumeFeedbacks();
        A_Indicator2?.ResumeFeedbacks();
        D_Indicator?.ResumeFeedbacks();
        D_Indicator2?.ResumeFeedbacks();

        Debug.Log("Tutorial indicator hidden after 3 seconds of A input.");
    }
}