using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerAnimControllerSelector : MonoBehaviour
{
    [Header("Animator (同一个Player上的Animator)")]
    [SerializeField] private Animator animator;

    [Header("动画控制器")]
    [SerializeField] private RuntimeAnimatorController normalController;
    [SerializeField] private RuntimeAnimatorController darkController;

    [Header("需要Dark控制器的【精确场景名】")]
    [SerializeField] private List<string> darkExactNames = new() { "Level3-0" };

    [Header("需要Dark控制器的【场景名前缀】")]
    [SerializeField] private List<string> darkPrefixes = new() { "Level1", "Level2" };

    // 可选：紧急覆盖（比如临时强制Dark）
    [Header("可选：强制覆盖")]
    public bool forceDark = false;

    private void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        // 确保这个选择器在切场景时不会被销毁（跟随你的 Player）
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        // 订阅场景切换
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        // 进入当前场景时先应用一次
        ApplyForScene(SceneManager.GetActiveScene().name);
    }

    private void OnDisable()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
    }

    private void OnActiveSceneChanged(Scene prev, Scene next)
    {
        ApplyForScene(next.name);
    }

    private void ApplyForScene(string sceneName)
    {
        if (animator == null) return;

        bool useDark = ShouldUseDark(sceneName);
        var target = useDark ? darkController : normalController;

        if (target != null && animator.runtimeAnimatorController != target)
        {
            animator.runtimeAnimatorController = target;
        }
    }

    private bool ShouldUseDark(string sceneName)
    {
        if (forceDark) return true;

        // 精确匹配
        for (int i = 0; i < darkExactNames.Count; i++)
        {
            if (!string.IsNullOrEmpty(darkExactNames[i]) && sceneName == darkExactNames[i])
                return true;
        }

        // 前缀匹配
        for (int i = 0; i < darkPrefixes.Count; i++)
        {
            var p = darkPrefixes[i];
            if (!string.IsNullOrEmpty(p) && sceneName.StartsWith(p))
                return true;
        }

        return false;
    }
}
