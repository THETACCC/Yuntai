using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NoteBookManager : MonoBehaviour
{
    public GameObject NoteBook_Canvas;

    private bool isOpen = false;


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
