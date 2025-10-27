using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Test : MonoBehaviour
{
    public int number = 0;
    public bool isTrue = false;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void CallAfterNode()
    {
        Debug.Log("CallAfterNode");
    }

    public void CallDuringNode()
    {
        Debug.Log("CallDuringNode");
    }

    public void CallDuringNode(int i)
    {
        Debug.Log("CallDuringNode " + i);
    }
}
