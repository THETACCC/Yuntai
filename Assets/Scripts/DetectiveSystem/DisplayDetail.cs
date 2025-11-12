using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

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
        if (myDetail != null)
            myDetail.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (myDetail != null)
            myDetail.SetActive(false);
    }
}
