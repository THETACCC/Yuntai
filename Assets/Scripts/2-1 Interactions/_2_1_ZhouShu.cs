using UnityEngine;

/// <summary>
/// Attach to any GameObject. Assign a target location in the Inspector,
/// then call MoveToDesignatedLocation() to move this object to that point.
/// If a Rigidbody2D is present, uses MovePosition; otherwise moves Transform.
/// Optionally, assign a sprite to switch to after arriving.
/// </summary>
public class _2_1_ZhouShu : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform designatedLocation;

    [Header("Movement")]
    [SerializeField, Min(0.01f)] private float moveSpeed = 3f;   // units per second
    [SerializeField] private bool keepCurrentY = false;          // only move on X/Z

    [Header("Optional Sprite Change On Arrival")]
    [Tooltip("If assigned, switch to this sprite after the movement finishes.")]
    [SerializeField] private Sprite spriteAfterMove;
    [Tooltip("Renderer to apply the sprite to. If empty, will auto-find on this GameObject or its children.")]
    [SerializeField] private SpriteRenderer targetRenderer;

    private Rigidbody2D rb2D;
    private Coroutine moveRoutine;

    private void Awake()
    {
        rb2D = GetComponent<Rigidbody2D>();
        if (!targetRenderer)
            targetRenderer = GetComponent<SpriteRenderer>() ?? GetComponentInChildren<SpriteRenderer>();
    }

    /// <summary>
    /// Public entry point: start moving to the designated location.
    /// </summary>
    public void MoveToDesignatedLocation()
    {
        if (!designatedLocation)
        {
            Debug.LogWarning("[_2_1_ZhouShu] No designatedLocation assigned.");
            return;
        }

        if (moveRoutine != null) StopCoroutine(moveRoutine);
        moveRoutine = StartCoroutine(MoveTo(designatedLocation.position));
    }

    /// <summary>
    /// (Optional) Change the target at runtime.
    /// </summary>
    public void SetDesignatedLocation(Transform newTarget)
    {
        designatedLocation = newTarget;
    }

    /// <summary>
    /// (Optional) Stop the current movement.
    /// </summary>
    public void StopMoving()
    {
        if (moveRoutine != null)
        {
            StopCoroutine(moveRoutine);
            moveRoutine = null;
        }
        if (rb2D) rb2D.velocity = Vector2.zero;
    }

    /// <summary>
    /// (Optional) Set which renderer will receive the sprite change.
    /// </summary>
    public void SetTargetRenderer(SpriteRenderer renderer)
    {
        targetRenderer = renderer;
    }

    /// <summary>
    /// (Optional) Provide/override the sprite to apply after movement completes.
    /// Pass null to disable the post-move sprite change.
    /// </summary>
    public void SetSpriteAfterMove(Sprite newSprite)
    {
        spriteAfterMove = newSprite;
    }

    private System.Collections.IEnumerator MoveTo(Vector3 target)
    {
        if (keepCurrentY) target.y = transform.position.y;

        const float eps = 0.0004f;

        while ((transform.position - target).sqrMagnitude > eps)
        {
            Vector3 next = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);

            if (rb2D)
                rb2D.MovePosition(next);
            else
                transform.position = next;

            yield return null;
        }

        // Snap to final target.
        if (rb2D) rb2D.MovePosition(target);
        else transform.position = target;

        // Optional: apply sprite after arriving.
        if (spriteAfterMove && targetRenderer)
        {
            targetRenderer.sprite = spriteAfterMove;
        }

        moveRoutine = null;
    }
}
