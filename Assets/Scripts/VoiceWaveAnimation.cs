using UnityEngine;
using UnityEngine.UI;

public class VoiceWaveAnimation : MonoBehaviour
{
    [Header("音浪条设置")]
    [SerializeField] private RectTransform[] waveBars;  // 音浪条数组
    [SerializeField] private float minHeight = 20f;     // 最小高度
    [SerializeField] private float maxHeight = 60f;    // 最大高度
    [SerializeField] private float animationSpeed = 5f; // 动画速度

    private float[] targetHeights;
    private float[] currentHeights;
    private float[] timers;
    private bool isAnimating = false;

    void Start()
    {
        if (waveBars != null && waveBars.Length > 0)
        {
            InitializeWaveBars();
        }
    }

    void Update()
    {
        if (isAnimating && waveBars != null)
        {
            AnimateWaveBars();
        }
    }

    /// <summary>
    /// 初始化音浪条
    /// </summary>
    private void InitializeWaveBars()
    {
        int count = waveBars.Length;
        targetHeights = new float[count];
        currentHeights = new float[count];
        timers = new float[count];

        for (int i = 0; i < count; i++)
        {
            if (waveBars[i] != null)
            {
                // 设置初始高度
                currentHeights[i] = minHeight;
                targetHeights[i] = minHeight;
                timers[i] = Random.Range(0f, 1f);

                // 设置初始大小
                waveBars[i].sizeDelta = new Vector2(waveBars[i].sizeDelta.x, minHeight);
            }
        }
    }

    /// <summary>
    /// 开始播放动画
    /// </summary>
    public void StartAnimation()
    {
        isAnimating = true;
        Debug.Log("音浪动画开始");
    }

    /// <summary>
    /// 停止播放动画
    /// </summary>
    public void StopAnimation()
    {
        isAnimating = false;

        // 将所有条恢复到最小高度
        if (waveBars != null)
        {
            for (int i = 0; i < waveBars.Length; i++)
            {
                if (waveBars[i] != null)
                {
                    waveBars[i].sizeDelta = new Vector2(waveBars[i].sizeDelta.x, minHeight);
                }
            }
        }

        Debug.Log("音浪动画停止");
    }

    /// <summary>
    /// 动画更新
    /// </summary>
    private void AnimateWaveBars()
    {
        for (int i = 0; i < waveBars.Length; i++)
        {
            if (waveBars[i] == null) continue;

            // 更新计时器
            timers[i] += Time.deltaTime * animationSpeed;

            // 随机改变目标高度
            if (timers[i] >= 1f)
            {
                targetHeights[i] = Random.Range(minHeight, maxHeight);
                timers[i] = 0f;
            }

            // 平滑过渡到目标高度
            currentHeights[i] = Mathf.Lerp(currentHeights[i], targetHeights[i], Time.deltaTime * animationSpeed);

            // 更新UI高度
            waveBars[i].sizeDelta = new Vector2(waveBars[i].sizeDelta.x, currentHeights[i]);
        }
    }

    /// <summary>
    /// 根据音量设置动画强度（可选，用于实际音频输入）
    /// </summary>
    public void SetVolumeLevel(float volume)
    {
        if (!isAnimating) return;

        // 将音量映射到高度范围
        float normalizedVolume = Mathf.Clamp01(volume);
        float heightRange = maxHeight - minHeight;

        for (int i = 0; i < waveBars.Length; i++)
        {
            // 基于音量添加随机变化
            float randomFactor = Random.Range(0.7f, 1.3f);
            targetHeights[i] = minHeight + (heightRange * normalizedVolume * randomFactor);
        }
    }
}
