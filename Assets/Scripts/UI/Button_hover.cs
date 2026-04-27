using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;

public class ButtonHoverHighlight : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Highlight Object")]
    [SerializeField] private GameObject highlightObject;  // e.g. glow image under the button

    [Header("Text")]
    [SerializeField] private TMP_Text label;
    [SerializeField] private Color hoverTextColor = Color.yellow;

    [Header("Move Animation")]
    [SerializeField] private bool enableMoveAnimation = false;
    [SerializeField] private float moveUpDistance = 10f;
    [SerializeField] private float animationDuration = 0.15f;

    private Color normalTextColor;
    private RectTransform rectTransform;
    private Vector2 originalPosition;
    private Coroutine currentAnimation;

    private void Awake()
    {
        if (label == null)
            label = GetComponentInChildren<TMP_Text>();

        if (label != null)
            normalTextColor = label.color;

        rectTransform = GetComponent<RectTransform>();
        if (rectTransform != null)
            originalPosition = rectTransform.anchoredPosition;

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

        // 上移动画
        if (enableMoveAnimation && rectTransform != null)
        {
            if (currentAnimation != null)
                StopCoroutine(currentAnimation);

            Vector2 targetPos = new Vector2(originalPosition.x, originalPosition.y + moveUpDistance);
            currentAnimation = StartCoroutine(AnimatePosition(targetPos));
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        ResetVisuals();

        // 恢复位置动画
        if (enableMoveAnimation && rectTransform != null)
        {
            if (currentAnimation != null)
                StopCoroutine(currentAnimation);

            currentAnimation = StartCoroutine(AnimatePosition(originalPosition));
        }
    }

    private void ResetVisuals()
    {
        if (highlightObject != null)
            highlightObject.SetActive(false);

        if (label != null)
            label.color = normalTextColor;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // 点击时立即还原位置
        if (enableMoveAnimation && rectTransform != null)
        {
            if (currentAnimation != null)
                StopCoroutine(currentAnimation);

            rectTransform.anchoredPosition = originalPosition;
        }
    }

    private IEnumerator AnimatePosition(Vector2 targetPosition)
    {
        Vector2 startPosition = rectTransform.anchoredPosition;
        float elapsed = 0f;

        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / animationDuration;
            rectTransform.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, t);
            yield return null;
        }

        rectTransform.anchoredPosition = targetPosition;
    }
}