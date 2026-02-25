using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RedNode : MonoBehaviour
{
    [HideInInspector]public RedNode parent;
    public List<RedNode> children = new List<RedNode>();
    public bool enable = false;
    [SerializeField]NodeType nodeType;

    Image image;
    enum NodeType
    {
        Task,
        Character,
        Event,
        CharacterTab,
        TaskTab,
        EventTab,
    }
    
    private void Awake()
    {
        image = GetComponent<Image>();
    }

    private void Start()
    { 
        switch (nodeType) {
            case NodeType.Task:
                parent = RedNodeManager.instance.taskRedNode;
                break;
            case NodeType.Character:
                parent = RedNodeManager.instance.characterRedNode;
                break;
            case NodeType.Event:
                parent = RedNodeManager.instance.eventRedNode;
                break;
        }
        if (parent != null)
        {
            parent.children.Add(this);
        }
        Refresh();
    }

    void Refresh()
    {
        if (enable)
        {
            image.enabled = true;
        }
        else
        {
            image.enabled = false;
        }
    }

    public void Enable()
    {
        enable = true;
        Refresh();
        if (parent != null)
        {
            parent.Enable();
        }
    }

    public void Disable()
    {
        enable = false;
        Refresh();
        if (parent != null)
        {
            if (parent.children.Count > 0)
            {
                bool result = false;
                for (int i = 0; i < parent.children.Count; i++)
                {
                    if (parent.children[i].enable)
                    {
                        result = true;
                    }
                }
                if (!result)
                {
                    parent.Disable();
                }
            }
        }
        if (nodeType == NodeType.TaskTab)
        {
            for (int i = 0; i < children.Count; i++)
            {
                children[i].Disable();
            }
        }
    }
}
