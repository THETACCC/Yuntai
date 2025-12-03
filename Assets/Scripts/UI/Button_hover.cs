using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

public class ButtonHoverHighlight : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Highlight Object")]
    [SerializeField] private GameObject highlightObject;  // e.g. glow image under the button

    [Header("Text")]
    [SerializeField] private TMP_Text label;
    [SerializeField] private Color hoverTextColor = Color.yellow;

    private Color normalTextColor;

    private void Awake()
    {
        if (label == null)
            label = GetComponentInChildren<TMP_Text>();

        if (label != null)
            normalTextColor = label.color;

        ResetVisuals();
    }

    private void OnEnable()
    {
        // When you come back to this page/panel, clear any old hover state
        ResetVisuals();
    }

    private void OnDisable()
    {
        // Also reset when leaving this page/panel
        ResetVisuals();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (highlightObject != null)
            highlightObject.SetActive(true);

        if (label != null)
            label.color = hoverTextColor;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        ResetVisuals();
    }

    private void ResetVisuals()
    {
        if (highlightObject != null)
            highlightObject.SetActive(false);

        if (label != null)
            label.color = normalTextColor;
    }
}