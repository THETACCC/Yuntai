using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "EventData", menuName = "NoteBook/Event Data")]
public class NoteBookEventData : ScriptableObject
{
    [System.Serializable]
    public class EventEntry
    {
        [Tooltip("事件名称的StringID")]
        public string nameID;

        [Tooltip("事件介绍的StringID")]
        public string infoID;
    }

    [Header("事件列表")]
    public List<EventEntry> events = new List<EventEntry>();
}