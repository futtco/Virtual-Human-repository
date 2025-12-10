using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class VoiceInputHandler : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    [Header("UI引用")]
    [SerializeField] private GameObject voicePanel;      // 长按时显示的面板
    [SerializeField] private GameObject cancelArea;      // 取消发送区域
    [SerializeField] private Text recordingText;         // 录音提示文字
    [SerializeField] private VoiceWaveAnimation waveAnimation; // 音浪动画
    [SerializeField] private GameObject waveContainer;   // 音浪容器
    [SerializeField] private GameObject undoIcon;        // 取消图标

    [Header("聊天管理器")]
    [SerializeField] private ChatManager chatManager;

    private bool isRecording = false;
    private bool isCanceling = false;
    private RectTransform cancelAreaRect;

    void Start()
    {
        // 初始化隐藏面板
        if (voicePanel != null)
        {
            voicePanel.SetActive(false);
        }

        // 获取取消区域组件
        if (cancelArea != null)
        {
            cancelAreaRect = cancelArea.GetComponent<RectTransform>();
        }

        // 如果未设置ChatManager，尝试自动查找
        if (chatManager == null)
        {
            chatManager = FindObjectOfType<ChatManager>();
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

        // 开始音浪动画，隐藏 undo icon
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

        // TODO: 这里添加实际的录音启动代码
        // 例如：Microphone.Start(null, false, 60, 44100);
    }

    private void StopRecording()
    {
        if (!isRecording) return;

        // 保存当前是否取消的状态
        bool shouldCancel = isCanceling;

        // 重置状态
        isRecording = false;
        isCanceling = false;

        // 停止音浪动画
        if (waveAnimation != null)
        {
            waveAnimation.StopAnimation();
        }

        // 隐藏语音面板
        if (voicePanel != null)
        {
            voicePanel.SetActive(false);
        }

        // 根据状态执行操作
        if (shouldCancel)
        {
            CancelVoiceMessage();
        }
        else
        {
            SendVoiceAsText();
        }

        Debug.Log("停止录音");

        // TODO: 这里添加实际的录音停止代码
        // 例如：Microphone.End(null);
    }

    private void CheckPointerPosition(Vector2 position)
    {
        bool wasInCancelArea = isCanceling;

        // 检查是否在取消区域
        isCanceling = IsPointerInArea(position, cancelAreaRect);

        // 如果状态改变，更新UI
        if (isCanceling != wasInCancelArea)
        {
            UpdateRecordingText();
            UpdateVisualState();
        }
    }

    private bool IsPointerInArea(Vector2 pointerPosition, RectTransform areaRect)
    {
        if (areaRect == null) return false;

        // 使用屏幕坐标判断
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
            // 在取消区域：隐藏整个音浪容器，显示 undo icon
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
            // 不在取消区域：显示音浪容器，隐藏 undo icon
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

    private void SendVoiceAsText()
    {
        Debug.Log("语音转文字并发送");

        // TODO: 这里添加实际的语音转文字逻辑
        // 例如：
        // AudioClip clip = GetRecordedAudio();
        // string text = ConvertSpeechToText(clip);

        // 临时使用占位文本
        string convertedText = "这是语音转换的文字";

        if (chatManager != null)
        {
            chatManager.AddSelfMessage(convertedText);
        }
    }


    private void CancelVoiceMessage()
    {
        Debug.Log("取消发送语音消息");

        // TODO: 这里添加取消语音的逻辑
        // 例如：清除录音缓存
    }
}
