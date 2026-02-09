using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DoorBlock : MonoBehaviour
{
    [Header("Trigger Settings")]
    public bool isReadyToTrigger = true;
    public GameObject myDoorBlock;
    public GameObject myDoorClose;
    [Header("Interaction Settings")]
    public int requiredPresses = 6;   // Can be changed in Inspector
    private int currentPressCount = 0;

    [Header("Interaction UI")]
    [SerializeField] public GameObject InteractIndicator;

    private bool isPlayerInTrigger = false;
    private Coroutine blinkRoutine;

    // Cached renderers (supports both UI and Sprite)
    private Image indicatorImage;
    private SpriteRenderer indicatorSprite;

    protected virtual void Awake()
    {
        if (InteractIndicator)
        {
            indicatorImage = InteractIndicator.GetComponent<Image>();
            indicatorSprite = InteractIndicator.GetComponent<SpriteRenderer>();
        }
    }

    protected virtual void OnTriggerEnter2D(Collider2D other)
    {
        if (!isReadyToTrigger) return;

        if (other.CompareTag("Player"))
        {
            isPlayerInTrigger = true;
            StartBlinking();
        }
    }

    protected virtual void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInTrigger = false;
            StopBlinking();
        }
    }

    void Update()
    {
        if (!isReadyToTrigger || !isPlayerInTrigger) return;

        if (Input.GetKeyDown(KeyCode.E))
        {
            currentPressCount++;

            if (currentPressCount >= requiredPresses)
            {
                DisableDoorBlock();
            }
        }
    }

    private void StartBlinking()
    {
        if (InteractIndicator == null) return;

        InteractIndicator.SetActive(true);

        if (blinkRoutine == null)
        {
            blinkRoutine = StartCoroutine(BlinkIndicator());
        }
    }

    private void StopBlinking()
    {
        if (blinkRoutine != null)
        {
            StopCoroutine(blinkRoutine);
            blinkRoutine = null;
        }

        SetIndicatorColor(Color.white);

        if (InteractIndicator)
            InteractIndicator.SetActive(false);
    }

    private IEnumerator BlinkIndicator()
    {
        while (isReadyToTrigger && isPlayerInTrigger)
        {
            SetIndicatorColor(Color.yellow);
            yield return new WaitForSeconds(0.15f);

            SetIndicatorColor(Color.white);
            yield return new WaitForSeconds(0.15f);
        }
    }

    private void SetIndicatorColor(Color color)
    {
        if (indicatorImage)
            indicatorImage.color = color;

        if (indicatorSprite)
            indicatorSprite.color = color;
    }

    public void DisableDoorBlock()
    {
        StopBlinking();

        if (myDoorBlock)
            myDoorBlock.SetActive(false);
        myDoorClose.SetActive(true);
        isReadyToTrigger = false;
    }
}