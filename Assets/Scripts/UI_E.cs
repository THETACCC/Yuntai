using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using static AudioManager;

public class UI_E : MonoBehaviour
{
    [Header("Interaction UI")]
    [SerializeField] protected GameObject InteractIndicator;

    [Header("E Press Trigger")]
    [SerializeField] private bool triggerManagerFunctionOnE = false;
    [SerializeField] private UnityEvent onEPressed;

    private DialogueTrigger dialogueTrigger;

    protected bool isPlayerInTrigger = false;

    protected virtual void Start()
    {
        dialogueTrigger = GetComponent<DialogueTrigger>();

        if (InteractIndicator) InteractIndicator.SetActive(false);
    }

    protected virtual void Update()
    {
        if (!isPlayerInTrigger) return;

        bool isDoor = dialogueTrigger != null && dialogueTrigger.IsDoor;

        bool pressedCorrectKey =
            (!isDoor && Input.GetKeyDown(KeyCode.E)) ||
            (isDoor && Input.GetKeyDown(KeyCode.W));

        if (pressedCorrectKey)
        {
            SetIndicator(false);

            AudioManager.Play("Sound Effects/Henk/sndSpeak", AudioGroup.SFX);

            if (triggerManagerFunctionOnE)
            {
                onEPressed?.Invoke();
            }
        }
    }

    protected virtual void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInTrigger = true;
            SetIndicator(true);
        }
    }

    protected virtual void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInTrigger = false;
            SetIndicator(false);
        }
    }

    protected void SetIndicator(bool on)
    {
        if (InteractIndicator) InteractIndicator.SetActive(on);
    }

    private void Reset()
    {
        if (InteractIndicator == null)
        {
            var t = transform.Find("E") ?? transform.Find("InteractIndicator");
            if (t) InteractIndicator = t.gameObject;
        }
    }
}