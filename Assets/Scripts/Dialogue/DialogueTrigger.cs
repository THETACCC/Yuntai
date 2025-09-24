using UnityEngine;

public class DialogueTrigger : MonoBehaviour
{
    public TextAsset mainDialogueJsonFile;
    public TextAsset postDialogueJsonFile;



    public bool isReadyToTrigger = false; //是否可以触发对话
    public bool isMainDialogueFinished = false; //主要对话是否结束

    //public UnityEvent OnDialogueCompleted;

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
                if (!DialogueManager.instance.isDialogueActive)
                {
                    if (!isMainDialogueFinished)
                    {
                        DialogueManager.instance.currentTrigger = this;
                        Gamemanager.instance?.StartDialogue();
                        DialogueManager.instance.LoadDialogueFromFile(mainDialogueJsonFile);
                        DialogueManager.instance.StartDialogue();
                    }
                    else
                    {
                        if (postDialogueJsonFile != null)
                        {
                            Gamemanager.instance?.StartDialogue();
                            DialogueManager.instance.LoadDialogueFromFile(postDialogueJsonFile);
                            DialogueManager.instance.StartDialogue();
                        }
                    }
                }
            }
        }

    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isReadyToTrigger = true;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isReadyToTrigger = false;
        }
    }

    public void Test()
    {
        Debug.Log("222222222222222222");
    }
}
