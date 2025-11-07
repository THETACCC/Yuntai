using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UI_E : MonoBehaviour
{
    [Header("Interaction UI")]
    [SerializeField] protected GameObject InteractIndicator;

    // child classes can read this
    protected bool isPlayerInTrigger = false;

    protected virtual void Start()
    {
        if (InteractIndicator) InteractIndicator.SetActive(false);
    }

    protected virtual void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInTrigger = true;
            SetIndicator(true);
        }
    }

    // <-- CHANGED: now protected virtual (was private), so children can call/override
    protected virtual void OnTriggerStay2D(Collider2D collision)
    {
        // This disables the E when player presses
        if (collision.CompareTag("Player"))
        {
            if (Input.GetKey(KeyCode.E))
                SetIndicator(false);
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

    // Quality-of-life: auto-find an indicator if you forgot to assign one
    private void Reset()
    {
        if (InteractIndicator == null)
        {
            var t = transform.Find("E") ?? transform.Find("InteractIndicator");
            if (t) InteractIndicator = t.gameObject;
        }
    }
}
