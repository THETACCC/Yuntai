using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneController : MonoBehaviour
{
    public static SceneController instance;

    [Header("Teleport Options")]
    [Tooltip("If true, zeroes out the player's Rigidbody2D velocity when teleporting.")]
    public bool resetPlayerVelocity2D = true;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Loads the scene asynchronously and teleports the player to the SpawnPoint with the given numeric id.
    /// </summary>
    public void LoadSceneAndTeleport(string sceneName, int spawnId)
    {
        StartCoroutine(LoadSceneAndTeleportRoutine(sceneName, spawnId));
    }

    private IEnumerator LoadSceneAndTeleportRoutine(string sceneName, int spawnId)
    {
        // Start loading the scene
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
        while (!asyncLoad.isDone)
            yield return null;

        // Scene is loaded¡ªfind the player and the target spawn point.
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            Debug.LogWarning("SceneController: Player (tag 'Player') not found after scene load.");
            yield break;
        }

        Transform targetSpawn = FindSpawnPointTransform(spawnId);
        if (targetSpawn == null)
        {
            Debug.LogWarning($"SceneController: No SpawnPoint with id {spawnId} found in scene '{sceneName}'.");
            yield break;
        }

        // Teleport
        player.transform.position = targetSpawn.position;

        // Optional: reset 2D velocity so the player doesn't slide off spawn
        if (resetPlayerVelocity2D)
        {
            var rb2d = player.GetComponent<Rigidbody2D>();
            if (rb2d) rb2d.velocity = Vector2.zero;
        }
    }

    /// <summary>
    /// Fallback simple load without teleport.
    /// </summary>
    public void LoadScene(string sceneName)
    {
        SceneManager.LoadSceneAsync(sceneName);
    }

    /// <summary>
    /// Finds a SpawnPoint by numeric id. Also supports a fallback name pattern "SpawnPoint_{id}".
    /// </summary>
    private Transform FindSpawnPointTransform(int spawnId)
    {
        // Preferred: find via component
        SpawnPoint[] all = GameObject.FindObjectsOfType<SpawnPoint>();
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].id == spawnId)
                return all[i].transform;
        }

        // Fallback: try by exact name "SpawnPoint_{id}"
        string fallbackName = $"SpawnPoint_{spawnId}";
        GameObject byName = GameObject.Find(fallbackName);
        if (byName != null)
            return byName.transform;

        return null;
    }
}
