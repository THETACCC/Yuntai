using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnableAfterDelay : MonoBehaviour
{
    [Tooltip("GameObject to enable after the delay")]
    public GameObject targetObject;

    [Tooltip("Time in seconds before enabling")]
    public float delay = 2f;

    void Start()
    {
        EnableGhost();
    }


    public void EnableGhost()
    {
            if (targetObject != null)
            {
                StartCoroutine(EnableObjectAfterDelay());
            }
            else
            {
                Debug.LogWarning("Target Object is not assigned.");
            }
        
    }

    IEnumerator EnableObjectAfterDelay()
    {
        yield return new WaitForSeconds(delay);
        Debug.Log("Enable Ghost!");
        targetObject.SetActive(true);
    }
}
