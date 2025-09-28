using System.Collections.Generic;

namespace DialogueSystem
{
    /// <summary>
    /// Parameter types supported by dialogue events
    /// </summary>
    [System.Serializable]
    public enum ParameterType
    {
        None,
        String,
        Int,
        Float,
        Bool
    }

    /// <summary>
    /// Data structure for dialogue event calls
    /// </summary>
    [System.Serializable]
    public class DialogueEventCall
    {
        public string targetObjectName = "";  // Target GameObject name
        public string componentTypeName = ""; // Component type name (e.g., "GameManager", "AudioSource")
        public string methodName = "";        // Method name
        public string stringParameter = "";   // String parameter
        public int intParameter = 0;          // Integer parameter
        public float floatParameter = 0f;     // Float parameter
        public bool boolParameter = false;    // Boolean parameter
        public ParameterType parameterType = ParameterType.None; // Parameter type
    }

    /// <summary>
    /// Helper class for serializing event call lists
    /// </summary>
    [System.Serializable]
    public class SerializableEventCallList
    {
        public List<DialogueEventCall> eventCalls = new List<DialogueEventCall>();
    }
}