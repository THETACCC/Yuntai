using SKCell;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SceneControllerEditor : SKMonoSingleton<SceneControllerEditor>
{
    [Header("Scene Info")]
    public SceneInfo sceneInfo;

    [Header("CameraReference")]
    public FindBound findBound;
    [Tooltip("Reference point used to choose the nearest bound. Defaults to this transform.")]
    public Transform reference;


    [Header("Spawn Settings")]
    [Tooltip("Spawn point id to use right after a scene loads.")]
    public int defaultSpawnId = 0;

    // Internal: track whether we should teleport after load
    private bool _pendingTeleport = false;
    private int _pendingSpawnId = 0;

    void Start()
    {
        // When the next scene is loaded, do our setup then teleport to spawn
        SKSceneManager.instance.onNextSceneLoaded.AddListener(() =>
        {
            // Give Unity a brief frame slice so everything in the new scene initializes
            SKUtils.InvokeAction(0.1f, () =>
            {
                LoadSceneSetup();
                TryTeleportToSpawn(_pendingSpawnId);
                findBound.AssignNearestBound(reference, "Bounds");
                _pendingTeleport = false;
            });
        });
    }

    /// <summary>
    /// Call this to begin loading a scene and schedule an auto-teleport to SpawnPoint 0.
    /// </summary>
    public bool LoadSceneAsset(SceneInfo info)
    {
        sceneInfo = info;

        // Mark that we want to teleport after the scene switch completes
        _pendingTeleport = true;
        _pendingSpawnId = defaultSpawnId; // always 0 unless you change the field

        // Begin async load via SKSceneManager
        bool started = SKSceneManager.instance.LoadSceneAsync(
            GlobalLibrary.G_SCENE_LOADING_ASSET_NAME,
            GlobalLibrary.G_SCENE_ASSET_NAME[info.index]
        );

        if (started)
        {
            RuntimeData.isSceneLoading = true;
            EventDispatcher.Dispatch(EventDispatcher.Common, EventRef.CM_ON_SCENE_EXIT);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Performs post-load bookkeeping.
    /// </summary>
    public void LoadSceneSetup()
    {
        RuntimeData.activeSceneTitle = sceneInfo.index;
        RuntimeData.isSceneLoading = false;
    }

    /// <summary>
    /// Attempts to find the player and move them to the requested SpawnPoint id.
    /// </summary>
    private void TryTeleportToSpawn(int spawnId)
    {
        if (!_pendingTeleport) return;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            Debug.LogWarning("SceneController: Player (tag 'Player') not found after scene load.");
            return;
        }

        Transform targetSpawn = FindSpawnPointTransform(spawnId);
        if (targetSpawn == null)
        {
            Debug.LogWarning($"SceneController: No SpawnPoint with id {spawnId} found in the loaded scene.");
            return;
        }

        // Teleport: preserve player's Z if your game is 2D; otherwise take full position.
        Vector3 dest = targetSpawn.position;
        if (Mathf.Abs(player.transform.position.z - dest.z) > 0.0001f)
        {
            // If you prefer to preserve player's original Z:
            dest.z = player.transform.position.z;
        }

        player.transform.position = dest;
        // Optional: reset velocity/inputs here if you use a Rigidbody/CharacterController.

        // Optional: align rotation to spawn
        // player.transform.rotation = targetSpawn.rotation;
    }

    /// <summary>
    /// Finds a SpawnPoint by id, with a fallback to a named object "SpawnPoint_{id}".
    /// </summary>
    private Transform FindSpawnPointTransform(int spawnId)
    {
        // Preferred: find via component (fast if you cache, but OK here at load)
        SpawnPoint[] all = GameObject.FindObjectsOfType<SpawnPoint>();
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].id == spawnId)
                return all[i].transform;
        }

        // Fallback: exact name "SpawnPoint_{id}"
        string fallbackName = $"SpawnPoint_{spawnId}";
        GameObject byName = GameObject.Find(fallbackName);
        if (byName != null)
            return byName.transform;

        return null;
    }
}

[System.Serializable]
public struct SceneInfo
{
    public SceneTitle index;
    public SceneTeleportType teleportType;
    public Vector3 position;
}

public enum SceneTeleportType
{
    SpawnPoint,
    CheckPoint,
    CustomPosition
}

public enum SceneTitle
{
    TitleScene = 0,
    GYM_Level = 1,
    InitialCG = 2,
    Level1_1 = 3,
    Level1_2 = 4,
    Level2_1 = 5,
    Level2_2 = 6,
    Level3_1 = 7,
    Level3_2 = 8,
    Level3_3 = 9,
    Level3_4 = 10,
    Level3_5 = 11,
    Level3_6 = 12,
    Level4_1City = 13,
    Level4_2Festival = 14,
    Level4_3Apartment1F = 15,
    Level4_4Apartment2F = 16,
    Level4_5Apartment3F = 17,
    Level4_6NoemaHouse = 18,
    Level4_17Stage = 19,
    Level4_8Temple = 20,
    Level4_6_1Ghost3F = 21,
    Level4_6_2LoopStuck1 = 22,
    Level4_6_3LoopDead = 23,
    Level4_6_4LoopStuck2 = 24,
    Level4_6_5LoopStuck3 = 25,
    Level4_6_6FinalChase = 26,
    Level4_7FestivalNoema = 27,
    Level4_8FestivalGhost = 28,
    Level4_9FestivalDead = 29,
    Level4_10FestivalNoemaHelp = 30,
    Level4_11FestivalPuzzle1 = 31,
    Level4_12FestivalPuzzle2 = 32,
    Level4_13FestivalPuzzle3 = 33,
    Level4_14FestivalPuzzle4 = 34,
    Level4_15FestivalPuzzle5 = 35,
    Level4_16FestivalKill = 36,
    JasmineTest = 50
}

public enum SceneCategory
{
    Common = 0,
}