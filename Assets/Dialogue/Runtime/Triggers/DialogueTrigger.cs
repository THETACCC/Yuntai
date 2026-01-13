using UnityEngine;

public class DialogueTrigger : MonoBehaviour
{
    public TextAsset mainDialogueJsonFile;
    public TextAsset postDialogueJsonFile;

    public bool isReadyToTrigger = false; //是否可以触发对话
    public bool isMainDialogueFinished = false; //主要对话是否结束

    [SerializeField] bool isDoor = false;

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
                TriggerDialogue();
            }
            if (Input.GetKeyDown(KeyCode.W) && isDoor)
            {
                TriggerDialogue();
            }
        }

    }

    public void TriggerDialogue()
    {
        if (!DialogueController.instance.isDialogueActive)
        {
            if (!isMainDialogueFinished)
            {
                DialogueController.instance.currentTrigger = this;
                Gamemanager.instance?.StartDialogue();
                DialogueController.instance.LoadDialogueFromFile(mainDialogueJsonFile);
                DialogueController.instance.StartDialogue();
            }
            else
            {
                if (postDialogueJsonFile != null)
                {
                    Gamemanager.instance?.StartDialogue();
                    DialogueController.instance.LoadDialogueFromFile(postDialogueJsonFile);
                    DialogueController.instance.StartDialogue();
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
}