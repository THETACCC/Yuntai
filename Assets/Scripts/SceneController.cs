using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneController : MonoBehaviour
{
    public static SceneController instance;
    public bool loadingGame;

    [Header("Teleport Options")]
    [Tooltip("If true, zeroes out the player's Rigidbody2D velocity when teleporting.")]
    public bool resetPlayerVelocity2D = true;

    [Header("Camera Bound")]
    [Tooltip("可选：场景里的 CameraBound 物体（上面挂着 FindBound）。如果不指定，会在场景中自动查找。")]
    public GameObject CameraBound;
    public FindBound findbound;

    [SerializeField] private Animator transitionAnim;

    [Header("Debug")]
    [SerializeField] private bool verboseLog = false;

    //Note Book Ref
    public NoteBookManager noteBookManager;



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

    private void Start()
    {

        GameObject myNoteBook = GameObject.FindGameObjectWithTag("NoteBook");
        if (myNoteBook != null)
        {
            noteBookManager = myNoteBook.GetComponent<NoteBookManager>();
        }



        RefreshCameraBound();
    }

    /// <summary>
    /// 每次进新场景后，重新找一遍 FindBound / CameraBound。
    /// </summary>
    private void RefreshCameraBound()
    {
        // 如果 Inspector 里没指定 CameraBound，就直接全局找一个 FindBound
        if (CameraBound == null)
        {
            findbound = FindObjectOfType<FindBound>();
            if (findbound != null)
            {
                CameraBound = findbound.gameObject;
                if (verboseLog) Debug.Log("[SceneController] Found FindBound via FindObjectOfType.");
            }
            else
            {
                if (verboseLog) Debug.LogWarning("[SceneController] No FindBound found in scene.");
            }
        }
        else
        {
            findbound = CameraBound.GetComponent<FindBound>();
            if (findbound == null && verboseLog)
                Debug.LogWarning("[SceneController] CameraBound is set but has no FindBound component.");
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
        if (verboseLog)
            Debug.Log($"[SceneController] LoadSceneAndTeleport -> scene='{sceneName}', spawnId={spawnId}");
        var gm = Gamemanager.instance;
        if(gm != null)
        {
            gm.phase = GamePhase.Eventing;
        }
        // 1) 黑屏
        if (transitionAnim != null)
        {
            noteBookManager.disableNoteBook();


            transitionAnim.SetTrigger("End");



            yield return new WaitForSeconds(1.5f);

        }

        // 2) 异步加载新场景
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
        while (!asyncLoad.isDone)
            yield return null;

        // 3) 场景加载完了，刷新 CameraBound / FindBound 引用
        RefreshCameraBound();

        // 4) 找 Player —— 优先单例，其次 tag
        GameObject player = null;
        PlayerController pc = PlayerController.Instance;
        if (pc != null)
        {
            player = pc.gameObject;
            if (verboseLog)
                Debug.Log("[SceneController] Found Player via PlayerController.Instance.");
        }
        else
        {
            player = GameObject.FindGameObjectWithTag("Player");
            if (player != null && verboseLog)
                Debug.Log("[SceneController] Found Player via tag 'Player'.");
        }

        if (player == null)
        {
            Debug.LogWarning($"[SceneController] Player not found after loading scene '{sceneName}'.");
            yield break;
        }

        // 5) 找 SpawnPoint
        Transform targetSpawn = FindSpawnPointTransform(spawnId);
        if (targetSpawn == null)
        {
            Debug.LogWarning($"[SceneController] No SpawnPoint with id {spawnId} found in scene '{sceneName}'.");
            yield break;
        }

        // 6) 传送
        if (verboseLog)
        {
            Debug.Log($"[SceneController] Teleporting player from {player.transform.position} to {targetSpawn.position}");
        }

        player.transform.position = targetSpawn.position;

        // 7) Camera 边界绑定
        if (findbound != null)
        {
            findbound.AssignNearestBound(player.transform, "Bounds");
        }
        else if (verboseLog)
        {
            Debug.LogWarning("[SceneController] findbound is null, cannot assign camera bounds.");
        }

        // 8) 可选：清零速度
        if (resetPlayerVelocity2D)
        {
            var rb2d = player.GetComponent<Rigidbody2D>();
            if (rb2d) rb2d.velocity = Vector2.zero;
        }

        // 9) 打开画面
        if (transitionAnim != null)
        {
            yield return new WaitForSeconds(0.5f);



            transitionAnim.SetTrigger("Start");
            noteBookManager.enableNoteBook();
            if (gm != null)
            {
                gm.phase = GamePhase.Moving;
            }
        }

        // 10)保存存档
        if (loadingGame)
        {
            loadingGame = false;
        } else
        {
            SaveManager.instance.SaveGame(isAutoSave: true);
        }
    }

    public void setBlackScreenTrigger()
    {
        // 1) 黑屏
        if (transitionAnim != null)
        {
        //    transitionAnim.SetTrigger("End");
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
