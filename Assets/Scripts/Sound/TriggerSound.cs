using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(AudioSource))]
public class TriggerSound : MonoBehaviour
{
    public enum AfterPlayOnceAction { None, DisableCollider, DisableGameObject, DestroyGameObject }

    [Header("Trigger options")]
    public string playerTag = "Player";
    public bool playOnce = false;                 // play only once
    public bool restartIfAlreadyPlaying = false;  // restart if already playing

    [Header("Dialogue JSON assignments (simple)")]
    [Tooltip("Apply assignments after audio finishes (true) or immediately (false).")]
    public bool applyAssignmentsAfterAudioEnd = false;
    [Tooltip("Extra delay before applying assignments (seconds).")]
    public float extraAssignmentsDelay = 0f;
    [Tooltip("Apply assignments only once.")]
    public bool assignmentsOnlyOnce = true;

    [System.Serializable]
    public class DialogueAssignment
    {
        [Tooltip("GameObject that has a DialogueTrigger component")]
        public DialogueTrigger target;        // drag the object with DialogueTrigger here
        [Tooltip("If set, replace target.mainDialogueJsonFile")]
        public TextAsset mainJson;            // leave null to keep current
        [Tooltip("If set, replace target.postDialogueJsonFile")]
        public TextAsset postJson;            // leave null to keep current
    }

    public DialogueAssignment[] assignments;

    [Header("After it has played once")]
    public AfterPlayOnceAction afterPlayOnceAction = AfterPlayOnceAction.None;
    public bool waitForAudioEndBeforeAction = true;
    public float extraActionDelay = 0f;

    private AudioSource _audio;
    private Collider2D _col;
    private bool _hasPlayed = false;
    private bool _assignmentsApplied = false;

    private void Awake()
    {
        _audio = GetComponent<AudioSource>();
        _col = GetComponent<Collider2D>();
        _col.isTrigger = true; // ensure trigger
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;
        if (_audio == null || _audio.clip == null) return;

        if (playOnce && _hasPlayed) return;

        if (_audio.isPlaying && !restartIfAlreadyPlaying) return;
        if (_audio.isPlaying && restartIfAlreadyPlaying) _audio.Stop();

        _audio.Play();
        _hasPlayed = true;

        // Apply JSON assignments (now or after audio)
        if (!assignmentsOnlyOnce || !_assignmentsApplied)
        {
            if (applyAssignmentsAfterAudioEnd) StartCoroutine(DoAssignmentsAfterAudio());
            else ApplyAssignments();
        }

        // After-play-once action (disable/destroy/etc.)
        if (playOnce && afterPlayOnceAction != AfterPlayOnceAction.None)
        {
            if (waitForAudioEndBeforeAction) StartCoroutine(DoAfterAudioEnds_Action());
            else ApplyAfterPlayOnceAction();
        }
    }

    private IEnumerator DoAssignmentsAfterAudio()
    {
        while (_audio != null && _audio.isPlaying) yield return null;
        if (extraAssignmentsDelay > 0f) yield return new WaitForSeconds(extraAssignmentsDelay);
        ApplyAssignments();
    }

    private void ApplyAssignments()
    {
        if (assignments == null || assignments.Length == 0) return;

        foreach (var a in assignments)
        {
            if (a == null || a.target == null) continue;

            // Replace only the fields that are assigned
            if (a.mainJson != null) a.target.mainDialogueJsonFile = a.mainJson;
            if (a.postJson != null) a.target.postDialogueJsonFile = a.postJson;
        }

        _assignmentsApplied = true;
    }

    private IEnumerator DoAfterAudioEnds_Action()
    {
        while (_audio != null && _audio.isPlaying) yield return null;
        if (extraActionDelay > 0f) yield return new WaitForSeconds(extraActionDelay);
        ApplyAfterPlayOnceAction();
    }

    private void ApplyAfterPlayOnceAction()
    {
        switch (afterPlayOnceAction)
        {
            case AfterPlayOnceAction.DisableCollider:
                if (_col) _col.enabled = false; break;
            case AfterPlayOnceAction.DisableGameObject:
                gameObject.SetActive(false); break;
            case AfterPlayOnceAction.DestroyGameObject:
                Destroy(gameObject); break;
            case AfterPlayOnceAction.None:
            default: break;
        }
    }
}
