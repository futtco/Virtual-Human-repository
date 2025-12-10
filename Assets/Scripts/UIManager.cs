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

        if (chatManager == null)
        {
            Debug.LogError("ChatManager未设置，无法发送消息");
            return;
        }

        chatManager.AddSelfMessage(message);

        inputField.text = string.Empty;

        UpdateSendButtonState();

        // 重新激活输入框焦点，方便继续输入
        inputField.ActivateInputField();

        Debug.Log($"已发送消息: {message}");
    }
}
