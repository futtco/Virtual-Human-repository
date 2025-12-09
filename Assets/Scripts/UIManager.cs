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
    }

    private void InitializeListeners()
    {
        speakBtn.onClick.AddListener(ChangeToSpeak);
        WriteBtn.onClick.AddListener(ChangeToWrite);
        sendBtn.onClick.AddListener(OnSendButtonClick);

        // 支持回车发送
        inputField.onEndEdit.AddListener(OnInputFieldEndEdit);
    }

    private void ChangeToSpeak()
    {
        inputField.gameObject.SetActive(false);
        onSpeakBtn.SetActive(true);

        speakBtn.gameObject.SetActive(false);
        WriteBtn.gameObject.SetActive(true);
    }

    private void ChangeToWrite()
    {
        onSpeakBtn.SetActive(false);
        inputField.gameObject.SetActive(true);

        WriteBtn.gameObject.SetActive(false);
        speakBtn.gameObject.SetActive(true);
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
    /// 发送消息到聊天界面
    /// </summary>
    private void SendMessage()
    {
        // 获取输入框内容
        string message = inputField.text.Trim();

        // 检查消息是否为空
        if (string.IsNullOrEmpty(message))
        {
            Debug.LogWarning("消息内容为空，无法发送");
            return;
        }

        // 检查ChatManager是否存在
        if (chatManager == null)
        {
            Debug.LogError("ChatManager未设置，无法发送消息");
            return;
        }

        chatManager.AddSelfMessage(message);

        inputField.text = string.Empty;

        // 重新激活输入框焦点，方便继续输入
        inputField.ActivateInputField();

        Debug.Log($"已发送消息: {message}");
    }
}
