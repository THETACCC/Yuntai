using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(BoxCollider2D))]
public class DirectionalEnterTrigger : MonoBehaviour
{
    public enum TriggerMode
    {
        EnterLeftToRight,
        EnterRightToLeft
    }

    [Header("Settings")]
    public string playerTag = "Player";
    public TriggerMode mode = TriggerMode.EnterLeftToRight;

    [Tooltip("If true, this trigger can only fire once. Default is false.")]
    [SerializeField] private bool oneShot = false;

    [Tooltip("Minimum |x-velocity| to accept as directional movement. If the player has no Rigidbody2D, this is ignored.")]
    public float dirSpeedThreshold = 0.05f;

    [Tooltip("Log helpful messages to the Console.")]
    public bool debugLogs = false;

    [Header("Callback")]
    public UnityEvent OnTriggered;   // Drag any target object here, choose a public no-arg method

    private BoxCollider2D _zone;
    private bool _hasTriggered = false;

    private void Awake()
    {
        _zone = GetComponent<BoxCollider2D>();
        _zone.isTrigger = true; // ensure trigger
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;

        if (oneShot && _hasTriggered)
        {
            if (debugLogs) Debug.Log("[DirectionalEnterTrigger] Ignored because oneShot already fired.");
            return;
        }

        // Where did the player cross relative to the zone center?
        float playerX = other.bounds.center.x;
        float centerX = _zone.bounds.center.x;

        // If the player has a Rigidbody2D, we use velocity.x to tell L->R or R->L.
        // If there's no Rigidbody2D, we fall back to "which side entered".
        var rb = other.attachedRigidbody;
        bool hasRB = rb != null;

        bool movingRight = hasRB ? (rb.velocity.x > dirSpeedThreshold) : (playerX < centerX);
        bool movingLeft = hasRB ? (rb.velocity.x < -dirSpeedThreshold) : (playerX > centerX);

        bool fire = false;
        switch (mode)
        {
            case TriggerMode.EnterLeftToRight:
                // Expect crossing from left side and/or moving right
                fire = movingRight && (playerX <= centerX + 0.0001f);
                break;

            case TriggerMode.EnterRightToLeft:
                // Expect crossing from right side and/or moving left
                fire = movingLeft && (playerX >= centerX - 0.0001f);
                break;
        }

        if (fire)
        {
            if (debugLogs) Debug.Log($"[DirectionalEnterTrigger] Fired: {mode}");

            _hasTriggered = true;
            OnTriggered?.Invoke();
        }
        else
        {
            if (debugLogs) Debug.Log($"[DirectionalEnterTrigger] Ignored enter (mode={mode})");
        }
    }
}