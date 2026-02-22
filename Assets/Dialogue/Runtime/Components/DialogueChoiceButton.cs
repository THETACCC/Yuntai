using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static AudioManager;

public class DialogueChoiceButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    public int index;
    public bool visible = true;
    Button bt;

    public TextMeshProUGUI content;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        bt = GetComponent<Button>();
    }

    // Update is called once per frame
    void Update()
    {

    }

    public void GoToChoiceIndex()
    {
        //Audio
        AudioManager.Play("Sound Effects/Henk/sndChoice", AudioGroup.SFX);

        SetDialogueIndex();
        UpdateDialogue();
    }

    void SetDialogueIndex()
    {
        if (DialogueController.instance != null)
        {
            DialogueController.instance.SetDialogueIndex(index);
        }
        else
        {
            Debug.LogError("Please Assign DialogueController");
        }
    }

    void UpdateDialogue()
    {
        if (DialogueController.instance != null)
        {
            DialogueController.instance.UpdateDialogue(DialogueDisplaySettings.instance?.currentLanguage);
        }
        else
        {
            Debug.LogError("Please Assign DialogueController");
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        foreach (Transform child in transform)
        {
            RectTransform rect = child as RectTransform;
            if (rect != null)
            {
                rect.position += Vector3.right * 30;
            }
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        foreach (Transform child in transform)
        {
            RectTransform rect = child as RectTransform;
            if (rect != null)
            {
                rect.position -= Vector3.right * 30;
            }
        }
    }

    public void OnSelect(BaseEventData eventData)
    {
        Debug.Log("Highlighted/selected by keyboard or controller!");
    }

    public void OnDeselect(BaseEventData eventData)
    {
        Debug.Log("No longer selected.");
    }

}