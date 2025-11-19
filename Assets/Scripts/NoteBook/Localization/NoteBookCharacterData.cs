using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CharacterData", menuName = "NoteBook/Character Data")]
public class NoteBookCharacterData : ScriptableObject
{
    [System.Serializable]
    public class CharacterEntry
    {
        [Tooltip("角色名称的StringID")]
        public string nameID;

        [Tooltip("角色介绍的StringID")]
        public string infoID;
    }

    [Header("角色列表")]
    public List<CharacterEntry> characters = new List<CharacterEntry>();
}