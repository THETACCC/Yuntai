using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting.FullSerializer;
using UnityEngine;
using UnityEngine.Experimental.GlobalIllumination;

public class TweeningCurve
{
    public DialogueCurveType DialogueCurveType;
    public Func<float, float> tweenFunc;

    public TweeningCurve(DialogueCurveType DialogueCurveType)
    {
        this.DialogueCurveType = DialogueCurveType;
    }

    public TweeningCurve(Func<float, float> tweenFunc)
    {
        this.tweenFunc = tweenFunc;
    }

    public static TweeningCurve Linear { get { return new TweeningCurve(DialogueCurveType.Linear); } }
    public static TweeningCurve Quadratic { get { return new TweeningCurve(DialogueCurveType.Quadratic); } }
    public static TweeningCurve Cubic { get { return new TweeningCurve(DialogueCurveType.Cubic); } }
    public static TweeningCurve Quartic { get { return new TweeningCurve(DialogueCurveType.Quartic); } }
    public static TweeningCurve Quintic { get { return new TweeningCurve(DialogueCurveType.Quintic); } }
    public static TweeningCurve Bounce { get { return new TweeningCurve(DialogueCurveType.Bounce); } }
}

