using UnityEngine;

/// <summary>
/// uLipSync管理器 - 处理实时音频口型同步
/// </summary>
public class LipSyncManager : MonoBehaviour
{
    [Header("口型同步配置")]
    [SerializeField] private uLipSync.uLipSync lipSyncComponent;
    [SerializeField] private AudioSource audioSource;

    [Header("调试")]
    [SerializeField] private bool showDebugInfo = true;

    void Start()
    {
        // 尝试自动查找 AudioSource
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (audioSource == null)
        {
            audioSource = FindObjectOfType<AudioSource>();
        }

        // lipSyncComponent 必须在 Inspector 中手动赋值
        if (lipSyncComponent == null)
        {
            Debug.LogError("LipSyncManager: 请在 Inspector 中将 uLipSync 组件拖入 Lip Sync Component 字段");
            return;
        }

        if (audioSource == null)
        {
            Debug.LogError("LipSyncManager: 找不到 AudioSource 组件！");
            return;
        }

        Debug.Log("✓ 口型同步管理器已初始化");
    }

    /// <summary>
    /// 播放音频文件并自动进行口型同步
    /// </summary>
    public void PlayAudioWithLipSync(AudioClip clip)
    {
        if (audioSource == null || lipSyncComponent == null)
        {
            Debug.LogError("无法进行口型同步: 缺少音频源或同步上下文");
            return;
        }

        audioSource.clip = clip;
        audioSource.Play();

        Debug.Log($"播放音频并进行口型同步: {clip.name}");
    }

    /// <summary>
    /// 停止音频播放
    /// </summary>
    public void StopAudio()
    {
        if (audioSource != null)
        {
            audioSource.Stop();
        }
    }

    /// <summary>
    /// 暂停音频
    /// </summary>
    public void PauseAudio()
    {
        if (audioSource != null)
        {
            audioSource.Pause();
        }
    }

    /// <summary>
    /// 恢复音频播放
    /// </summary>
    public void ResumeAudio()
    {
        if (audioSource != null)
        {
            audioSource.UnPause();
        }
    }

    /// <summary>
    /// 获取当前音素信息 (通过反射)
    /// </summary>
    public object GetCurrentLipSyncInfo()
    {
        if (lipSyncComponent == null)
        {
            return null;
        }

        try
        {
            var method = lipSyncComponent.GetType().GetMethod("GetLipSyncInfo",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            if (method != null)
            {
                return method.Invoke(lipSyncComponent, null);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"获取口型信息失败: {e.Message}");
        }

        return null;
    }

    /// <summary>
    /// 检查音频是否正在播放
    /// </summary>
    public bool IsAudioPlaying => audioSource != null && audioSource.isPlaying;

    void OnGUI()
    {
        if (!showDebugInfo) return;

        GUILayout.BeginArea(new Rect(10, 10, 300, 150));
        GUILayout.BeginVertical("box");

        if (lipSyncComponent != null)
        {
            GUILayout.Label("口型同步: 已连接");
        }
        else
        {
            GUILayout.Label("口型同步: 未连接");
        }

        if (audioSource != null)
        {
            GUILayout.Label($"音频播放: {(IsAudioPlaying ? "播放中" : "停止")}");
            if (audioSource.clip != null)
            {
                GUILayout.Label($"播放进度: {audioSource.time:F1}s / {audioSource.clip.length:F1}s");
            }
        }

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }
}
