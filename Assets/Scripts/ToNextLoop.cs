using System.Collections;
using UnityEngine;
using UnityEngine.Video;
using Fungus;    // 为了 StopAllBlocks / SayDialog
using static AudioManager;
public class ToNextLoop : MonoBehaviour
{
    [Header("Scenes")]
    public string scenename;
    public int SpawnPointLocation;

    [Header("Video Settings")]
    [Tooltip("包含 VideoPlayer 的对象，例如黑屏/死亡视频容器。")]
    public GameObject DeathVideoPlayerObject;

    [Tooltip("播放 mp4 的 VideoPlayer。")]
    public VideoPlayer deathVideoPlayer;

    [Tooltip("安全超时（秒），以防视频未正常结束。")]
    [Min(0f)] public float hardTimeoutSeconds = 10f;

    [Tooltip("是否允许按任意键跳过视频。")]
    public bool allowSkipWithAnyKey = false;

    [Header("Sound")]
    public AudioSource DeathAnimationSound;
    public AudioSource DeathAnimationSoundOneShot;

    [Header("Debug")]
    public bool verboseLog = false;

    private Coroutine _runCo;
    private bool _videoFinished = false;

    public Gamemanager myGamemanager;

    public void Start()
    {
        myGamemanager = Gamemanager.instance;

        if (deathVideoPlayer != null)
        {
            deathVideoPlayer.loopPointReached += OnVideoFinished;
        }
    }

    private void OnDestroy()
    {
        if (deathVideoPlayer != null)
        {
            deathVideoPlayer.loopPointReached -= OnVideoFinished;
        }
    }

    private void OnVideoFinished(VideoPlayer vp)
    {
        if (verboseLog) Debug.Log("[ToNextLoop] Video playback finished.");
        _videoFinished = true;
    }

    public void toNextLoop()
    {
        myGamemanager.phase = GamePhase.Talking;

        if (DeathAnimationSound) DeathAnimationSound.Play();
        // if (DeathAnimationSoundOneShot) DeathAnimationSoundOneShot.Play();

        if (_runCo != null) return;
        _runCo = StartCoroutine(WaitForVideoAndLoad());
    }

    private IEnumerator WaitForVideoAndLoad()
    {
        if (verboseLog) Debug.Log("[ToNextLoop] Waiting for death video...");

        // 1) 停止对话 + 锁玩家
        KillDialogAndFreezePlayer();

        // 2) 打开视频容器
        if (DeathVideoPlayerObject)
            DeathVideoPlayerObject.SetActive(true);

        if (!deathVideoPlayer)
        {
            if (verboseLog) Debug.LogWarning("[ToNextLoop] Missing VideoPlayer, skip to next loop.");
            ProceedToNextLoop();
            yield break;
        }

        _videoFinished = false;

        // 可选：先 Prepare，避免第一次播放黑屏/卡顿
        deathVideoPlayer.Prepare();
        while (!deathVideoPlayer.isPrepared)
        {
            yield return null;
        }

        deathVideoPlayer.frame = 0;
        deathVideoPlayer.Play();

        //Audio 
        AudioManager.PlayOneShot("Sound Effects/Chapter1/sndLoopSound", AudioGroup.SFX);
        AudioManager.PlayOneShot("Sound Effects/Chapter1/sndLoopSoundLayer2", AudioGroup.SFX);
        AudioManager.PlayOneShot("Sound Effects/Chapter1/sndLoopSoundLayer3", AudioGroup.SFX);
        AudioManager.PlayOneShot("Sound Effects/Chapter1/sndLoopSoundLayer4", AudioGroup.SFX);
        float t = 0f;

        while (true)
        {
            if (_videoFinished)
            {
                if (verboseLog) Debug.Log("[ToNextLoop] Death video finished normally.");
                break;
            }

            if (allowSkipWithAnyKey && Input.anyKeyDown)
            {
                if (verboseLog) Debug.Log("[ToNextLoop] User skipped video.");
                break;
            }

            t += Time.unscaledDeltaTime;
            if (t > hardTimeoutSeconds)
            {
                if (verboseLog) Debug.LogWarning("[ToNextLoop] Hard timeout waiting for video. Proceeding.");
                break;
            }

            yield return null;
        }

        if (deathVideoPlayer.isPlaying)
            deathVideoPlayer.Stop();

        ProceedToNextLoop();
    }

    private void KillDialogAndFreezePlayer()
    {
        try
        {
            var activeSay = SayDialog.ActiveSayDialog;
            if (activeSay != null)
                activeSay.gameObject.SetActive(false);

            Flowchart[] charts = FindObjectsOfType<Flowchart>();
            foreach (var fc in charts)
                fc.StopAllBlocks();
        }
        catch (System.Exception e)
        {
            if (verboseLog) Debug.LogWarning("[ToNextLoop] KillDialog error: " + e.Message);
        }

        var pc = PlayerController.Instance;
        if (pc == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                pc = player.GetComponent<PlayerController>();
        }

        if (pc != null)
        {
            pc.DisablePlayerControl();
            if (verboseLog) Debug.Log("[ToNextLoop] Player control disabled before death video.");
        }
        else
        {
            if (verboseLog) Debug.LogWarning("[ToNextLoop] KillDialogAndFreezePlayer: PlayerController not found.");
        }

        if (Gamemanager.instance)
            Gamemanager.instance.phase = GamePhase.Eventing;
    }

    private void RestorePlayerForNextScene()
    {
        var pc = PlayerController.Instance;
        if (pc == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                pc = player.GetComponent<PlayerController>();
        }

        if (pc != null)
        {
            pc.EnablePlayerControl();
            if (verboseLog) Debug.Log("[ToNextLoop] Player control enabled for next scene.");
        }

        if (Gamemanager.instance)
            Gamemanager.instance.phase = GamePhase.Moving;
    }

    private void ProceedToNextLoop()
    {
        if (verboseLog)
        {
            var pc = PlayerController.Instance;
            if (pc != null)
            {
                var pos = pc.transform.position;
                Debug.Log($"[ToNextLoop] Proceeding to next scene {scenename}, spawnIndex={SpawnPointLocation}, current player pos={pos}");
            }
            else
            {
                Debug.Log($"[ToNextLoop] Proceeding to next scene {scenename}, spawnIndex={SpawnPointLocation}, PlayerController.Instance is null.");
            }
        }

        RestorePlayerForNextScene();

        LoopTracker.I?.IncrementLoop();

        if (SceneController.instance != null)
        {
            SceneController.instance.LoadSceneAndTeleport(scenename, SpawnPointLocation);
        }
        else
        {
            Debug.LogError("[ToNextLoop] SceneController.instance 为 null，无法切场景。");
        }

        _runCo = null;
    }
}