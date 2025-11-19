using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "EventDetailData", menuName = "NoteBook/Event Detail Data")]
public class NoteBookEventDetailData : ScriptableObject
{
    [System.Serializable]
    public class DetailEntry
    {
        [Tooltip("标题的StringID")]
        public string titleID;

        [Tooltip("正文段落0的StringID")]
        public string body0ID;

        [Tooltip("正文段落1的StringID")]
        public string body1ID;
    }

    [Header("详情列表")]
    public List<DetailEntry> details = new List<DetailEntry>();
}