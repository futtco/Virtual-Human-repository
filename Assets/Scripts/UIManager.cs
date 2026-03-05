using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("UI按钮")]
    [SerializeField]
    private Button speakBtn;

    [SerializeField]
    private Button sendBtn;

    [SerializeField]
    private Button WriteBtn;

    [Header("输入组件")]
    [SerializeField]
    private InputField inputField;

    [SerializeField]
    private GameObject onSpeakBtn;

    [SerializeField]
    private GameObject voiceArea;

    [Header("聊天管理器")]
    [SerializeField]
    private ChatManager chatManager;

    [Header("AI管理器")]
    [SerializeField]
    private AIManager aiManager;

    [Header("虚拟人控制")]
    [SerializeField]
    private VirtualHumanController virtualHumanController;

    void Start()
    {
        InitializeUI();
        InitializeListeners();

        // 如果未设置ChatManager，尝试自动查找
        if (chatManager == null)
        {
            chatManager = FindObjectOfType<ChatManager>();
            if (chatManager == null)
            {
                Debug.LogError("ChatManager未找到！请在Inspector中设置或确保场景中存在ChatManager");
            }
        }

        // 如果未设置AIManager，尝试自动查找
        if (aiManager == null)
        {
            aiManager = FindObjectOfType<AIManager>();
        }

        // 如果未设置VirtualHumanController，尝试自动查找
        if (virtualHumanController == null)
        {
            virtualHumanController = FindObjectOfType<VirtualHumanController>();
        }
    }

    private void InitializeUI()
    {
        speakBtn.gameObject.SetActive(true);
        inputField.gameObject.SetActive(true);
        WriteBtn.gameObject.SetActive(false);
        onSpeakBtn.SetActive(false);
        voiceArea.SetActive(false);
    }

    private void InitializeListeners()
    {
        speakBtn.onClick.AddListener(ChangeToSpeak);
        WriteBtn.onClick.AddListener(ChangeToWrite);
        sendBtn.onClick.AddListener(OnSendButtonClick);

        // 支持回车发送
        inputField.onEndEdit.AddListener(OnInputFieldEndEdit);

        inputField.onValueChanged.AddListener(OnInputFieldValueChanged);

        UpdateSendButtonState();
    }

    private void ChangeToSpeak()
    {
        inputField.gameObject.SetActive(false);
        onSpeakBtn.SetActive(true);

        speakBtn.gameObject.SetActive(false);
        WriteBtn.gameObject.SetActive(true);

        sendBtn.interactable = false;
    }

    private void ChangeToWrite()
    {
        onSpeakBtn.SetActive(false);
        inputField.gameObject.SetActive(true);

        WriteBtn.gameObject.SetActive(false);
        speakBtn.gameObject.SetActive(true);

        UpdateSendButtonState();
    }

    /// <summary>
    /// 发送按钮点击事件
    /// </summary>
    private void OnSendButtonClick()
    {
        SendMessage();
    }

    /// <summary>
    /// 输入框结束编辑事件（支持回车发送）
    /// </summary>
    private void OnInputFieldEndEdit(string text)
    {
        // 按下回车键时发送消息
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            SendMessage();
        }
    }

    /// <summary>
    /// 输入框内容变化事件
    /// </summary>
    private void OnInputFieldValueChanged(string text)
    {
        UpdateSendButtonState();
    }

    /// <summary>
    /// 更新发送按钮的可交互状态
    /// </summary>
    private void UpdateSendButtonState()
    {
        bool hasContent = !string.IsNullOrEmpty(inputField.text);
        sendBtn.interactable = hasContent;
    }

    /// <summary>
    /// 发送消息到聊天界面
    /// </summary>
    private void SendMessage()
    {
        string message = inputField.text;

        if (string.IsNullOrEmpty(message))
        {
            Debug.LogWarning("消息内容为空，无法发送");
            return;
        }

        inputField.text = string.Empty;
        UpdateSendButtonState();

        // 重新激活输入框焦点，方便继续输入
        inputField.ActivateInputField();

        Debug.Log($"已发送消息: {message}");

        // ===== 使用虚拟人控制器处理消息 =====
        if (virtualHumanController != null)
        {
            // 使用虚拟人控制器 - 完整流程：显示消息 -> AI回复 -> 语音生成 -> 表情和口型
            virtualHumanController.HandleUserMessage(message);
        }
        else
        {
            // Fallback: 如果没有虚拟人控制器，使用原来的方式
            if (chatManager == null)
            {
                Debug.LogError("ChatManager未设置，无法发送消息");
                return;
            }

            chatManager.AddSelfMessage(message);

            if (aiManager != null)
            {
                Debug.Log("正在调用AI获取回复...");
                aiManager.GetAIResponse(
                    message,
                    onComplete: (aiResponse) =>
                    {
                        Debug.Log($"<color=cyan>AI回复: {aiResponse}</color>");
                        if (chatManager != null)
                        {
                            chatManager.AddOtherMessage(aiResponse);
                        }
                    },
                    onError: (error) =>
                    {
                        Debug.LogError($"AI调用失败: {error}");
                        if (chatManager != null)
                        {
                            chatManager.AddOtherMessage($"抱歉，AI服务出错: {error}");
                        }
                    }
                );
            }
            else
            {
                Debug.LogWarning("AIManager未找到，无法获取AI回复");
            }
        }
    }
}
