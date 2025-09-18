using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Fungus;
using UnityEngine.UIElements;

public class NPC1 : MonoBehaviour
{
    public Fungus.Flowchart myFlowchart;

    private bool player_staying = false;

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        if (player_staying)
        {
            bool isdialogue = GameObject.FindGameObjectWithTag("Dialogues");
            if (Input.GetKeyDown(KeyCode.E) && player_staying && !isdialogue)
            {
                myFlowchart.ExecuteBlock("New Block");
            }

        }


 
    }



    private void OnTriggerEnter2D(Collider2D collision)
    {
        player_staying = true;
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        player_staying = false;
    }

}
