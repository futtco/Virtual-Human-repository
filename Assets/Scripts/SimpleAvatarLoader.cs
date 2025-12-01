using UnityEngine;
using ReadyPlayerMe.Core;

public class SimpleAvatarLoader : MonoBehaviour
{
    [Header("Avatar Settings")]
    [Tooltip("Ready Player Me avatar URL")]
    [SerializeField] private string avatarUrl = "https://models.readyplayer.me/692d17d8dbdba5d65ffb0ca7.glb";

    [Tooltip("Load avatar automatically on start")]
    [SerializeField] private bool loadOnStart = true;

    [Tooltip("Animator controller to apply to loaded avatar")]
    [SerializeField] private RuntimeAnimatorController animatorController;

    private AvatarObjectLoader avatarLoader;
    private GameObject currentAvatar;

    void Start()
    {
        // 创建 AvatarObjectLoader 实例
        avatarLoader = new AvatarObjectLoader();

        // 订阅事件
        avatarLoader.OnCompleted += OnAvatarLoadCompleted;
        avatarLoader.OnFailed += OnAvatarLoadFailed;
        avatarLoader.OnProgressChanged += OnAvatarLoadProgress;

        if (loadOnStart && !string.IsNullOrEmpty(avatarUrl))
        {
            LoadAvatar();
        }
    }

    /// <summary>
    /// 加载头像
    /// </summary>
    public void LoadAvatar()
    {
        if (string.IsNullOrEmpty(avatarUrl))
        {
            Debug.LogError("Avatar URL is empty!");
            return;
        }

        Debug.Log($"开始加载头像: {avatarUrl}");
        avatarLoader.LoadAvatar(avatarUrl);
    }

    /// <summary>
    /// 使用自定义 URL 加载头像
    /// </summary>
    public void LoadAvatar(string url)
    {
        avatarUrl = url;
        LoadAvatar();
    }

    /// <summary>
    /// 头像加载完成回调
    /// </summary>
    private void OnAvatarLoadCompleted(object sender, CompletionEventArgs args)
    {
        Debug.Log("头像加载成功!");

        // 如果已有头像，先销毁
        if (currentAvatar != null)
        {
            Destroy(currentAvatar);
        }

        currentAvatar = args.Avatar;

        // 设置父对象和位置
        currentAvatar.transform.SetParent(transform);
        currentAvatar.transform.localPosition = Vector3.zero;
        currentAvatar.transform.localRotation = Quaternion.identity;

        // 如果指定了动画控制器，应用到头像
        if (animatorController != null)
        {
            Animator animator = currentAvatar.GetComponent<Animator>();
            if (animator != null)
            {
                animator.runtimeAnimatorController = animatorController;
            }
        }

        Debug.Log($"头像已生成: {currentAvatar.name}");
    }

    /// <summary>
    /// 头像加载失败回调
    /// </summary>
    private void OnAvatarLoadFailed(object sender, FailureEventArgs args)
    {
        Debug.LogError($"头像加载失败: {args.Type} - {args.Message}");
    }

    /// <summary>
    /// 头像加载进度回调
    /// </summary>
    private void OnAvatarLoadProgress(object sender, ProgressChangeEventArgs args)
    {
        //Debug.Log($"加载进度: {args.Progress:P0} - {args.Operation}");
    }

    void OnDestroy()
    {
        // 取消订阅事件
        if (avatarLoader != null)
        {
            avatarLoader.OnCompleted -= OnAvatarLoadCompleted;
            avatarLoader.OnFailed -= OnAvatarLoadFailed;
            avatarLoader.OnProgressChanged -= OnAvatarLoadProgress;
        }
    }
}
