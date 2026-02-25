using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using static AudioManager;

public class DisplayDetail : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Tooltip("The detail panel or tooltip to show when hovered.")]
    public GameObject myDetail;

    private void Start()
    {
        if (myDetail != null)
            myDetail.SetActive(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {

        //Audio
        AudioManager.PlayOneShot("Sound Effects/Chapter1/sndPuzzleHover", AudioGroup.SFX);
        if (myDetail != null)
            myDetail.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (myDetail != null)
            myDetail.SetActive(false);
    }

    public void playSelectSound()
    {
        AudioManager.PlayOneShot("Sound Effects/Henk/sndChoice", AudioGroup.SFX);
    }

}
