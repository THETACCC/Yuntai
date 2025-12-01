using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SoundMiniGameManager : MonoBehaviour
{
    [Header("Assign the 5 sliders (each 0¨C4)")]
    public Slider[] sliders = new Slider[5];

    // The correct puzzle answer
    private readonly int[] answer = new int[] { 4, 1, 2, 0, 4 };

    [Header("Feedback Image")]
    public Image myPuzzleSolveImage;

    [Header("Feedback Settings")]
    public float blinkDuration = 0.15f;
    public int blinkTimes = 3;

    private Color originalColor;
    private Coroutine feedbackRoutine;

    void Start()
    {
        // Store original color
        if (myPuzzleSolveImage != null)
            originalColor = myPuzzleSolveImage.color;

        // Ensure sliders snap to whole numbers 0¨C4
        foreach (var s in sliders)
        {
            if (s != null)
            {
                s.wholeNumbers = true;
                s.minValue = 0;
                s.maxValue = 4;
            }
        }
    }

    public int[] GetSliderValues()
    {
        int[] values = new int[5];

        for (int i = 0; i < sliders.Length; i++)
        {
            if (sliders[i] != null)
                values[i] = Mathf.RoundToInt(sliders[i].value);
        }

        return values;
    }

    public bool IsCorrect()
    {
        int[] current = GetSliderValues();

        for (int i = 0; i < 5; i++)
        {
            if (current[i] != answer[i])
                return false;
        }

        return true;
    }

    public void OnCheckAnswerButtonPressed()
    {
        bool correct = IsCorrect();
        Debug.Log(correct ? "Correct Answer!" : "Wrong Answer.");

        if (myPuzzleSolveImage == null)
            return;

        if (feedbackRoutine != null)
            StopCoroutine(feedbackRoutine);

        feedbackRoutine = StartCoroutine(BlinkFeedback(correct));
    }

    private IEnumerator BlinkFeedback(bool isCorrect)
    {
        Color targetColor = isCorrect ? Color.green : Color.red;

        for (int i = 0; i < blinkTimes; i++)
        {
            myPuzzleSolveImage.color = targetColor;
            yield return new WaitForSeconds(blinkDuration);

            myPuzzleSolveImage.color = originalColor;
            yield return new WaitForSeconds(blinkDuration);
        }

        myPuzzleSolveImage.color = originalColor;
        feedbackRoutine = null;
    }

    [ContextMenu("Test Puzzle")]
    void TestPuzzle()
    {
        Debug.Log(IsCorrect() ? "Correct Answer!" : "Wrong Answer.");
    }
}
