using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NoteBookManager : MonoBehaviour
{
    public GameObject NoteBook_Canvas;

    private bool isOpen = false;

    [Header("Character TAB")]
    public GameObject Character1;

    [Header("Event Tab")]
    public GameObject Event1;

    [Header("Character1 INFO Tab")]
    public GameObject Character1_INFO1;

    [Header("Event1 INFO Tab")]
    public GameObject Event1_INFO1;


    void Start()
    {
        NoteBook_Canvas.SetActive(false);
        isOpen = false;
    }

    // Update is called once per frame
    void Update()
    {
        if(Input.GetKeyDown(KeyCode.Tab))
        {
            if(!isOpen)
            {
                NoteBook_Canvas.SetActive(true);
                isOpen = true;
            }
            else
            {
                NoteBook_Canvas.SetActive(false);
                isOpen = false;
            }
        }
    }
}
