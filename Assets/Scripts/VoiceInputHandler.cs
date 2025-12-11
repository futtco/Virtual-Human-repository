using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Whisper;

public class VoiceInputHandler : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    [Header("UI引用")]
    [SerializeField] private GameObject voicePanel;      // 长按时显示的面板
    [SerializeField] private GameObject cancelArea;      // 取消发送区域
    [SerializeField] private Text recordingText;         // 录音提示文字
    [SerializeField] private VoiceWaveAnimation waveAnimation; // 音浪动画
    [SerializeField] private GameObject waveContainer;   // 音浪容器
    [SerializeField] private GameObject undoIcon;        // 取消图标
    [SerializeField] private GameObject loadIcon;        // 加载图标

    [Header("聊天管理器")]
    [SerializeField] private ChatManager chatManager;

    [Header("语音识别")]
    [SerializeField] private WhisperManager whisperManager;

    private bool isRecording = false;
    private bool isCanceling = false;
    private RectTransform cancelAreaRect;
    private AudioClip clip;
    private int recordingPosition = 0;

    void Start()
    {
        if (voicePanel != null)
        {
            voicePanel.SetActive(false);
        }

        if (cancelArea != null)
        {
            cancelAreaRect = cancelArea.GetComponent<RectTransform>();
        }

        if (chatManager == null)
        {
            chatManager = FindObjectOfType<ChatManager>();
        }

        // 初始化时隐藏加载图标
        if (loadIcon != null)
        {
            loadIcon.SetActive(false);
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        StartRecording();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        StopRecording();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isRecording) return;

        CheckPointerPosition(eventData.position);
    }

    private void StartRecording()
    {
        isRecording = true;
        isCanceling = false;

        if (voicePanel != null)
        {
            voicePanel.SetActive(true);
        }

        if (recordingText != null)
        {
            recordingText.text = "上滑，取消发送";
        }

        if (waveAnimation != null)
        {
            waveAnimation.StartAnimation();
        }

        if (waveContainer != null)
        {
            waveContainer.SetActive(true);
        }

        if (undoIcon != null)
        {
            undoIcon.SetActive(false);
        }

        Debug.Log("开始录音");

        if (Microphone.devices.Length == 0)
        {
            Debug.LogError("没有检测到麦克风设备!");
            return;
        }

        Debug.Log($"使用麦克风: {Microphone.devices[0]}");
        clip = Microphone.Start(null, false, 10, 16000);
        Debug.Log("开始录音... (最长10秒)");
    }

    private void StopRecording()
    {
        if (!isRecording) return;

        bool shouldCancel = isCanceling;

        isRecording = false;
        isCanceling = false;

        if (waveAnimation != null)
        {
            waveAnimation.StopAnimation();
        }

        if (voicePanel != null)
        {
            voicePanel.SetActive(false);
        }

        Debug.Log("停止录音");

        if (clip == null)
        {
            Debug.LogWarning("没有正在进行的录音!");
            return;
        }

        recordingPosition = Microphone.GetPosition(null);

        Microphone.End(null);
        Debug.Log($"录音结束,音频长度: {clip.length}秒, 采样数: {clip.samples}, 实际录音位置: {recordingPosition}");

        if (shouldCancel)
        {
            CancelVoiceMessage();
        }
        else
        {
            SendVoiceAsText();
        }
    }

    private void CheckPointerPosition(Vector2 position)
    {
        bool wasInCancelArea = isCanceling;

        isCanceling = IsPointerInArea(position, cancelAreaRect);

        if (isCanceling != wasInCancelArea)
        {
            UpdateRecordingText();
            UpdateVisualState();
        }
    }

    private bool IsPointerInArea(Vector2 pointerPosition, RectTransform areaRect)
    {
        if (areaRect == null) return false;

        return RectTransformUtility.RectangleContainsScreenPoint(
            areaRect,
            pointerPosition,
            null  // 对于Overlay Canvas使用null
        );
    }

    private void UpdateRecordingText()
    {
        if (recordingText == null) return;

        if (isCanceling)
        {
            recordingText.text = "松开，取消发送";
        }
        else
        {
            recordingText.text = "上滑，取消发送";
        }
    }

    /// <summary>
    /// 更新音浪和图标的显示状态
    /// </summary>
    private void UpdateVisualState()
    {
        if (isCanceling)
        {
            if (waveContainer != null)
            {
                waveContainer.SetActive(false);
            }

            if (undoIcon != null)
            {
                undoIcon.SetActive(true);
            }
        }
        else
        {
            if (waveContainer != null)
            {
                waveContainer.SetActive(true);
            }

            if (undoIcon != null)
            {
                undoIcon.SetActive(false);
            }
        }
    }

    private async void SendVoiceAsText()
    {
        Debug.Log("语音转文字并发送");

        if (clip == null)
        {
            Debug.LogWarning("没有录音数据!");
            return;
        }

        if (whisperManager == null)
        {
            Debug.LogError("WhisperManager未设置!");
            return;
        }

        if (recordingPosition <= 0)
        {
            Debug.LogWarning("没有有效的录音数据!");
            return;
        }

        // 显示加载图标
        if (loadIcon != null)
        {
            loadIcon.SetActive(true);
        }

        try
        {
            // 只获取实际录音的部分
            float[] allSamples = new float[clip.samples];
            clip.GetData(allSamples, 0);

            // 修剪音频数据，只保留实际录音的部分
            float[] samples = new float[recordingPosition];
            System.Array.Copy(allSamples, 0, samples, 0, recordingPosition);

            Debug.Log($"实际录音样本数: {recordingPosition}, 总样本数: {clip.samples}");

            if (!whisperManager.IsLoaded)
            {
                Debug.LogWarning("Whisper模型未加载,等待加载完成...");
                await whisperManager.InitModel();
            }

            Debug.Log("正在识别语音...");
            var result = await whisperManager.GetTextAsync(samples, 16000, 1);

            if (result != null && !string.IsNullOrEmpty(result.Result))
            {
                Debug.Log($"<color=green>识别结果: {result.Result}</color>");

                if (chatManager != null)
                {
                    chatManager.AddSelfMessage(result.Result);
                }
            }
            else
            {
                Debug.LogWarning("未识别到语音内容");
            }
        }
        finally
        {
            // 无论成功还是失败，都隐藏加载图标
            if (loadIcon != null)
            {
                loadIcon.SetActive(false);
            }
        }
    }


    private void CancelVoiceMessage()
    {
        Debug.Log("取消发送语音消息");

        if (clip != null)
        {
            Destroy(clip);
            clip = null;
        }
    }
}
