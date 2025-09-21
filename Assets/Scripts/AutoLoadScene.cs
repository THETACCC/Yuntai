using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AutoLoadScene : MonoBehaviour
{
    public string scenename;
    public int SpawnPointLocation;
    // Start is called before the first frame update
    private IEnumerator Start()
    {
        // Delays for 1 second (affected by Time.timeScale).
        yield return new WaitForSeconds(1f);

        if (SceneController.instance != null)
        {
            SceneController.instance.LoadSceneAndTeleport(scenename, SpawnPointLocation);
        }
        else
        {
            Debug.LogWarning("SceneController.instance is null. Make sure SceneController exists in the scene.");
        }
    }
}
