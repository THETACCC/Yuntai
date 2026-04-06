using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RedNodeManager : MonoBehaviour
{
    public static RedNodeManager instance;
    [Header("Tab RedNode Reference")]
    public RedNode taskRedNode;
    public RedNode eventRedNode;
    public RedNode characterRedNode;

    private void Awake()
    {
        instance = this;
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
