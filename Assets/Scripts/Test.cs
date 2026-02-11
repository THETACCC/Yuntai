using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Test : MonoBehaviour
{
    public int number = 0;
    public bool isTrue = false;

    public string clipName;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.C))
        {
            AudioManager.Play(clipName);
        }

        if (Input.GetKeyDown(KeyCode.W))
        {
            AudioManager.SetVolume(clipName, 1f);
        }

        if (Input.GetKeyDown(KeyCode.S))
        {
            AudioManager.SetVolume(clipName, 0.5f);
        }
    }

    public void CallAfterNode()
    {
        Debug.Log("CallAfterNode");
    }

    public void CallDuringNode()
    {
        Debug.Log("CallDuringNode");
    }

    public void CallAfterNodeDisappear(int i)
    {
        Debug.Log("CallAfterNodeDisappear " + i);
    }
}
