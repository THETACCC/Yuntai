using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BlackScreenEffect : MonoBehaviour
{
    [SerializeField] private Animator transitionAnim;
    public void setBlackScreenTrigger()
    {
        // 1) ºÚÆÁ
        if (transitionAnim != null)
        {
           transitionAnim.SetTrigger("End");
        }
    }
}
