using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Cinemachine;  
using URPLight2D = UnityEngine.Rendering.Universal.Light2D;

public class LevelManager3_3 : BaseLevelManager
{
    [Header("Lantern Stall")]
    [SerializeField] private GameObject LanternStall;
    [SerializeField] private GameObject LanternStall_wait;
    [SerializeField] private GameObject LanternStall_wrong;
    [SerializeField] private GameObject LanternStall_correct;

    [SerializeField] private GameObject LanternPile;
    [SerializeField] private GameObject Lanterns;

    [Header("Puzzle")]
    [SerializeField] private PuzzleLanternManager puzzleLanternManager;
    [SerializeField] private Collider2D puzzleLanternCollider;
    [SerializeField] private Collider2D puzzleLanternColliderChild;

    [Header("Horror Lantern Sequence")]
    [Tooltip("Set to true from dialogue when the horror sequence should run.")]
    public bool NeedToPlayLanternAnim = false;

    [Tooltip("Optional giant shadow shown during the last green flash.")]
    [SerializeField] private GameObject beastShadow;

    [Header("Horror Timing")]
    [SerializeField, Min(1)] private int greenBlinkCount = 3;
    [SerializeField, Min(0f)] private float greenOnTime = 0.25f;
    [SerializeField, Min(0f)] private float greenOffTime = 0.15f;
    [SerializeField, Min(0f)] private float shakeDuration = 3f;
    [SerializeField, Min(0f)] private float blackFadeTime = 0.5f;
    [SerializeField, Min(0f)] private float blackHoldTime = 1.5f;

    [Header("Audio")]
    [SerializeField] private AudioSource horrorAudioSource;
    [SerializeField] private AudioClip horrorRoarClip;

    [Header("Dialogue After Horror")]
    [SerializeField] private DialogueTrigger MC_scaredDialogue;

    [Header("Doors")]
    [SerializeField] private GameObject door;

    [Header("Horror – Player Soul & BW")]
    [SerializeField] private PlayerSoulEchoController playerSoulEcho;
    [SerializeField] private BlackAndWhiteFlicker blackWhiteFlicker;

    [Header("Horror Camera Zoom/Tilt")]
    [Tooltip("VCam used for the horror sequence (usually the main camera).")]
    [SerializeField] private CinemachineVirtualCamera horrorVCam;

    [Tooltip("Target orthographic size during the soul-out shot.")]
    [SerializeField] private float soulZoomSize = 7.5f;

    [Tooltip("Duration of the zoom / tilt tween.")]
    [SerializeField] private float soulZoomTime = 0.4f;

    [Tooltip("Extra Z-rotation applied during the horror zoom.")]
    [SerializeField] private float soulTiltAngle = 8f;

    [Tooltip("Easing for the camera zoom / tilt.")]
    [SerializeField]
    private AnimationCurve soulZoomCurve =
        AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Horror Post FX – Wipe & Web")]
    [Tooltip("Full-screen wipe/smear material.")]
    [SerializeField] private Material wipeSmearMaterial;
    [SerializeField] private float wipeSmearDuration = 1.2f;

    [Tooltip("Controls the spider-web build up effect.")]
    [SerializeField] private SpiderWebAnimator spiderWebAnimator;
    [SerializeField] private float spiderWebExtraHold = 0.3f;

    private CameraShake cameraShake;

    private const string OverlayName = "__BlackOverlay__";
    private CanvasGroup overlayCG;

    private bool isPlayingLanternHorror = false;

    private PuzzleLantern[] puzzleLanterns = new PuzzleLantern[0];

    private bool rb2dHadSimulated;
    private RigidbodyConstraints2D rb2dOldConstraints;

    private Animator[] playerAnimators;
    private float[] animatorOrigSpeeds;

    private float originalOrthoSize;
    private Quaternion originalCamRot;
    private Coroutine camTweenRoutine;

    private static readonly int PropWipeProgress = Shader.PropertyToID("_WipeProgress");
    private static readonly int PropWebBuildProgress = Shader.PropertyToID("_BuildProgress");

    public GameObject myDrumCollider;
    public GameObject myLanternCollider;

    protected override void Awake()
    {
        base.Awake();

        if (!puzzleLanternManager)
            puzzleLanternManager = FindObjectOfType<PuzzleLanternManager>();

        RefreshPuzzleLanternList();

        cameraShake = FindObjectOfType<CameraShake>();

        if (horrorVCam == null)
        {
            horrorVCam = FindObjectOfType<CinemachineVirtualCamera>();
            if (horrorVCam)
                Debug.Log("[LevelManager3_3] Auto-found CinemachineVirtualCamera: " + horrorVCam.name, this);
        }

        if (playerObject)
        {
            if (playerRb != null)
            {
                rb2dHadSimulated = playerRb.simulated;
                rb2dOldConstraints = playerRb.constraints;
            }

            playerAnimators = playerObject.GetComponentsInChildren<Animator>(true);
            if (playerAnimators != null && playerAnimators.Length > 0)
            {
                animatorOrigSpeeds = new float[playerAnimators.Length];
                for (int i = 0; i < playerAnimators.Length; i++)
                    animatorOrigSpeeds[i] = playerAnimators[i].speed;
            }
        }
        else
        {
            Debug.LogWarning("[LevelManager3_3] Awake: PlayerController.Instance / Player not found.");
        }

        overlayCG = GetOrCreateBlackOverlay();

        Debug.Log($"[LevelManager3_3] Awake: found {puzzleLanterns.Length} PuzzleLantern(s).");

        if (playerSoulEcho == null && playerObject != null)
        {
            playerSoulEcho = playerObject.GetComponentInChildren<PlayerSoulEchoController>(true);
            Debug.Log("[LevelManager3_3] Auto-found PlayerSoulEchoController (in children): " + playerSoulEcho, this);
        }

        if (blackWhiteFlicker == null)
        {
            blackWhiteFlicker = FindObjectOfType<BlackAndWhiteFlicker>();
            Debug.Log("[LevelManager3_3] Auto-found BlackAndWhiteFlicker: " + blackWhiteFlicker, this);
        }

        if (wipeSmearMaterial != null)
            wipeSmearMaterial.SetFloat(PropWipeProgress, 0f);

        if (spiderWebAnimator != null && spiderWebAnimator.spiderWebMaterial != null)
            spiderWebAnimator.spiderWebMaterial.SetFloat(PropWebBuildProgress, 0f);
    }

    private void Start()
    {
        ShowPlayerAndAllowMove();

        if (playerObject != null)
        {
            CachePlayerSprites();
            foreach (var sr in _playerSprites)
            {
                if (!sr) continue;
                sr.enabled = true;
                var c = sr.color;
                c.a = 1f;
                sr.color = c;
            }
        }

        HideSoulEchoSprites();
        ResetHorrorPostFX();
    }

    private void HideSoulEchoSprites()
    {
        if (playerSoulEcho == null) return;

        var soulSrs = playerSoulEcho.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var sr in soulSrs)
        {
            if (!sr) continue;
            sr.enabled = false;
        }
    }

    public void ChangeLanternToWait()
    {
        LanternStall.SetActive(false);
        LanternStall_wait.SetActive(true);
        LanternStall_wrong.SetActive(false);
        LanternStall_correct.SetActive(false);
    }

    public void CheckIfLanternCorrect()
    {
        if (!puzzleLanternManager)
        {
            Debug.LogError("[LevelManager3_3] puzzleLanternManager is NULL, cannot check.", this);
            return;
        }

        puzzleLanternManager.ForceRecheck();
        Debug.Log($"[LevelManager3_3] CheckIfLanternCorrect, Solved = {puzzleLanternManager.Solved}");

        if (puzzleLanternManager.Solved)
        {
            LanternStall.SetActive(false);
            LanternStall_wait.SetActive(false);
            LanternStall_wrong.SetActive(false);
            LanternStall_correct.SetActive(true);

            //在这里加
        }
        else
        {
            LanternStall.SetActive(false);
            LanternStall_wait.SetActive(false);
            LanternStall_correct.SetActive(false);
            LanternStall_wrong.SetActive(true);
        }
    }

    public void lanternBlockColliderDisappear()
    {
        puzzleLanternCollider.enabled = false;
        puzzleLanternColliderChild.enabled = false;
    }

    public void LanternPileDisappear()
    {
        LanternPile.SetActive(false);
        Lanterns.SetActive(true);
        ChangeLanternToWait();
    }

    public void MarkNeedToPlayLanternAnim()
    {
        NeedToPlayLanternAnim = true;
    }

    public void ClearNeedToPlayLanternAnim()
    {
        NeedToPlayLanternAnim = false;
    }

    private void RefreshPuzzleLanternList()
    {
        if (puzzleLanternManager)
            puzzleLanterns = puzzleLanternManager.GetComponentsInChildren<PuzzleLantern>(true);
        else
            puzzleLanterns = FindObjectsOfType<PuzzleLantern>(true);
    }

    private void SetAllLanternsGreen(bool on)
    {
        if (puzzleLanterns == null || puzzleLanterns.Length == 0)
            RefreshPuzzleLanternList();

        if (puzzleLanterns == null) return;

        foreach (var p in puzzleLanterns)
        {
            if (!p) continue;
            p.SetGreen(on);
        }

        Debug.Log($"[LevelManager3_3] SetAllLanternsGreen({on}) on {puzzleLanterns.Length} lanterns.");
    }

    private void ForceAllLanternsHanged(bool hanged)
    {
        if (puzzleLanterns == null || puzzleLanterns.Length == 0)
            RefreshPuzzleLanternList();

        if (puzzleLanterns == null) return;

        foreach (var p in puzzleLanterns)
        {
            if (!p) continue;
            p.ForceSetHanged(hanged);
        }

        Debug.Log($"[LevelManager3_3] ForceAllLanternsHanged({hanged}) on {puzzleLanterns.Length} lanterns.");
    }

    public void PlayLanternHorrorSequence()
    {
        if (isPlayingLanternHorror) return;
        if (!NeedToPlayLanternAnim) return;

        StartCoroutine(CoLanternHorrorSequence());
    }

    private IEnumerator CoLanternHorrorSequence()
    {
        isPlayingLanternHorror = true;
        ClearNeedToPlayLanternAnim();

        Debug.Log("[LevelManager3_3] >>> START Lantern Horror Sequence <<<");

        RefreshPuzzleLanternList();
        FreezePlayer(true);

        if (Lanterns) Lanterns.SetActive(true);
        ForceAllLanternsHanged(true);
        SetAllLanternsGreen(false);

        if (playerSoulEcho != null)
        {
            var soulSrs = playerSoulEcho.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var sr in soulSrs)
            {
                if (!sr) continue;
                sr.enabled = true;
            }

            playerSoulEcho.StartKnockOut();
            StartSoulCameraEffect();

            float waitKnock = playerSoulEcho.knockDuration + 0.2f;
            yield return new WaitForSeconds(waitKnock);
        }
        else
        {
            Debug.LogWarning("[LevelManager3_3] CoLanternHorrorSequence: playerSoulEcho is null.");
        }

        for (int i = 0; i < greenBlinkCount; i++)
        {
            SetAllLanternsGreen(true);
            if (blackWhiteFlicker != null)
                blackWhiteFlicker.SetIntensity(1f);

            if (beastShadow && i == greenBlinkCount - 1)
                beastShadow.SetActive(true);

            yield return new WaitForSeconds(greenOnTime);

            if (beastShadow)
                beastShadow.SetActive(false);

            SetAllLanternsGreen(false);
            if (blackWhiteFlicker != null)
                blackWhiteFlicker.SetIntensity(0f);

            yield return new WaitForSeconds(greenOffTime);
        }

        if (playerSoulEcho != null)
        {
            playerSoulEcho.StartReturn();

            float waitReturn = playerSoulEcho.returnDuration + 0.2f;
            yield return new WaitForSeconds(waitReturn);

            HideSoulEchoSprites();
        }

        RestoreSoulCameraEffect();

        if (cameraShake)
        {
            cameraShake.Shake(
                cameraShake.defaultAmplitude,
                cameraShake.defaultFrequency,
                shakeDuration
            );
        }

        if (horrorAudioSource && horrorRoarClip)
        {
            horrorAudioSource.PlayOneShot(horrorRoarClip);
        }

        yield return new WaitForSeconds(shakeDuration);

        if (wipeSmearMaterial != null)
            yield return PlayWipeSmearEffect();

        if (spiderWebAnimator != null && spiderWebAnimator.spiderWebMaterial != null)
        {
            spiderWebAnimator.Play();
            float buildDur = Mathf.Max(spiderWebAnimator.buildDuration, 0.01f);
            yield return new WaitForSeconds(buildDur + spiderWebExtraHold);
        }

        if (overlayCG)
        {
            // effects still visible during fade-in
            yield return FadeOverlay(1f, blackFadeTime);

            // once fully black, clear post FX
            ResetHorrorPostFX();

            ForceAllLanternsHanged(false);

            yield return new WaitForSeconds(blackHoldTime);

            yield return FadeOverlay(0f, blackFadeTime);
        }
        else
        {
            ForceAllLanternsHanged(false);
            ResetHorrorPostFX();
        }

        if (blackWhiteFlicker != null)
            blackWhiteFlicker.SetIntensity(0f);

        FreezePlayer(false);

        if (MC_scaredDialogue != null)
            MC_scaredDialogue.TriggerDialogue();

        isPlayingLanternHorror = false;
    }

    private void ResetHorrorPostFX()
    {
        if (wipeSmearMaterial != null)
            wipeSmearMaterial.SetFloat(PropWipeProgress, 0f);

        if (spiderWebAnimator != null && spiderWebAnimator.spiderWebMaterial != null)
            spiderWebAnimator.spiderWebMaterial.SetFloat(PropWebBuildProgress, 0f);
    }

    private IEnumerator PlayWipeSmearEffect()
    {
        if (wipeSmearMaterial == null)
            yield break;

        float duration = Mathf.Max(0.05f, wipeSmearDuration);
        float t = 0f;

        wipeSmearMaterial.SetFloat(PropWipeProgress, 0f);

        while (t < duration)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / duration);

            float progress = Mathf.Lerp(0f, 1.2f, u);
            wipeSmearMaterial.SetFloat(PropWipeProgress, progress);

            yield return null;
        }

        wipeSmearMaterial.SetFloat(PropWipeProgress, 1.2f);
    }

    private void StartSoulCameraEffect()
    {
        if (horrorVCam == null) return;

        if (camTweenRoutine != null)
            StopCoroutine(camTweenRoutine);

        originalOrthoSize = horrorVCam.m_Lens.OrthographicSize;
        originalCamRot = horrorVCam.transform.rotation;

        Vector3 e = horrorVCam.transform.eulerAngles;
        float targetZ = e.z + soulTiltAngle;
        Quaternion targetRot = Quaternion.Euler(e.x, e.y, targetZ);

        camTweenRoutine = StartCoroutine(
            TweenCameraTo(soulZoomSize, targetRot, soulZoomTime)
        );
    }

    private void RestoreSoulCameraEffect()
    {
        if (horrorVCam == null) return;

        if (camTweenRoutine != null)
            StopCoroutine(camTweenRoutine);

        camTweenRoutine = StartCoroutine(
            TweenCameraTo(originalOrthoSize, originalCamRot, soulZoomTime)
        );
    }

    private IEnumerator TweenCameraTo(float targetSize, Quaternion targetRot, float duration)
    {
        if (horrorVCam == null) yield break;

        float startSize = horrorVCam.m_Lens.OrthographicSize;
        Quaternion startRot = horrorVCam.transform.rotation;

        float t = 0f;
        duration = Mathf.Max(0.0001f, duration);

        while (t < duration)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / duration);
            float k = soulZoomCurve != null ? soulZoomCurve.Evaluate(u) : u;

            horrorVCam.m_Lens.OrthographicSize = Mathf.Lerp(startSize, targetSize, k);
            horrorVCam.transform.rotation = Quaternion.Slerp(startRot, targetRot, k);

            yield return null;
        }

        horrorVCam.m_Lens.OrthographicSize = targetSize;
        horrorVCam.transform.rotation = targetRot;
    }

    private CanvasGroup GetOrCreateBlackOverlay()
    {
        var exist = GameObject.Find(OverlayName);
        if (exist)
        {
            var cg = exist.GetComponent<CanvasGroup>();
            if (cg) return cg;
        }

        var go = new GameObject(OverlayName, typeof(Canvas), typeof(CanvasGroup));
        var canvas = go.GetComponent<Canvas>();
        var cgNew = go.GetComponent<CanvasGroup>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;
        cgNew.alpha = 0f;
        cgNew.blocksRaycasts = false;
        cgNew.interactable = false;

        var imgGO = new GameObject("Black", typeof(RectTransform), typeof(Image));
        imgGO.transform.SetParent(go.transform, false);
        var rt = imgGO.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var img = imgGO.GetComponent<Image>();
        img.color = Color.black;
        img.raycastTarget = false;

        return cgNew;
    }

    private IEnumerator FadeOverlay(float target, float dur)
    {
        if (!overlayCG) yield break;
        float start = overlayCG.alpha;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            overlayCG.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(t / dur));
            yield return null;
        }
        overlayCG.alpha = target;
    }

    public void ChangeScenePortal()
    {
    }

    private void FreezePlayer(bool freeze)
    {
        if (!playerObject || playerCtrl == null) return;

        if (freeze)
        {
            playerCtrl.DisablePlayerControl();

            if (playerRb != null)
            {
                rb2dHadSimulated = playerRb.simulated;
                rb2dOldConstraints = playerRb.constraints;

                playerRb.velocity = Vector2.zero;
                playerRb.angularVelocity = 0f;
                playerRb.simulated = false;
            }

            if (playerAnimators != null && playerAnimators.Length > 0)
            {
                if (animatorOrigSpeeds == null || animatorOrigSpeeds.Length != playerAnimators.Length)
                    animatorOrigSpeeds = new float[playerAnimators.Length];

                for (int i = 0; i < playerAnimators.Length; i++)
                {
                    var anim = playerAnimators[i];
                    if (!anim) continue;
                    animatorOrigSpeeds[i] = anim.speed;
                    anim.speed = 0f;
                }
            }

            if (Gamemanager.instance)
                Gamemanager.instance.phase = GamePhase.Eventing;
        }
        else
        {
            if (playerRb != null)
            {
                playerRb.simulated = rb2dHadSimulated;
                playerRb.constraints = rb2dOldConstraints;
            }

            if (playerAnimators != null && playerAnimators.Length > 0)
            {
                for (int i = 0; i < playerAnimators.Length; i++)
                {
                    var anim = playerAnimators[i];
                    if (!anim) continue;

                    float orig = (animatorOrigSpeeds != null && i < animatorOrigSpeeds.Length)
                                 ? animatorOrigSpeeds[i]
                                 : 1f;
                    anim.speed = orig;
                }
            }

            playerCtrl.EnablePlayerControl();
            if (Gamemanager.instance)
                Gamemanager.instance.phase = GamePhase.Moving;
        }
    }

    public void CameraZoomIn_3_3()
    {
        CameraZoomIn();
    }

    public void CameraZoomOut_3_3()
    {
        CameraZoomOut();
    }

    public void DisableDrumCollider()
    {
        myDrumCollider.SetActive(false);
    }
    public void DisableLanternCollider()
    {
        myLanternCollider.SetActive(false);
    }
}
