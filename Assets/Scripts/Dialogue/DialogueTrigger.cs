using UnityEngine;

public class DialogueTrigger : MonoBehaviour
{
    [SerializeField] private TextAsset jsonFile;
    public bool isReadyToTrigger = false;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (isReadyToTrigger)
        {
            if (Input.GetKeyDown(KeyCode.E))
            {
                Gamemanager.instance.StartDialogue();
                DialogueManager.instance.LoadDialogueFromResources(jsonFile);
                DialogueManager.instance.StartDialogue();
            }
        }
    }
}
